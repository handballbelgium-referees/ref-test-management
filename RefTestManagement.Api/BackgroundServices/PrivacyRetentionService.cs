using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
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
                // This loop reads and rewrites participant names and addresses, so an exception
                // raised inside it can quote them back.
                logger.LogError(LogRedaction.MaskEmails(exception), "Privacy retention cleanup failed");
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
            .Where(PrivacyRetentionQueries.IsDueForErasure(cutoff))
            .ToListAsync(cancellationToken);

        foreach (var refTest in refTests)
        {
            await erasureService.EraseAsync(refTest, ErasureInitiator.Operator, cancellationToken);
        }

        if (refTests.Count > 0)
            logger.LogInformation("Erased {Count} RefTest records that exceeded privacy retention", refTests.Count);
    }
}