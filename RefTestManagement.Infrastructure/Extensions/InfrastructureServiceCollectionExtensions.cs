using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure.IhfRules;
using Handball.Belgium.RefTestManagement.Infrastructure.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure.Persistence;
using Handball.Belgium.RefTestManagement.Infrastructure.Privacy;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StrawberryShake;

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
        services.AddScoped<JobExecutionContext>();
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
        services.AddScoped<IIhfRulesQuestionsService, IhfRulesQuestionsService>();
        services.AddScoped<IRefTestUnitOfWork, EfRefTestUnitOfWork>();

        // Privacy challenges and participant credentials. Token protection is a singleton because
        // Data Protection keys are process-wide; the request services are scoped to the DbContext.
        services.AddSingleton<IPersonalDataExportKeyProtection, PersonalDataExportKeyProtection>();
        services.AddSingleton<IRefTestInvitationTokenProtection, RefTestInvitationTokenProtection>();
        services.AddSingleton<IRefTestSessionTokenService, RefTestSessionTokenService>();
        services.AddScoped<IPersonalDataExportRequestService, PersonalDataExportRequestService>();
        services.AddScoped<IPrivacyWithdrawalRequestService, PrivacyWithdrawalRequestService>();
        // ponytail: process-local session lock; R1-ARCH WP9 replaces it with a shared one for
        // multi-replica deployments.
        // TryAdd: a multi-replica host registers its distributed lock first.
        services.TryAddSingleton<IRefTestSessionService, RefTestSessionService>();

        return services;
    }

    /// <summary>
    /// Registers the Infrastructure background job handlers, keyed by the job type the worker
    /// dispatches on. A job type with no handler registered fails as "Unknown job type" rather than
    /// silently doing nothing. The approval notification handler needs Auth0, so the host registers it.
    /// </summary>
    public static IServiceCollection AddJobHandlers(this IServiceCollection services)
    {
        services.AddKeyedScoped<IJobHandler, InvitationEmailJobHandler>(JobType.InvitationEmail);
        services.AddKeyedScoped<IJobHandler, ResultEmailJobHandler>(JobType.ResultEmail);
        services.AddKeyedScoped<IJobHandler, ReportEmailJobHandler>(JobType.ReportEmail);
        services.AddKeyedScoped<IJobHandler, RefTestExpirationJobHandler>(JobType.RefTestExpiration);
        services.AddKeyedScoped<IJobHandler, ApprovalDecisionEmailJobHandler>(JobType.ApprovalDecisionEmail);
        services.AddKeyedScoped<IJobHandler, PersonalDataExportChallengeEmailJobHandler>(
            JobType.PersonalDataExportChallengeEmail);
        services.AddKeyedScoped<IJobHandler, PersonalDataExportDeliveryEmailJobHandler>(
            JobType.PersonalDataExportDeliveryEmail);
        services.AddKeyedScoped<IJobHandler, PrivacyWithdrawalChallengeEmailJobHandler>(
            JobType.PrivacyWithdrawalChallengeEmail);
        services.AddKeyedScoped<IJobHandler, PrivacyWithdrawalBatchJobHandler>(JobType.PrivacyWithdrawalBatch);

        return services;
    }

    /// <summary>
    /// Registers the generated IHF Rules questions GraphQL client used by
    /// <see cref="IhfRulesQuestionsService"/>. The address is parsed when the client is first
    /// created, not at startup, so hosts that never call the question bank need not configure it.
    /// </summary>
    public static IServiceCollection AddIhfRulesQuestionsHttpClient(this IServiceCollection services, string? baseAddress)
    {
        // This client sits on the test-creation path, so an upstream hang without a timeout fails
        // test creation after a two-minute wait with nothing to show for it. It only issues GraphQL
        // queries, never mutations, so retrying is safe.
        services.AddIHFRulesQuestionsClient(ExecutionStrategy.CacheFirst)
            .ConfigureHttpClient(
                (sp, c) =>
                {
                    c.BaseAddress = new Uri(baseAddress!);
                    var langConfig = sp.GetRequiredService<LanguageConfiguration>();
                    c.DefaultRequestHeaders.Add("Accept-Language", langConfig.DefaultPhraseLanguage);
                    c.Timeout = TimeSpan.FromSeconds(30);
                },
                // StrawberryShake wraps the registration in its own builder, so the resilience handler has
                // to be added through this hook rather than chained off the call.
                clientBuilder => clientBuilder.AddStandardResilienceHandler());

        return services;
    }
}
