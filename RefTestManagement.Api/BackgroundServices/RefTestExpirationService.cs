using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
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

        var now = DateTime.UtcNow;

        // The predicate runs in the database, so only rows that are genuinely due come back. Two
        // columns are projected because the action depends on the status — nothing else about the
        // row is needed to enqueue the job.
        var expiredTests = await context.RefTests
            .Where(RefTestExpirationQueries.IsDueForExpiration(now, _expirationIfNotStarted))
            .Select(rt => new { rt.Id, rt.Status })
            .ToListAsync(cancellationToken);

        if (expiredTests.Count == 0)
        {
            ServiceLoggerMessages.LogNoPotentiallyExpiredTests(_logger);
            return;
        }

        ServiceLoggerMessages.LogCheckingExpiredTests(_logger, expiredTests.Count);

        foreach (var test in expiredTests)
        {
            // An in-progress test has answers worth scoring; one that was never started does not.
            var action = test.Status == RefTestStatus.InProgress
                ? RefTestExpirationAction.AutoComplete
                : RefTestExpirationAction.MarkAsExpired;

            await jobEnqueueService.EnqueueRefTestExpirationAsync(
                new RefTestExpirationPayload(test.Id, action),
                executeAfter: null,
                cancellationToken: cancellationToken);

            ServiceLoggerMessages.LogEnqueuedExpirationJob(_logger, action, test.Id);
        }

        ServiceLoggerMessages.LogEnqueuedExpirationJobs(_logger, expiredTests.Count);
    }
}