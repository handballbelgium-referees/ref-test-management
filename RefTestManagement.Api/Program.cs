using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Auth0;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Extensions;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Api;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Infrastructure.Jobs;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Api.Configurations;
using Handball.Belgium.RefTestManagement.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Handball.Belgium.RefTestManagement.Api.Extensions;
using QuestPDF.Infrastructure;
using Handball.Belgium.RefTestManagement.Api.Graphql;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using System.Threading.RateLimiting;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;

// Configure QuestPDF license
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var configuration = builder.Configuration;

services.AddSecurityConfiguration(configuration);
services.AddTaskBasedAuthorization();
services.AddHttpContextAccessor();
services.AddDataProtection().SetApplicationName("RefTestManagement");
services.AddSingleton<TimeProvider>(TimeProvider.System);
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

var auditLogOptions = services.AddAuditLogging(opts =>
{
    var section = configuration.GetSection("AuditLogConfiguration");
    opts.EnableCleanup = section.GetValue("EnableCleanup", true);
    opts.CleanupIntervalHours = section.GetValue("CleanupIntervalHours", 24);
    opts.RetentionDays = section.GetValue("RetentionDays", 90);

    opts.ExcludeEntity<Job>();
    opts.ExcludeProperty("Token");
    opts.ExcludeProperty("KeyHash");
    opts.ExcludeProperty("ProtectedDeliveryKey");
    opts.ExcludeProperty("ProtectedInvitationToken");
    opts.RegisterEntityResolver("RefTestTitle", (id, ctx) =>
        ctx.Set<RefTestTitle>().Find(id)?.Value);
});
auditLogOptions.Validate();

services.AddDatabaseProvider(configuration);

// Add services
services.AddValidatedConfiguration<EmailConfiguration>(configuration, "EmailConfiguration");
var languageConfig = configuration.GetSection("LanguageConfiguration").Get<LanguageConfiguration>() ??
                     LanguageConfiguration.CreateDefault();
if (languageConfig.EnabledLanguages.Length == 0)
    languageConfig = LanguageConfiguration.CreateDefault();
languageConfig.Validate();

if (string.IsNullOrEmpty(languageConfig.DefaultPhraseLanguage) ||
    !languageConfig.EnabledLanguages.Contains(languageConfig.DefaultPhraseLanguage))
    languageConfig.SetDefaultPhraseLanguage(languageConfig.EnabledLanguages[0]);

services.AddSingleton(languageConfig);

services.AddValidatedConfiguration<ScoreConfiguration>(configuration, "ScoreConfiguration");
services.AddValidatedConfiguration<ReportConfiguration>(configuration, "ReportConfiguration");
services.AddValidatedConfiguration<PrivacyConfiguration>(configuration, "PrivacyConfiguration", c => c.Validate());

var containerAppName = configuration["CONTAINER_APP_NAME"];
var redisConfig = services.AddValidatedConfiguration<RedisConfiguration>(
    configuration, "RedisConfiguration", c => c.Validate(containerAppName));
var useRedis = redisConfig.IsConfigured;

var privacyChallengeConfig = services.AddValidatedConfiguration<PrivacyChallengeConfiguration>(
    configuration, "PrivacyChallengeConfiguration");
PrivacyChallengeRateLimiter.ValidateRateLimitConfiguration(privacyChallengeConfig, useRedis, containerAppName);
if (useRedis)
{
    var endpoint = new Uri(redisConfig.Endpoint!);
    var credentials = endpoint.UserInfo.Split(':', 2);
    var redisOptions = new ConfigurationOptions
    {
        AbortOnConnectFail = false,
        Ssl = true,
        User = Uri.UnescapeDataString(credentials[0]),
        Password = Uri.UnescapeDataString(credentials.Length > 1 ? credentials[1] : string.Empty),
        ConnectTimeout = 1000,
        AsyncTimeout = 1000,
        SyncTimeout = 1000
    };
    redisOptions.EndPoints.Add(endpoint.Host, endpoint.Port);
    services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));
    // Registered before AddInfrastructureServices, whose in-memory lock only fills the gap.
    services.AddSingleton<IRefTestSessionService, RedisRefTestSessionService>();
}
services.AddSingleton<IPrivacyChallengeRateLimiter, PrivacyChallengeRateLimiter>();

services.AddValidatedConfiguration<RefTestExpirationConfiguration>(configuration, "RefTestExpirationConfiguration");
services.AddValidatedConfiguration<BackgroundJobConfiguration>(configuration, "BackgroundJobConfiguration");
var graphQlLimitsConfig = services.AddValidatedConfiguration<GraphQlLimitsConfiguration>(
    configuration, "GraphQlLimitsConfiguration");
var forwardedHeadersConfig = services.AddValidatedConfiguration<ForwardedHeadersConfiguration>(
    configuration, "ForwardedHeadersConfiguration", ConfigurableHeaderClientIpResolver.ValidateConfiguration);
services.Configure<ForwardedHeadersConfiguration>(configuration.GetSection("ForwardedHeadersConfiguration"));
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

services.AddInfrastructureServices();

services.AddAuth0ManagementServices(configuration);
services.AddMemoryCache();
services.AddSingleton<IPermissionSnapshotService, PermissionSnapshotService>();

// Add background services
services.AddHostedService<PermissionSyncService>();
services.AddHostedService<RefTestExpirationService>();
services.AddHostedService<PrivacyRetentionService>();
services.AddHostedService<PersonalDataExportRequestCleanupService>();
services.AddHostedService<PrivacyWithdrawalCleanupService>();
services.AddHostedService<BackgroundJobService>();

services.AddJobHandlers();
services.AddKeyedScoped<IJobHandler, ApprovalNotificationEmailJobHandler>(JobType.ApprovalNotificationEmail);
if (auditLogOptions.EnableCleanup)
{
    services.AddHostedService<AuditLogCleanupService>();
}

services.AddIhfRulesQuestionsHttpClient(configuration["RulesQuestions:Url"]);

// With Redis configured the API may run as several replicas, so subscription events must reach
// subscribers connected to any of them; without it, in-process delivery is enough.
var graphQlBuilder = services.AddGraphQLServer();
if (useRedis)
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

await app.MigrateRefTestManagementDatabase();

// Preserve the transport peer; forwarded-header middleware replaces RemoteIpAddress.
app.Use((context, next) =>
{
    context.Items[ConfigurableHeaderClientIpResolver.TransportPeerAddressItemKey] =
        context.Connection.RemoteIpAddress;
    return next(context);
});

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