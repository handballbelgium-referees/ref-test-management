using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Api;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using StrawberryShake;

// Configure QuestPDF license
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var configuration = builder.Configuration;

services.AddSecurityConfiguration(configuration);
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

services.AddDbContextFactory<RefTestManagementContext>(options =>
{
    options.UseSqlServer(configuration.GetConnectionString("RefTestManagement"),
        x => x
            .EnableRetryOnFailure()
            .MigrationsAssembly(typeof(RefTestManagementContext).Assembly.GetName().Name));
#if DEBUG
    options.EnableSensitiveDataLogging();
#endif
});

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

var refTestExpirationConfig = configuration.GetSection("RefTestExpirationConfiguration")
                                  .Get<RefTestExpirationConfiguration>()
                              ?? new RefTestExpirationConfiguration();
services.AddSingleton(refTestExpirationConfig);

var backgroundJobConfig = configuration.GetSection("BackgroundJobConfiguration")
                              .Get<BackgroundJobConfiguration>()
                          ?? new BackgroundJobConfiguration();
services.AddSingleton(backgroundJobConfig);

services.AddHttpClient<ILogoService, LogoService>();
services.AddSingleton<ITranslationService, TranslationService>();
services.AddHttpClient<IEmailService, EmailService>((sp, client) =>
{
    var emailCfg = sp.GetRequiredService<EmailConfiguration>();
    client.DefaultRequestHeaders.Add("api-key", emailCfg.BrevoApiKey);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.BaseAddress = new Uri(emailCfg.BrevoApiUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});
services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
services.AddScoped<IRefTestResultsPdfService, RefTestResultsPdfService>();
services.AddScoped<IRefTestReportService, RefTestReportService>();
services.AddScoped<IIhfRulesQuestionsService, IhfRulesQuestionsService>();
services.AddScoped<IJobEnqueueService, JobEnqueueService>();
services.AddScoped<IRefTestSubscriptionService, RefTestSubscriptionService>();

// Add background services
services.AddHostedService<RefTestExpirationService>();
services.AddHostedService<BackgroundJobService>();

// Add IHF Rules Questions GraphQL client
services.AddIHFRulesQuestionsClient(ExecutionStrategy.CacheFirst)
    .ConfigureHttpClient((sp, c) =>
    {
        c.BaseAddress = new Uri(configuration["RulesQuestions:Url"]!);
        var langConfig = sp.GetRequiredService<LanguageConfiguration>();
        c.DefaultRequestHeaders.Add("Accept-Language", langConfig.DefaultPhraseLanguage);
    });

services.AddGraphQLServer()
    .AddQueryType()
    .AddMutationType()
    .AddSubscriptionType()
    .AddApiTypes()
    .AddQueryConventions()
    .AddMutationConventions()
    .AddInMemorySubscriptions()
    .ModifyPagingOptions(options =>
    {
        options.DefaultPageSize = 20;
        options.IncludeTotalCount = true;
        options.MaxPageSize = 100;
        options.AllowBackwardPagination = true;
    })
    .ModifyCostOptions(o => o.EnforceCostLimits = false)
    .RegisterDbContextFactory<RefTestManagementContext>()
    .AddProjections()
    .AddFiltering()
    .AddSorting()
    .AddCacheControl()
    .AddDefaultNodeIdSerializer(useUrlSafeBase64: true)
    .AddGlobalObjectIdentification(true)
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

await app.MigrateRefTestManagementDatabase();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();
app.UseStaticFiles();

// Enable CORS for development
if (app.Environment.IsDevelopment())
{
    app.UseCors("DevelopmentCors");
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    "default",
    "{controller}/{action=Index}/{id?}"
);

app.MapGraphQL();
app.MapFallbackToFile("index.html");

app.RunWithGraphQLCommands(args);