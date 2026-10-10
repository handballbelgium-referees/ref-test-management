using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.RefTests;
using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Handball.Belgium.RefTestManagement.Infrastructure.Persistence;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Jobs;

/// <summary>
/// Closes out a RefTest that has reached its deadline, either by auto-completing an attempt already
/// in progress or by expiring one never started.
/// </summary>
/// <remarks>
/// This handler uses <see cref="IDbContextFactory{TContext}"/> rather than the scoped context. The
/// auto-complete path runs the same completion use case participants do, which tracks a graph of its
/// own, and it must not share a change tracker with the worker loop that owns the job row.
/// The enqueue service is built on this same context so completion and its result-email outbox row
/// commit together.
/// </remarks>
public sealed class RefTestExpirationJobHandler(
    IDbContextFactory<RefTestManagementContext> contextFactory,
    IRefTestSubscriptionService subscriptionService,
    IIhfRulesQuestionsService ihfRulesQuestionsService,
    EmailConfiguration emailConfiguration,
    IRefTestInvitationTokenProtection tokenProtection,
    ILogger<RefTestExpirationJobHandler> logger,
    ILogger<JobEnqueueService> jobEnqueueLogger,
    TimeProvider? timeProvider = null) : IJobHandler
{
    public async Task HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JobPayload.Deserialize<RefTestExpirationPayload>(job, logger);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Load the specific RefTest
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(rt => rt.Id == payload.RefTestId, cancellationToken);

        if (refTest == null)
        {
            logger.LogWarning("RefTest {Id} not found for expiration job", payload.RefTestId);
            return;
        }

        // Skip if already completed, expired, or its consent has been withdrawn
        if (refTest.Status == RefTestStatus.Completed || refTest.Status == RefTestStatus.Expired ||
            refTest.IsAnonymized)
        {
            logger.LogDebug(
                "RefTest {Id} already in status {Status} or anonymized ({IsAnonymized}), skipping",
                refTest.Id, refTest.Status, refTest.IsAnonymized);
            return;
        }

        ServiceLoggerMessages.LogRefTestExpirationCheck(logger, refTest.Id, true, refTest.Status);

        try
        {
            switch (payload.Action)
            {
                case RefTestExpirationAction.AutoComplete when refTest.Status == RefTestStatus.InProgress:
                {
                    var jobEnqueueService = new JobEnqueueService(context, tokenProtection, jobEnqueueLogger, timeProvider);
                    await new CompleteRefTestHandler(
                            ihfRulesQuestionsService, subscriptionService, emailConfiguration,
                            timeProvider ?? TimeProvider.System)
                        .HandleAsync(
                            refTest,
                            refTest.SelectedAnswerIds,
                            refTest.Language,
                            RefTestCompletionSource.ExpirationService,
                            new EfRefTestUnitOfWork(context, jobEnqueueService),
                            cancellationToken);

                    ServiceLoggerMessages.LogAutoCompleted(logger, refTest.Id);
                    break;
                }
                case RefTestExpirationAction.MarkAsExpired:
                    if (refTest.Status != RefTestStatus.Pending)
                    {
                        logger.LogWarning(
                            "Ignoring MarkAsExpired for RefTest {Id} in status {Status}",
                            refTest.Id,
                            refTest.Status);
                        break;
                    }

                    refTest.Expire(timeProvider?.GetUtcNow().UtcDateTime);
                    await context.SaveChangesWithRetryAsync(cancellationToken);

                    // Publish subscription event
                    await subscriptionService.PublishRefTestExpiredAsync(
                        refTest.Id,
                        refTest.Status,
                        DateTime.UtcNow,
                        cancellationToken);

                    ServiceLoggerMessages.LogExpired(logger, refTest.Id, refTest.Status);
                    break;
                case RefTestExpirationAction.AutoComplete:
                    logger.LogWarning(
                        "Ignoring AutoComplete for RefTest {Id} in status {Status}",
                        refTest.Id,
                        refTest.Status);
                    break;
            }
        }
        catch (Exception ex)
        {
            ServiceLoggerMessages.LogAutoCompleteFailed(logger, ex, refTest.Id);
            throw; // Re-throw so the job can be retried
        }
    }
}
