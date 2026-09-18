using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

public sealed class PrivacyRetentionService(
    IServiceProvider serviceProvider,
    ILogger<PrivacyRetentionService> logger,
    PrivacyConfiguration privacyConfiguration) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EraseExpiredDataAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Privacy retention cleanup failed");
            }

            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task EraseExpiredDataAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RefTestManagementContext>();
        var erasureService = scope.ServiceProvider.GetRequiredService<IRefTestPrivacyErasureService>();
        var cutoff = DateTime.UtcNow.AddYears(-privacyConfiguration.RetentionYears);

        var refTests = await context.RefTests
            .Where(refTest =>
                !refTest.IsAnonymized &&
                ((refTest.Status == RefTestStatus.Completed && refTest.CompletedAt < cutoff) ||
                 (refTest.Status == RefTestStatus.Expired && refTest.ExpiredAt < cutoff) ||
                 // PendingApproval and Rejected are terminal too: RefTestExpirationService never
                 // transitions them, so without this clause they would retain personal data
                 // forever. Neither status stamps a completion timestamp, so retention runs from
                 // CreatedAt — a RefTest left unapproved for the entire retention window is
                 // abandoned, and keeping personal data for it has no lawful basis.
                 ((refTest.Status == RefTestStatus.PendingApproval ||
                   refTest.Status == RefTestStatus.Rejected) && refTest.CreatedAt < cutoff)))
            .ToListAsync(cancellationToken);

        foreach (var refTest in refTests)
        {
            await erasureService.EraseAsync(refTest, cancellationToken);
        }

        if (refTests.Count > 0)
            logger.LogInformation("Erased {Count} RefTest records that exceeded privacy retention", refTests.Count);
    }
}