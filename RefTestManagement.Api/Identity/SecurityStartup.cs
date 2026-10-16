using System.Globalization;
using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Api.Identity;
using Handball.Belgium.RefTestManagement.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Handball.Belgium.RefTestManagement.Api;

public static class SecurityStartup
{
    // Token names written by OpenIdConnectOptions.SaveTokens.
    private const string AccessTokenName = "access_token";
    private const string RefreshTokenName = "refresh_token";
    private const string IdTokenName = "id_token";
    private const string ExpiresAtName = "expires_at";

    // Access tokens are renewed this long before they expire so requests never run on an expired token.
    private static readonly TimeSpan TokenRefreshLeeway = TimeSpan.FromSeconds(60);

    internal static void AddSecurityConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = IdentityProviderSelector.Resolve(configuration);
        IdentityProviderSelector.ValidateSettings(configuration, provider);
        var schemeName = IdentityProviderSelector.SchemeName(provider);
        var issuer = IdentityProviderSelector.ClaimsIssuer(provider);
        var authority = IdentityProviderSelector.Authority(configuration, provider);
        var audience = configuration[$"{IdentityProviderSelector.SettingsPath(provider)}:Audience"];

        services.TryAddSingleton<OidcTokenRefresher>();

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
                    var tokenOutcome = await RefreshTokensIfExpiringAsync(context, provider, schemeName);
                    if (tokenOutcome == TokenRefreshOutcome.Failed)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        return;
                    }

                    // Permissions are re-read from the IdP right after a token refresh, bypassing the snapshot cache.
                    var refresh = await RefreshPermissionClaimsAsync(
                        context.HttpContext,
                        context.Principal,
                        issuer,
                        context.HttpContext.RequestAborted,
                        forceRefresh: tokenOutcome == TokenRefreshOutcome.Refreshed);
                    if (!refresh.Succeeded)
                    {
                        context.RejectPrincipal();
                        return;
                    }

                    if (tokenOutcome == TokenRefreshOutcome.Refreshed || refresh.ClaimsChanged)
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

    /// <summary>
    /// Renews the stored access token when it is close to expiry. Cookies issued before token storage existed
    /// have no <c>expires_at</c> and are left unchanged.
    /// </summary>
    private static async Task<TokenRefreshOutcome> RefreshTokensIfExpiringAsync(
        CookieValidatePrincipalContext context,
        IdentityProvider provider,
        string schemeName)
    {
        var properties = context.Properties;
        var expiresAtText = properties.GetTokenValue(ExpiresAtName);
        if (expiresAtText is null)
            return TokenRefreshOutcome.NotNeeded;

        if (!DateTimeOffset.TryParse(expiresAtText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt))
            return TokenRefreshOutcome.Failed;

        if (expiresAt - TokenRefreshLeeway > DateTimeOffset.UtcNow)
            return TokenRefreshOutcome.NotNeeded;

        var refreshToken = properties.GetTokenValue(RefreshTokenName);
        if (string.IsNullOrEmpty(refreshToken))
            return TokenRefreshOutcome.Failed;

        try
        {
            var services = context.HttpContext.RequestServices;
            var oidcOptions = services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(schemeName);
            var refresher = services.GetRequiredService<OidcTokenRefresher>();
            var result = await refresher.RefreshAsync(oidcOptions, provider, refreshToken, context.HttpContext.RequestAborted);
            if (!result.Succeeded || result.AccessToken is null || result.RefreshToken is null)
                return TokenRefreshOutcome.Failed;

            properties.UpdateTokenValue(AccessTokenName, result.AccessToken);
            properties.UpdateTokenValue(RefreshTokenName, result.RefreshToken);
            if (result.IdToken is not null)
                properties.UpdateTokenValue(IdTokenName, result.IdToken);
            properties.UpdateTokenValue(ExpiresAtName, result.ExpiresAt.ToString("o", CultureInfo.InvariantCulture));

            return TokenRefreshOutcome.Refreshed;
        }
        catch (Exception)
        {
            return TokenRefreshOutcome.Failed;
        }
    }

    private static async Task<PermissionRefreshResult> RefreshPermissionClaimsAsync(
        HttpContext httpContext,
        ClaimsPrincipal? principal,
        string issuer,
        CancellationToken cancellationToken,
        bool forceRefresh = false)
    {
        if (principal is null)
            return default;

        var identity = principal.Identities.FirstOrDefault(candidate => candidate.IsAuthenticated);
        if (identity is null)
            return default;

        try
        {
            var permissionService = httpContext.RequestServices.GetRequiredService<IPermissionSnapshotService>();
            var permissions = forceRefresh
                ? await permissionService.RefreshForSubjectAsync(principal, cancellationToken)
                : await permissionService.GetCurrentPermissionsAsync(principal, cancellationToken);
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

        // Tokens are stored in the encrypted auth cookie so the refresh flow can renew them.
        options.SaveTokens = true;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("offline_access");

        options.CallbackPath = new PathString("/callback");

        options.ClaimsIssuer = issuer;

        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = async context =>
            {
                // Interactive sign-in bypasses the snapshot cache so a new session starts from current grants.
                var refresh = await RefreshPermissionClaimsAsync(
                    context.HttpContext,
                    context.Principal,
                    issuer,
                    context.HttpContext.RequestAborted,
                    forceRefresh: true);
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

                    logoutUri += $"{(logoutUri.Contains('?') ? '&' : '?')}{returnParameter}={Uri.EscapeDataString(postLogoutUri)}";
                }

                context.Response.Redirect(logoutUri);
                context.HandleResponse();

                return Task.CompletedTask;
            },

            OnRedirectToIdentityProvider = context =>
            {
                if (IdentityProviderSelector.SendsAudienceOnAuthorize(provider))
                    context.ProtocolMessage.SetParameter(
                        "audience",
                        configuration[$"{IdentityProviderSelector.SettingsPath(provider)}:Audience"]);
                return Task.CompletedTask;
            }
        };
    }

    private readonly record struct PermissionRefreshResult(bool Succeeded, bool ClaimsChanged);

    private enum TokenRefreshOutcome
    {
        NotNeeded,
        Refreshed,
        Failed,
    }
}
