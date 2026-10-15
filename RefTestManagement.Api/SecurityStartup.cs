using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Api.Identity;
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
        var provider = IdentityProviderSelector.Resolve(configuration);
        IdentityProviderSelector.ValidateSettings(configuration, provider);
        var schemeName = IdentityProviderSelector.SchemeName(provider);
        var issuer = IdentityProviderSelector.ClaimsIssuer(provider);
        var authority = IdentityProviderSelector.Authority(configuration, provider);
        var audience = configuration[$"{IdentityProviderSelector.SettingsPath(provider)}:Audience"];

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
                        issuer,
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
            .AddOpenIdConnect(schemeName, options => ConfigureOpenIdConnect(options, configuration, provider))
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var refresh = await RefreshPermissionClaimsAsync(
                            context.HttpContext,
                            context.Principal,
                            issuer,
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
        string issuer,
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
                identity.AddClaim(new Claim("permissions", permission, ClaimValueTypes.String, issuer));

            return new PermissionRefreshResult(true, true);
        }
        catch (Exception)
        {
            return default;
        }
    }

    private static void ConfigureOpenIdConnect(
        OpenIdConnectOptions options,
        IConfiguration configuration,
        IdentityProvider provider)
    {
        var section = IdentityProviderSelector.SettingsPath(provider);
        var authority = IdentityProviderSelector.Authority(configuration, provider);
        var clientId = configuration[$"{section}:ClientId"];
        var issuer = IdentityProviderSelector.ClaimsIssuer(provider);

        options.Authority = authority;
        options.ClientId = clientId;
        options.ClientSecret = configuration[$"{section}:ClientSecret"];

        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.FormPost;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        options.CallbackPath = new PathString("/callback");

        options.ClaimsIssuer = issuer;

        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = async context =>
            {
                var refresh = await RefreshPermissionClaimsAsync(
                    context.HttpContext,
                    context.Principal,
                    issuer,
                    context.HttpContext.RequestAborted);
                if (!refresh.Succeeded)
                    context.Fail("The current permissions could not be verified.");
            },

            OnRedirectToIdentityProviderForSignOut = context =>
            {
                var logoutUri = IdentityProviderSelector.LogoutUri(authority, clientId ?? string.Empty, provider);
                var returnParameter = IdentityProviderSelector.LogoutReturnParameter(provider);

                var postLogoutUri = context.Properties.RedirectUri;
                if (!string.IsNullOrEmpty(postLogoutUri))
                {
                    if (postLogoutUri.StartsWith('/'))
                    {
                        // transform to absolute
                        var request = context.Request;
                        postLogoutUri = request.Scheme + "://" + request.Host + request.PathBase + postLogoutUri;
                    }

                    logoutUri += $"&{returnParameter}={Uri.EscapeDataString(postLogoutUri)}";
                }

                context.Response.Redirect(logoutUri);
                context.HandleResponse();

                return Task.CompletedTask;
            },

            OnRedirectToIdentityProvider = context =>
            {
                if (IdentityProviderSelector.SendsAudienceOnAuthorize(provider))
                    context.ProtocolMessage.SetParameter("audience", configuration["Identity:Auth0:Audience"]);
                return Task.CompletedTask;
            }
        };
    }

    private readonly record struct PermissionRefreshResult(bool Succeeded, bool ClaimsChanged);
}
