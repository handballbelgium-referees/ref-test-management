using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Handball.Belgium.RefTestManagement.Api;

public static class SecurityStartup
{
    internal static void AddSecurityConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var publicOrigin = GetCanonicalPublicOrigin(configuration);

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.HttpOnly = true;
                options.Events.OnValidatePrincipal = async context =>
                {
                    var refresh = await RefreshPermissionClaimsAsync(
                        context.HttpContext,
                        context.Principal,
                        context.HttpContext.RequestAborted);
                    if (!refresh.Succeeded)
                    {
                        context.RejectPrincipal();
                        return;
                    }

                    if (refresh.ClaimsChanged)
                        context.ShouldRenew = true;
                };
            })
            .AddOpenIdConnect("Auth0", options => ConfigureOpenIdConnect(options, configuration, publicOrigin))
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = $"https://{configuration["Auth0:Domain"]}";
                options.Audience = configuration["Auth0:Audience"];
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var refresh = await RefreshPermissionClaimsAsync(
                            context.HttpContext,
                            context.Principal,
                            context.HttpContext.RequestAborted);
                        if (!refresh.Succeeded)
                            context.Fail("The current permissions could not be verified.");
                    }
                };
            });
    }

    private static async Task<PermissionRefreshResult> RefreshPermissionClaimsAsync(
        HttpContext httpContext,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken)
    {
        if (principal is null)
            return default;

        var identity = principal.Identities.FirstOrDefault(candidate => candidate.IsAuthenticated);
        if (identity is null)
            return default;

        try
        {
            var permissionService = httpContext.RequestServices.GetRequiredService<IPermissionSnapshotService>();
            var permissions = await permissionService.GetCurrentPermissionsAsync(principal, cancellationToken);
            if (permissions is null)
                return default;

            var existingClaims = principal.FindAll("permissions").ToArray();
            var existingPermissions = existingClaims
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (existingClaims.Length == permissions.Count && existingPermissions.SetEquals(permissions))
                return new PermissionRefreshResult(true, false);

            foreach (var claimsIdentity in principal.Identities)
            {
                foreach (var claim in claimsIdentity.FindAll("permissions").ToArray())
                    claimsIdentity.RemoveClaim(claim);
            }

            foreach (var permission in permissions)
                identity.AddClaim(new Claim("permissions", permission, ClaimValueTypes.String, "Auth0"));

            return new PermissionRefreshResult(true, true);
        }
        catch (Exception)
        {
            return default;
        }
    }

    private static string GetCanonicalPublicOrigin(IConfiguration configuration)
    {
        const string configurationKey = "Auth0:PublicOrigin";
        var value = configuration[configurationKey];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var origin)
            || (origin.Scheme != Uri.UriSchemeHttps && !IsDevelopment())
            || string.IsNullOrEmpty(origin.Host)
            || origin.UserInfo.Length > 0
            || origin.AbsolutePath != "/"
            || origin.Query.Length > 0
            || origin.Fragment.Length > 0)
            throw new InvalidOperationException(
                $"{configurationKey} must be an absolute HTTPS origin without credentials, path, query, or fragment.");

        return origin.GetLeftPart(UriPartial.Authority);
    }

    private static bool IsDevelopment()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
    }

    private static void ConfigureOpenIdConnect(
        OpenIdConnectOptions options,
        IConfiguration configuration,
        string publicOrigin)
    {
        options.Authority = $"https://{configuration["Auth0:Domain"]}";
        options.ClientId = configuration["Auth0:ClientId"];
        options.ClientSecret = configuration["Auth0:ClientSecret"];

        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.FormPost;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        options.CallbackPath = new PathString("/callback");

        options.ClaimsIssuer = "Auth0";

        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = async context =>
            {
                var refresh = await RefreshPermissionClaimsAsync(
                    context.HttpContext,
                    context.Principal,
                    context.HttpContext.RequestAborted);
                if (!refresh.Succeeded)
                    context.Fail("The current permissions could not be verified.");
            },

            OnRedirectToIdentityProviderForSignOut = context =>
            {
                var logoutUri =
                    $"https://{configuration["Auth0:Domain"]}/v2/logout?client_id={configuration["Auth0:ClientId"]}";

                var postLogoutUri = context.Properties.RedirectUri;
                if (!string.IsNullOrEmpty(postLogoutUri))
                {
                    if (postLogoutUri.StartsWith('/'))
                    {
                        // transform to absolute
                        postLogoutUri = publicOrigin + context.Request.PathBase + postLogoutUri;
                    }

                    logoutUri += $"&returnTo={Uri.EscapeDataString(postLogoutUri)}";
                }

                context.Response.Redirect(logoutUri);
                context.HandleResponse();

                return Task.CompletedTask;
            },

            OnRedirectToIdentityProvider = context =>
            {
                context.ProtocolMessage.SetParameter("audience", configuration["Auth0:Audience"]);
                return Task.CompletedTask;
            }
        };
    }

    private readonly record struct PermissionRefreshResult(bool Succeeded, bool ClaimsChanged);
}
