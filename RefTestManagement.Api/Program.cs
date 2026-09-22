using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Auth0;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Api;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Handball.Belgium.RefTestManagement.Api.Extensions;
using QuestPDF.Infrastructure;
using StrawberryShake;
using Handball.Belgium.RefTestManagement.Api.Graphql;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using System.Threading.RateLimiting;
using System.Net;

// Configure QuestPDF license
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var configuration = builder.Configuration;

services.AddSecurityConfiguration(configuration);
services.AddTaskBasedAuthorization();
services.AddHttpContextAccessor();
services.AddControllersWithViews();

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
    opts.RegisterEntityResolver("RefTestTitle", (id, ctx) =>
        ctx.Set<RefTestTitle>().Find(id)?.Value);
});

services.AddDatabaseProvider(configuration);

// Add services
var emailConfig = configuration.GetSection("EmailConfiguration").Get<EmailConfiguration>()
                  ?? new EmailConfiguration();
services.AddSingleton(emailConfig);
var languageConfig = configuration.GetSection("LanguageConfiguration").Get<LanguageConfiguration>() ??
                     LanguageConfiguration.CreateDefault();
if (languageConfig.EnabledLanguages.Length == 0)
    languageConfig = LanguageConfiguration.CreateDefault();

if (string.IsNullOrEmpty(languageConfig.DefaultPhraseLanguage) ||
    !languageConfig.EnabledLanguages.Contains(languageConfig.DefaultPhraseLanguage))
    languageConfig.SetDefaultPhraseLanguage(languageConfig.EnabledLanguages[0]);

services.AddSingleton(languageConfig);

var scoreConfig = configuration.GetSection("ScoreConfiguration").Get<ScoreConfiguration>()
                  ?? new ScoreConfiguration();
services.AddSingleton(scoreConfig);

var reportConfig = configuration.GetSection("ReportConfiguration").Get<ReportConfiguration>()
                   ?? new ReportConfiguration();
services.AddSingleton(reportConfig);

var privacyConfig = configuration.GetSection("PrivacyConfiguration").Get<PrivacyConfiguration>()
                    ?? new PrivacyConfiguration();
services.AddSingleton(privacyConfig);

var refTestExpirationConfig = configuration.GetSection("RefTestExpirationConfiguration")
                                  .Get<RefTestExpirationConfiguration>()
                              ?? new RefTestExpirationConfiguration();
services.AddSingleton(refTestExpirationConfig);

var backgroundJobConfig = configuration.GetSection("BackgroundJobConfiguration")
                              .Get<BackgroundJobConfiguration>()
                          ?? new BackgroundJobConfiguration();
services.AddSingleton(backgroundJobConfig);

var graphQlLimitsConfig = configuration.GetSection("GraphQlLimitsConfiguration")
                              .Get<GraphQlLimitsConfiguration>()
                          ?? new GraphQlLimitsConfiguration();
services.AddSingleton(graphQlLimitsConfig);

var forwardedHeadersConfig = configuration.GetSection("ForwardedHeadersConfiguration")
                                  .Get<ForwardedHeadersConfiguration>()
                              ?? new ForwardedHeadersConfiguration();
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

// Outbound HTTP is guarded in three places below. The shape is deliberately the same each time:
// an explicit timeout, because HttpClient's 100-second default is far longer than any of these
// calls should take, and the standard resilience handler, which adds a per-attempt timeout, a
// small retry with backoff and a circuit breaker. Every call made through these clients is a read
// or an idempotent write, so retrying cannot duplicate anything.
services.AddHttpClient<ILogoService, LogoService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15);
    })
    .AddStandardResilienceHandler();
