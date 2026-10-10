using Handball.Belgium.RefTestManagement.Application.Configurations;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

/// <summary>
/// Clears expired, unverified export challenges and verified requests with terminal, inactive
/// delivery jobs.
/// </summary>
public sealed class PersonalDataExportRequestCleanupService(
    IServiceScopeFactory scopeFactory,
    PrivacyChallengeConfiguration configuration,
    ILogger<PersonalDataExportRequestCleanupService> logger) : PollingBackgroundService
{
    protected override TimeSpan Interval => TimeSpan.FromMinutes(configuration.CleanupIntervalMinutes);

    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var cleanup = scope.ServiceProvider.GetRequiredService<IPersonalDataExportRequestCleanup>();
        var count = await cleanup.ClearExpiredChallengesAsync(DateTime.UtcNow, cancellationToken);
        if (count > 0)
            logger.LogInformation(
                "Cleared {Count} expired or terminal personal-data export requests",
                count);
    }

    // Do not attach database exception details: expired challenge rows contain participant
    // addresses and protected key material.
    protected override void LogFailure(Exception exception) =>
        logger.LogError("Personal-data export request cleanup failed.");
}
