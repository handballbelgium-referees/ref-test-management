using Handball.Belgium.RefTestManagement.Api.Graphql;
using Handball.Belgium.RefTestManagement.Api.Graphql.Models;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;


/// <summary>
/// Background service that periodically checks for expired RefTests and updates their status
/// </summary>
public partial class RefTestExpirationService : BackgroundService
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

    // High-performance logging using source generators
    [LoggerMessage(Level = LogLevel.Information, Message = "RefTest Expiration Service is starting")]
    partial void LogServiceStarting();

    [LoggerMessage(Level = LogLevel.Information, Message = "RefTest Expiration Service is stopping")]
    partial void LogServiceStopping();

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occurred while processing expired RefTests")]
    partial void LogProcessingError(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No potentially expired RefTests found")]
    partial void LogNoPotentiallyExpiredTests();

    [LoggerMessage(Level = LogLevel.Information, Message = "Checking {count} potentially expired RefTests")]
    partial void LogCheckingExpiredTests(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "RefTest {refTestId} is expired: {isExpired}, Status: {status}")]
    partial void LogRefTestExpirationCheck(Guid refTestId, bool isExpired, RefTestStatus status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Auto-completed expired RefTest {refTestId} for {email}")]
    partial void LogAutoCompleted(Guid refTestId, string email);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to auto-complete expired RefTest {refTestId} for {email}")]
    partial void LogAutoCompleteFailed(Exception ex, Guid refTestId, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Expired RefTest {refTestId} in status {status} for {email}")]
    partial void LogExpired(Guid refTestId, RefTestStatus status, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Processed {totalCount} expired RefTests: {expiredCount} expired, {completedCount} auto-completed")]
    partial void LogProcessingSummary(int totalCount, int expiredCount, int completedCount);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarting();

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
                LogProcessingError(ex);
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

        LogServiceStopping();
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
            LogNoPotentiallyExpiredTests();
            return;
        }

        LogCheckingExpiredTests(potentiallyExpiredTests.Count);

        var expiredCount = 0;
        var completedCount = 0;

        foreach (var refTest in potentiallyExpiredTests)
        {
            var isExpired = refTest.IsExpired(_expirationIfNotStarted);
            LogRefTestExpirationCheck(refTest.Id, isExpired, refTest.Status);
            
            if (!isExpired)
                continue;

            if (refTest.Status == RefTestStatus.InProgress)
            {
                // For in-progress tests, complete them automatically
                try
                {
                    var ihfRulesQuestionsService = scope.ServiceProvider.GetRequiredService<IIhfRulesQuestionsService>();
                    var jobEnqueueService = scope.ServiceProvider.GetRequiredService<IJobEnqueueService>();
                    
                    await RefTestMutations.CompleteRefTestAsync(
                        new CompleteRefTestInput(refTest.Token, refTest.SelectedAnswerIds, refTest.Language),
                        context,
                        ihfRulesQuestionsService,
                        jobEnqueueService,
                        cancellationToken);

                    completedCount++;
                    LogAutoCompleted(refTest.Id, refTest.Email);
                }
                catch (Exception ex)
                {
                    LogAutoCompleteFailed(ex, refTest.Id, refTest.Email);
                }
            }
            else
            {
                // For other statuses (Pending), just mark as expired
                refTest.Expire();
                context.RefTests.Update(refTest);
                expiredCount++;
                
                LogExpired(refTest.Id, refTest.Status, refTest.Email);
            }
        }

        if (expiredCount > 0 || completedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            LogProcessingSummary(expiredCount + completedCount, expiredCount, completedCount);
        }
    }
}