services.AddSingleton<ITranslationService, TranslationService>();
services.AddHttpClient<IEmailService, EmailService>((sp, client) =>
{
    var emailCfg = sp.GetRequiredService<EmailConfiguration>();
    client.DefaultRequestHeaders.Add("api-key", emailCfg.BrevoApiKey);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.BaseAddress = new Uri(emailCfg.BrevoApiUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});
// Deliberately no resilience handler here. Sending an email is the one outbound call that is not
// idempotent: a retry after a response that was sent but never received delivers the message
// twice. The job queue already owns retry for this path, and it retries the whole job rather than
// the HTTP call, so it can tell the difference.
services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
services.AddScoped<IRefTestResultsPdfService, RefTestResultsPdfService>();
services.AddScoped<IRefTestReportService, RefTestReportService>();
services.AddScoped<IIhfRulesQuestionsService, IhfRulesQuestionsService>();
services.AddScoped<IJobEnqueueService, JobEnqueueService>();
services.AddScoped<IRefTestPrivacyErasureService, RefTestPrivacyErasureService>();
services.AddScoped<IRefTestSubscriptionService, RefTestSubscriptionService>();
services.AddSingleton<IRefTestSessionService, RefTestSessionService>();

services.AddAuth0ManagementServices(configuration);

// Add background services
services.AddHostedService<PermissionSyncService>();
services.AddHostedService<RefTestExpirationService>();
services.AddHostedService<PrivacyRetentionService>();
services.AddHostedService<BackgroundJobService>();

// Job handlers, keyed by the job type BackgroundJobService dispatches on. A job type with no
// handler registered here fails as "Unknown job type" rather than silently doing nothing.
services.AddKeyedScoped<IJobHandler, InvitationEmailJobHandler>(JobType.InvitationEmail);
services.AddKeyedScoped<IJobHandler, ResultEmailJobHandler>(JobType.ResultEmail);
services.AddKeyedScoped<IJobHandler, ReportEmailJobHandler>(JobType.ReportEmail);
services.AddKeyedScoped<IJobHandler, RefTestExpirationJobHandler>(JobType.RefTestExpiration);
services.AddKeyedScoped<IJobHandler, ApprovalNotificationEmailJobHandler>(JobType.ApprovalNotificationEmail);
services.AddKeyedScoped<IJobHandler, ApprovalDecisionEmailJobHandler>(JobType.ApprovalDecisionEmail);
if (auditLogOptions.EnableCleanup)
{
    services.AddHostedService<AuditLogCleanupService>();
}

// Add IHF Rules Questions GraphQL client
// This one sits on the test-creation path, so an upstream hang without a timeout fails test
// creation after a two-minute wait with nothing to show for it. The client only issues GraphQL
// queries, never mutations, so retrying is safe.
services.AddIHFRulesQuestionsClient(ExecutionStrategy.CacheFirst)
    .ConfigureHttpClient(
        (sp, c) =>
        {
            c.BaseAddress = new Uri(configuration["RulesQuestions:Url"]!);
            var langConfig = sp.GetRequiredService<LanguageConfiguration>();
            c.DefaultRequestHeaders.Add("Accept-Language", langConfig.DefaultPhraseLanguage);
            c.Timeout = TimeSpan.FromSeconds(30);
        },
        // StrawberryShake wraps the registration in its own builder, so the resilience handler has
        // to be added through this hook rather than chained off the call.
        clientBuilder => clientBuilder.AddStandardResilienceHandler());

services.AddGraphQLServer()
    .AddQueryType()
    .AddMutationType()
    .AddSubscriptionType()
    .AddApiTypes()
    .AddQueryConventions()
    .AddMutationConventions()
    .AddInMemorySubscriptions()
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
    forwardedHeadersConfig.TrustedClientIpHeaders.Length == 0 &&
    forwardedHeadersConfig.KnownProxies.Length == 0 &&
    forwardedHeadersConfig.KnownNetworks.Length == 0;

if (hasUnsafeForwardedHeadersRateLimitConfig &&
    !app.Environment.IsDevelopment() &&
    !forwardedHeadersConfig.AllowUnsafeRateLimitingWithoutTrustedForwarders)
{
    throw new InvalidOperationException(
        "GraphQL rate limiting requires at least one trusted client IP source in non-development environments. Configure ForwardedHeadersConfiguration.TrustedClientIpHeaders, KnownProxies, or KnownNetworks, or disable rate limiting.");
}

if (hasUnsafeForwardedHeadersRateLimitConfig)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logger.LogWarning(
        "GraphQL rate limiting is enabled without trusted client IP sources. This is only safe for direct/local access and should not be used behind a reverse proxy.");
}

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = forwardedHeadersConfig.ForwardLimit
};
if (forwardedHeadersConfig.ForwardLimit < 1)
    throw new InvalidOperationException("ForwardedHeadersConfiguration:ForwardLimit must be at least 1.");

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

app.UseForwardedHeaders(forwardedHeadersOptions);

// Sent as real response headers rather than <meta http-equiv> tags. Browsers ignore
// X-Frame-Options and X-Content-Type-Options when they appear in markup, so the tags in
// index.html look like protection without providing any; and a meta Referrer-Policy only takes
// effect once the parser reaches it, which is too late for anything the document requests first.
//
// no-referrer matters more here than it usually would: a participant's invitation token travels
// in the URL path, so any weaker policy puts a working credential into another site's logs.
const string contentSecurityPolicy =
    "default-src 'self' https:; " +
    "script-src 'self' 'unsafe-inline'; " +
    "worker-src 'self' blob:; " +
    "style-src 'self' 'unsafe-inline'; " +
    "connect-src 'self' wss:; " +
    "img-src 'self' data: https:; " +
    "font-src 'self' data:; " +
    "base-uri 'self'; " +
    "form-action 'self';";

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["Content-Security-Policy"] = contentSecurityPolicy;
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