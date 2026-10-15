using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Auth0;
using Handball.Belgium.RefTestManagement.Auth0.Services;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure.Extensions;
using Handball.Belgium.RefTestManagement.Infrastructure.Jobs;
using Handball.Belgium.RefTestManagement.Hosting.BackgroundServices;
using Handball.Belgium.RefTestManagement.Hosting.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Hosting.Configurations;
using Handball.Belgium.RefTestManagement.Hosting.Services;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Keycloak;
using Handball.Belgium.RefTestManagement.Security;
using Microsoft.AspNetCore.DataProtection;
using QuestPDF.Infrastructure;

namespace Handball.Belgium.RefTestManagement.Hosting;

public static class HostingServiceCollectionExtensions
{
    public static StateConfiguration AddRefTestManagementHosting(
        this IServiceCollection services,
        IConfiguration configuration,
        HostingMode mode,
        Action<IDataProtectionBuilder>? configureDataProtection = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddHttpContextAccessor();
        var dataProtection = services.AddDataProtection()
            .SetApplicationName("RefTestManagement")
            .PersistKeysToDbContext<RefTestManagementContext>();
        configureDataProtection?.Invoke(dataProtection);

        var auditLogOptions = AddAuditLogging(services, configuration);

        services.AddDatabaseProvider(configuration);
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
        services.AddValidatedConfiguration<PrivacyConfiguration>(
            configuration, "PrivacyConfiguration", value => value.Validate());
        services.AddValidatedConfiguration<RefTestExpirationConfiguration>(
            configuration, "RefTestExpirationConfiguration");
        services.AddValidatedConfiguration<BackgroundJobConfiguration>(
            configuration, "BackgroundJobConfiguration");
        var redisConfiguration = services.AddValidatedConfiguration<RedisConfiguration>(
            configuration, "RedisConfiguration", value => value.Validate());
        var stateConfiguration = services.AddValidatedConfiguration<StateConfiguration>(
            configuration, "State");
        var privacyChallengeConfiguration = services.AddValidatedConfiguration<PrivacyChallengeConfiguration>(
            configuration,
            "PrivacyChallengeConfiguration",
            value => value.Validate(redisConfiguration.IsConfigured));
        stateConfiguration.Validate(redisConfiguration, privacyChallengeConfiguration);

        if (mode == HostingMode.Worker && stateConfiguration.Mode != StateMode.Shared)
            throw new InvalidOperationException("The Worker executable requires State:Mode=Shared.");

        if (stateConfiguration.Mode == StateMode.Shared ||
            privacyChallengeConfiguration.RateLimitBackend == PrivacyChallengeRateLimitBackend.Redis)
        {
            services.AddRefTestManagementRedis(redisConfiguration);
        }
        if (stateConfiguration.Mode == StateMode.Shared)
            services.AddSingleton<IRefTestSessionService, RedisRefTestSessionService>();

        services.AddInfrastructureServices();
        services.AddAuth0ManagementServices(configuration);
        AddIdentityPermissionSync(services, configuration);
        services.AddMemoryCache();

        if (mode is HostingMode.All or HostingMode.Worker)
        {
            services.AddJobHandlers();
            services.AddKeyedScoped<IJobHandler, ApprovalNotificationEmailJobHandler>(
                JobType.ApprovalNotificationEmail);
            services.AddHostedService<PermissionSyncService>();
            services.AddHostedService<RefTestExpirationService>();
            services.AddHostedService<PrivacyRetentionService>();
            services.AddHostedService<PersonalDataExportRequestCleanupService>();
            services.AddHostedService<PrivacyWithdrawalCleanupService>();
            services.AddHostedService<BackgroundJobService>();

            if (auditLogOptions.EnableCleanup)
                services.AddHostedService<AuditLogCleanupService>();
        }

        if (mode == HostingMode.Worker)
        {
            services.AddGraphQLServer()
                .AddQueryType<WorkerQuery>()
                .AddRedisSubscriptions(sp => sp.GetRequiredService<IConnectionMultiplexer>());
        }

        return stateConfiguration;
    }

    public static IServiceCollection AddRefTestManagementMigrationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        AddAuditLogging(services, configuration);
        services.AddDatabaseProvider(configuration);
        return services;
    }

    private static void AddIdentityPermissionSync(IServiceCollection services, IConfiguration configuration)
    {
        services.AddKeycloakPermissionSync(configuration);

        // Permission sync follows the configured provider. The Auth0 registration stays
        // unconditional because the API's permission snapshot service also depends on it.
        services.AddTransient<IIdentityPermissionSync>(serviceProvider =>
            string.Equals(
                configuration["Identity:Provider"],
                "Keycloak",
                StringComparison.OrdinalIgnoreCase)
                ? serviceProvider.GetRequiredService<KeycloakPermissionSyncService>()
                : serviceProvider.GetRequiredService<IAuth0ManagementService>());
    }

    private static AuditLogOptions AddAuditLogging(
        IServiceCollection services,
        IConfiguration configuration)
    {
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
            opts.RegisterEntityResolver("RefTestTitle", (id, context) =>
                context.Set<RefTestTitle>().Find(id)?.Value);
        });
        auditLogOptions.Validate();
        return auditLogOptions;
    }
}

public sealed class WorkerQuery
{
    public string Role => "worker";
}
