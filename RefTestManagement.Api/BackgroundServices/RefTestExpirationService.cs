using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

/// <summary>
/// Background service that checks for expired RefTests and enqueues specific jobs to handle them.
/// This approach is more efficient than processing all tests - it only creates jobs for tests that need action.
/// </summary>
public class RefTestExpirationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RefTestExpirationService> _logger;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _startupDelay;
    private readonly TimeSpan _expirationIfNotStarted;

    public RefTestExpirationService(
        IServiceProvider serviceProvider,
        ILogger<RefTestExpirationService> logger,
        RefTestExpirationConfiguration? configuration = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        configuration ??= new RefTestExpirationConfiguration();
        _checkInterval = TimeSpan.FromMinutes(configuration.ExpirationCheckIntervalMinutes);
        _startupDelay = TimeSpan.FromSeconds(configuration.StartupDelaySeconds);
        _expirationIfNotStarted = configuration.ExpirationIfNotStarted;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServiceLoggerMessages.LogServiceStarting(_logger, nameof(RefTestExpirationService));

        // Wait a bit before the first execution to let the app fully start
        await Task.Delay(_startupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndEnqueueExpiredTestsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                ServiceLoggerMessages.LogServiceError(_logger, ex, nameof(RefTestExpirationService));
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when the service is stopping
                break;
            }
        }

        ServiceLoggerMessages.LogServiceStopping(_logger, nameof(RefTestExpirationService));
    }

    private async Task CheckAndEnqueueExpiredTestsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RefTestManagementContext>>();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var jobEnqueueService = scope.ServiceProvider.GetRequiredService<IJobEnqueueService>();

        // Find all tests that might be expired - only load what we need for checking
        var potentiallyExpiredTests = await context.RefTests
            .Where(rt => rt.Status != RefTestStatus.Completed && rt.Status != RefTestStatus.Expired && !rt.IsAnonymized)
            .Select(rt => new { rt.Id, rt.Status, rt.StartedAt, rt.CreatedAt, rt.MaxTimeInMinutes })
            .ToListAsync(cancellationToken);

        if (potentiallyExpiredTests.Count == 0)
        {
            ServiceLoggerMessages.LogNoPotentiallyExpiredTests(_logger);
            return;
        }

        ServiceLoggerMessages.LogCheckingExpiredTests(_logger, potentiallyExpiredTests.Count);

        var enqueuedCount = 0;
        var now = DateTime.UtcNow;

        foreach (var test in potentiallyExpiredTests)
        {
            var isExpired = IsExpired(test.Status, test.StartedAt, test.CreatedAt, test.MaxTimeInMinutes, now);

            if (!isExpired)
                continue;

            // Determine the action based on status
            var action = test.Status == RefTestStatus.InProgress
                ? RefTestExpirationAction.AutoComplete
                : RefTestExpirationAction.MarkAsExpired;

            // Enqueue a specific job to handle this expired test
            await jobEnqueueService.EnqueueRefTestExpirationAsync(
                new RefTestExpirationPayload(test.Id, action),
                executeAfter: null,
                cancellationToken);

            enqueuedCount++;
            ServiceLoggerMessages.LogEnqueuedExpirationJob(_logger, action, test.Id);
        }

        if (enqueuedCount > 0)
        {
            ServiceLoggerMessages.LogEnqueuedExpirationJobs(_logger, enqueuedCount);
        }
    }

    private bool IsExpired(RefTestStatus status, DateTime? startedAt, DateTime createdAt, int maxTimeInMinutes,
        DateTime now)
    {
        switch (status)
        {
            case RefTestStatus.InProgress when startedAt.HasValue:
            {
                // Check if the test has exceeded its time limit
                var expirationTime = startedAt.Value.AddMinutes(maxTimeInMinutes);
                return now >= expirationTime;
            }
            case RefTestStatus.Pending:
            {
                // Check if the test was never started and is too old
                var expirationTime = createdAt.Add(_expirationIfNotStarted);
                return now >= expirationTime;
            }
            case RefTestStatus.Completed:
            case RefTestStatus.Expired:
            case RefTestStatus.PendingApproval:
            case RefTestStatus.Rejected:
            default:
                return false;
        }
    }
}