using Handball.Belgium.RefTestManagement.Auth0;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Api.Configurations;
using Handball.Belgium.RefTestManagement.Api;
using Handball.Belgium.RefTestManagement.Security;
using Handball.Belgium.RefTestManagement.Hosting;
using Handball.Belgium.RefTestManagement.Hosting.Azure;
using Handball.Belgium.RefTestManagement.Hosting.Cloudflare;
using Handball.Belgium.RefTestManagement.Hosting.Configurations;
using Handball.Belgium.RefTestManagement.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Handball.Belgium.RefTestManagement.Api.Graphql;
using System.Threading.RateLimiting;
using System.Net;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var configuration = builder.Configuration;
var hostingMode = HostingModeConfiguration.GetApiMode(configuration);
var isCloudflareProvider = CloudflareRequestAssertionMiddleware.IsCloudflareProvider(configuration);
CloudflareRequestAssertionMiddleware.ValidateConfiguration(configuration);

services.AddSecurityConfiguration(configuration);
services.AddTaskBasedAuthorization();
services.AddControllersWithViews();
services.Configure<HstsOptions>(options => options.MaxAge = TimeSpan.FromDays(365));

// Add CORS for development (allows WebSocket connections from Angular dev server)
services.AddCors(options =>
{
    options.AddPolicy("DevelopmentCors", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var dataProtectionKeyProtection =
    CloudflareDataProtectionConfiguration.CreateKeyProtection(configuration) ??
    AzureDataProtectionConfiguration.CreateKeyProtection(configuration, builder.Environment);
var stateConfiguration = services.AddRefTestManagementHosting(
    configuration,
    hostingMode,
    dataProtectionKeyProtection);
await AzureRedisConfiguration.ConfigureRedisConnectionAsync(services, configuration);
services.AddSingleton<IPrivacyChallengeRateLimiter, PrivacyChallengeRateLimiter>();

var graphQlLimitsConfig = services.AddValidatedConfiguration<GraphQlLimitsConfiguration>(
    configuration, "GraphQlLimitsConfiguration");
Action<ForwardedHeadersConfiguration>? forwardedHeadersValidator = isCloudflareProvider
    ? null
    : ConfigurableHeaderClientIpResolver.ValidateConfiguration;
var forwardedHeadersConfig = services.AddValidatedConfiguration<ForwardedHeadersConfiguration>(
    configuration, "ForwardedHeadersConfiguration", forwardedHeadersValidator);
services.Configure<ForwardedHeadersConfiguration>(configuration.GetSection("ForwardedHeadersConfiguration"));
if (isCloudflareProvider)
    services.AddSingleton<IClientIpResolver, CloudflareClientIpResolver>();
else
    services.AddSingleton<IClientIpResolver, ConfigurableHeaderClientIpResolver>();

const string graphQlRateLimiterPolicy = "graphql";

if (graphQlLimitsConfig.EnableRateLimiting)
{
    services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Partitioned by client address: the participant flow is anonymous, so there is no user
        // to key on, and a single shared bucket would let one abusive client lock out everyone.
        options.AddPolicy(graphQlRateLimiterPolicy, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.RequestServices.GetRequiredService<IClientIpResolver>().Resolve(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = graphQlLimitsConfig.RateLimitPermitLimit,
                    Window = TimeSpan.FromSeconds(graphQlLimitsConfig.RateLimitWindowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = graphQlLimitsConfig.RateLimitQueueLimit
                }));
    });
}

services.AddSingleton<IPermissionSnapshotService, PermissionSnapshotService>();

services.AddIhfRulesQuestionsHttpClient(configuration["RulesQuestions:Url"]);

// With Redis configured the API may run as several replicas, so subscription events must reach
// subscribers connected to any of them; without it, in-process delivery is enough.
var graphQlBuilder = services.AddGraphQLServer();
if (stateConfiguration.Mode == StateMode.Shared)
    graphQlBuilder.AddRedisSubscriptions(sp => sp.GetRequiredService<IConnectionMultiplexer>());
else
    graphQlBuilder.AddInMemorySubscriptions();

graphQlBuilder
    .AddQueryType()
    .AddMutationType()
    .AddSubscriptionType()
    .AddApiTypes()
    .AddQueryConventions()
    .AddMutationConventions()
    .AddApplicationService<IHttpContextAccessor>()
    .AddApplicationService<ILogger<UnhandledExceptionLoggingErrorFilter>>()
    .AddApplicationService<ILogger<ConcurrencyErrorFilter>>()
    // Order matters: the concurrency filter handles and unwraps its exception, so the logging
    // filter below no longer sees contention as an unhandled fault.
    .AddErrorFilter<ConcurrencyErrorFilter>()
    .AddErrorFilter<UnhandledExceptionLoggingErrorFilter>()
    .ModifyPagingOptions(options =>
    {
        options.DefaultPageSize = 20;
        options.IncludeTotalCount = true;
        options.MaxPageSize = 100;
        options.AllowBackwardPagination = true;
    })
    .ModifyCostOptions(o =>
    {
        o.EnforceCostLimits = graphQlLimitsConfig.EnforceCostLimits;
        o.MaxFieldCost = graphQlLimitsConfig.MaxFieldCost;
        o.MaxTypeCost = graphQlLimitsConfig.MaxTypeCost;
    })
    .RegisterDbContextFactory<RefTestManagementContext>()
    .AddProjections()
    .AddFiltering()
    .AddSorting()
    .AddCacheControl()
    .AddDefaultNodeIdSerializer(useUrlSafeBase64: true)
    .AddGlobalObjectIdentification(false)
    .AddAuthorization()
    .AddJsonTypeConverter()
    .AddHttpRequestInterceptor(async (ctx, _, _, _) =>
    {
        var result = await ctx.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (result is { Succeeded: true, Principal: not null })
            ctx.User = result.Principal;

        await Task.CompletedTask;
    });

var app = builder.Build();

var hasUnsafeForwardedHeadersRateLimitConfig =
    graphQlLimitsConfig.EnableRateLimiting &&
    !isCloudflareProvider &&
    forwardedHeadersConfig.KnownProxies.Length == 0 &&
    forwardedHeadersConfig.KnownNetworks.Length == 0;

if (hasUnsafeForwardedHeadersRateLimitConfig &&
    !app.Environment.IsDevelopment() &&
    !forwardedHeadersConfig.AllowUnsafeRateLimitingWithoutTrustedForwarders)
{
    throw new InvalidOperationException(
        "GraphQL rate limiting requires a trusted proxy address or network in non-development environments. Configure ForwardedHeadersConfiguration.KnownProxies or KnownNetworks for the actual ingress, disable rate limiting, or explicitly allow unsafe rate limiting. TrustedClientIpHeaders alone is not sufficient.");
}

if (hasUnsafeForwardedHeadersRateLimitConfig)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logger.LogWarning(
        "GraphQL rate limiting is enabled without trusted forwarded-header peers. This is only safe for direct/local access and should not be used behind a reverse proxy.");
}

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = forwardedHeadersConfig.ForwardLimit
};
forwardedHeadersOptions.KnownProxies.Clear();
forwardedHeadersOptions.KnownIPNetworks.Clear();

