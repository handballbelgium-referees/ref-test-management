using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Handball.Belgium.RefTestManagement.Api;

public static class SecurityStartup
{
    internal static void AddSecurityConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
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
            })
            .AddOpenIdConnect("Auth0", options => ConfigureOpenIdConnect(options, configuration))
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = $"https://{configuration["Auth0:Domain"]}";
                options.Audience = configuration["Auth0:Audience"];
            });
    }

    private static void ConfigureOpenIdConnect(OpenIdConnectOptions options, IConfiguration configuration)
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
        options.Scope.Add("offline_access");

        options.CallbackPath = new PathString("/callback");

        options.ClaimsIssuer = "Auth0";

        options.SaveTokens = true;

        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = context =>
            {
                // Copy the Auth0 'permissions' claim from the access token into the cookie identity.
                // JwtBearer authentication already has permissions in the token, but for cookie-based
                // sessions (browser login via OIDC) we need to extract them from the access token.
                var accessToken = context.TokenEndpointResponse?.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                    return Task.CompletedTask;

                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(accessToken))
                    return Task.CompletedTask;

                var jwt = handler.ReadJwtToken(accessToken);
                var permissionClaims = jwt.Claims
                    .Where(c => c.Type == "permissions")
                    .ToList();

                if (permissionClaims.Count > 0)
                    (context.Principal?.Identity as ClaimsIdentity)?.AddClaims(permissionClaims);

                return Task.CompletedTask;
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
                        var request = context.Request;
                        postLogoutUri = request.Scheme + "://" + request.Host + request.PathBase + postLogoutUri;
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
}