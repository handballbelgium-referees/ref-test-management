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

services.AddDbContext<QuizManagementContext>(options =>
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
services.AddScoped<IEmailService, EmailService>();
services.AddScoped<IIhfRulesQuestionsService, IhfRulesQuestionsService>();

// Add IHF Rules Questions GraphQL client
services.AddIHFRulesQuestionsClient(ExecutionStrategy.CacheFirst)
    .ConfigureHttpClient(c =>
    {
        c.BaseAddress = new Uri(configuration["RulesQuestions:Url"]!);
        c.DefaultRequestHeaders.Add("Accept-Language", "en");
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
    .ModifyCostOptions(o => { o.EnforceCostLimits = false; })
    .AddFiltering()
    .AddSorting()
    .AddDefaultNodeIdSerializer(useUrlSafeBase64: true)
    .AddGlobalObjectIdentification(true)
    .AddAuthorization();

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