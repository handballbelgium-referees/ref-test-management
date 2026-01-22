using Handball.Belgium.RefTestManagement.Api.Graphql;
using Handball.Belgium.RefTestManagement.Api.Graphql.Models;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;


/// <summary>
/// Background service that periodically checks for expired RefTests and updates their status
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
                await ProcessExpiredRefTestsAsync(stoppingToken);
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

    private async Task ProcessExpiredRefTestsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RefTestManagementContext>>();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Find all tests that are not completed and might be expired
        var potentiallyExpiredTests = await context.RefTests
            .Where(rt => rt.Status != RefTestStatus.Completed && rt.Status != RefTestStatus.Expired)
            .ToListAsync(cancellationToken);

        if (potentiallyExpiredTests.Count == 0)
        {
            ServiceLoggerMessages.LogNoPotentiallyExpiredTests(_logger);
            return;
        }

        ServiceLoggerMessages.LogCheckingExpiredTests(_logger, potentiallyExpiredTests.Count);

        var expiredCount = 0;
        var completedCount = 0;

        foreach (var refTest in potentiallyExpiredTests)
        {
            var isExpired = refTest.IsExpired(_expirationIfNotStarted);
            ServiceLoggerMessages.LogRefTestExpirationCheck(_logger, refTest.Id, isExpired, refTest.Status);
            
            if (!isExpired)
                continue;

            if (refTest.Status == RefTestStatus.InProgress)
            {
                // For in-progress tests, complete them automatically
                try
                {
                    var ihfRulesQuestionsService = scope.ServiceProvider.GetRequiredService<IIhfRulesQuestionsService>();
                    var jobEnqueueService = scope.ServiceProvider.GetRequiredService<IJobEnqueueService>();
                    var emailConfiguration = scope.ServiceProvider.GetRequiredService<EmailConfiguration>();
                    
                    await RefTestMutations.CompleteRefTestAsync(
                        new CompleteRefTestInput(refTest.Token, refTest.SelectedAnswerIds, refTest.Language),
                        context,
                        ihfRulesQuestionsService,
                        jobEnqueueService,
                        emailConfiguration,
                        cancellationToken);

                    completedCount++;
                    ServiceLoggerMessages.LogAutoCompleted(_logger, refTest.Id, refTest.Email);
                }
                catch (Exception ex)
                {
                    ServiceLoggerMessages.LogAutoCompleteFailed(_logger, ex, refTest.Id, refTest.Email);
                }
            }
            else
            {
                // For other statuses (Pending), just mark as expired
                refTest.Expire();
                context.RefTests.Update(refTest);
                expiredCount++;
                
                ServiceLoggerMessages.LogExpired(_logger, refTest.Id, refTest.Status, refTest.Email);
            }
        }

        if (expiredCount > 0 || completedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            ServiceLoggerMessages.LogProcessingSummary(_logger, expiredCount + completedCount, expiredCount, completedCount);
        }
    }
}