foreach (var value in forwardedHeadersConfig.KnownProxies)
{
    if (!IPAddress.TryParse(value, out var address))
        throw new InvalidOperationException($"Invalid forwarded-header proxy address: '{value}'.");

    forwardedHeadersOptions.KnownProxies.Add(address);
}

foreach (var value in forwardedHeadersConfig.KnownNetworks)
{
    var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
    if (parts.Length != 2 ||
        !IPAddress.TryParse(parts[0], out var address) ||
        !int.TryParse(parts[1], out var prefixLength) ||
        prefixLength < 0 ||
        prefixLength > address.GetAddressBytes().Length * 8)
        throw new InvalidOperationException($"Invalid forwarded-header network: '{value}'.");

    forwardedHeadersOptions.KnownIPNetworks.Add(new System.Net.IPNetwork(address, prefixLength));
}

// Preserve the transport peer; forwarded-header middleware replaces RemoteIpAddress.
app.Use((context, next) =>
{
    context.Items[ConfigurableHeaderClientIpResolver.TransportPeerAddressItemKey] =
        context.Connection.RemoteIpAddress;
    return next(context);
});

if (isCloudflareProvider)
    app.UseMiddleware<CloudflareRequestAssertionMiddleware>();
else
    app.UseForwardedHeaders(forwardedHeadersOptions);

// Security headers are enforced through response headers rather than meta tags. CSP stays
// response-only to avoid a second policy; browsers ignore X-Content-Type-Options in markup, and
// meta Referrer-Policy applies only after the parser reaches it.
//
// no-referrer also avoids leaking URL data to other origins.
// Keep the SPA index byte-for-byte stable because Angular's service worker verifies its precache hash.

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "worker-src 'self' blob:; " +
        "style-src 'self'; " +
        "style-src-elem 'self' 'unsafe-inline'; " +
        "style-src-attr 'unsafe-inline'; " +
        "connect-src 'self'; " +
        "img-src 'self' data:; " +
        "font-src 'self' data:; " +
        "object-src 'none'; " +
        "frame-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self';";

    headers["Referrer-Policy"] = "no-referrer";
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

// Enable CORS for development
if (app.Environment.IsDevelopment())
{
    app.UseCors("DevelopmentCors");
}

app.UseRouting();

if (graphQlLimitsConfig.EnableRateLimiting)
{
    app.UseRateLimiter();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    "default",
    "{controller}/{action=Index}/{id?}"
);

var graphQlEndpoint = app.MapGraphQL();

if (graphQlLimitsConfig.EnableRateLimiting)
{
    graphQlEndpoint.RequireRateLimiting(graphQlRateLimiterPolicy);
}

app.MapFallbackToFile("index.html");

app.RunWithGraphQLCommands(args);