using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using QuizManagement.Api;
using QuizManagement.Application.Services;
using QuizManagement.Infrastructure;
using QuizManagement.Infrastructure.Services;
using StrawberryShake;

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
var languageConfig = configuration.GetSection("LanguageConfiguration").Get<LanguageConfiguration>()
                     ?? new LanguageConfiguration();
services.AddSingleton(languageConfig);
services.AddScoped<IEmailService, EmailService>();
services.AddScoped<IIhfRulesQuestionsService, IhfRulesQuestionsService>();

// Add IHF Rules Questions GraphQL client
services.AddIHFRulesQuestionsClient(ExecutionStrategy.CacheFirst)
    .ConfigureHttpClient(c =>
    {
        c.BaseAddress = new Uri(configuration["RulesQuestions:Url"]!);
        c.DefaultRequestHeaders.Add("Accept-Language", languageConfig.DefaultPhraseLanguage);
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