using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using QuizManagement.Api;
using QuizManagement.Application.Services;
using QuizManagement.Infrastructure;
using QuizManagement.Infrastructure.Services;
using StrawberryShake;

// Configure QuestPDF license
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var configuration = builder.Configuration;

services.AddSecurityConfiguration(configuration);
services.AddControllersWithViews();

services.AddDbContextFactory<QuizManagementContext>(options =>
{
    options.UseSqlServer(configuration.GetConnectionString("QuizManagement"),
        x => x
            .EnableRetryOnFailure()
            .MigrationsAssembly(typeof(QuizManagementContext).Assembly.GetName().Name));
#if DEBUG
    options.EnableSensitiveDataLogging();
#endif
});

// Add services
var emailConfig = configuration.GetSection("EmailConfiguration").Get<EmailConfiguration>()
                  ?? new EmailConfiguration();
services.AddSingleton(emailConfig);
services.Configure<LanguageConfiguration>(configuration.GetSection("LanguageConfiguration"));
services.PostConfigure<LanguageConfiguration>(options =>
{
    // Fallback to all languages if none specified
    if (options.EnabledLanguages.Length == 0)
        options.EnabledLanguages = ["en", "nl", "fr", "de"];
});
services.AddScoped<IEmailService, EmailService>();
services.AddScoped<IQuizResultsPdfService, QuizResultsPdfService>();
services.AddScoped<IIhfRulesQuestionsService, IhfRulesQuestionsService>();

// Add IHF Rules Questions GraphQL client
services.AddIHFRulesQuestionsClient(ExecutionStrategy.CacheFirst)
    .ConfigureHttpClient((sp, c) =>
    {
        c.BaseAddress = new Uri(configuration["RulesQuestions:Url"]!);
        var langConfig = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<LanguageConfiguration>>();
        c.DefaultRequestHeaders.Add("Accept-Language", langConfig.CurrentValue.DefaultPhraseLanguage);
    });

services.AddGraphQLServer()
    .AddQueryType()
    .AddMutationType()
    .AddApiTypes()
    .AddQueryConventions()
    .AddMutationConventions()
    .ModifyPagingOptions(options =>
    {
        options.DefaultPageSize = 20;
        options.IncludeTotalCount = true;
        options.MaxPageSize = 100;
        options.AllowBackwardPagination = true;
    })
    .ModifyCostOptions(o => o.EnforceCostLimits = false)
    .RegisterDbContextFactory<QuizManagementContext>()
    .AddFiltering()
    .AddSorting()
    .AddDefaultNodeIdSerializer(useUrlSafeBase64: true)
    .AddGlobalObjectIdentification(true)
    .AddAuthorization()
    .AddHttpRequestInterceptor(async (ctx, _, _, _) =>
    {
        var result = await ctx.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (result is { Succeeded: true, Principal: not null })
            ctx.User = result.Principal;

        await Task.CompletedTask;
    });

var app = builder.Build();

await app.MigrateQuizManagementDatabase();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();
app.UseStaticFiles();
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