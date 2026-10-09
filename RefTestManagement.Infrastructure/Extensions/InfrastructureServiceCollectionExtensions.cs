using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Infrastructure implementations of the Application ports. Expects the
    /// validated configuration singletons (for example <see cref="EmailConfiguration"/>) and the
    /// database to be registered by the host.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Outbound HTTP keeps the same shape everywhere: an explicit timeout, because HttpClient's
        // 100-second default is far longer than any of these calls should take, and the standard
        // resilience handler, which adds a per-attempt timeout, a small retry with backoff and a
        // circuit breaker. The logo download is a read, so retrying cannot duplicate anything.
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
        services.AddScoped<IPersonalDataExportPdfService, PersonalDataExportPdfService>();
        services.AddScoped<IRefTestReportService, RefTestReportService>();
        services.AddScoped<IJobEnqueueService, JobEnqueueService>();
        services.AddScoped<IRefTestPrivacyErasureService, RefTestPrivacyErasureService>();
        services.AddScoped<IRefTestSubscriptionService, RefTestSubscriptionService>();
        // ponytail: process-local session lock; R1-ARCH WP9 replaces it with a shared one for
        // multi-replica deployments.
        services.AddSingleton<IRefTestSessionService, RefTestSessionService>();

        return services;
    }
}
