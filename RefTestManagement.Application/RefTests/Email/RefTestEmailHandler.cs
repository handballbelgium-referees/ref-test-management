using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Application.RefTests.Email;

public sealed class RefTestEmailHandler(
    IRefTestRepository refTestRepository,
    IUnitOfWork unitOfWork,
    IJobEnqueueService jobEnqueueService,
    ReportConfiguration reportConfig,
    ScoreConfiguration scoreConfig,
    ICurrentUser currentUser,
    ILogger<RefTestEmailHandler> logger)
{
    public async Task<SendInvitationsResult> SendInvitationsAsync(
        SendInvitationsCommand command,
        CancellationToken cancellationToken = default)
    {
        var refTests = await refTestRepository.GetByIdsAsync(command.Ids, cancellationToken);
        var result = new SendInvitationsResult { TotalRequested = command.Ids.Count };

        foreach (var id in command.Ids)
        {
            var refTest = refTests.FirstOrDefault(candidate => candidate.Id == id);
            try
            {
                // A previous failure clears the shared tracker. Re-query detached candidates so
                // each later item uses current database state, just as it did before the clear.
                if (refTest is not null)
                    refTest = await refTestRepository.ReloadIfDetachedAsync(refTest, cancellationToken);

                if (refTest is null)
                    throw new RefTestNotFoundException(id.ToString());

                if (refTest.IsAnonymized)
                    throw new InvalidRefTestStatusException("Cannot send an invitation for a RefTest whose consent has been withdrawn");

                if (refTest.Status != RefTestStatus.Pending)
                    throw new InvalidRefTestStatusException(refTest.Status, RefTestStatus.Pending);

                refTest.RegenerateToken();
                await jobEnqueueService.EnqueueInvitationEmailAsync(
                    refTest, unitOfWorkContext: unitOfWork, cancellationToken: cancellationToken);

                result.SentRefTests.Add(refTest);
                result.SuccessfullySent++;
            }
            catch (Exception exception)
            {

                // Email preparation failed, the RefTest was not found, or its status was invalid.
                // Keep ordinary per-ID failures isolated so later requests are still processed.
                MutationFailureHandling.LogMutationFailure(
                    logger, exception, nameof(SendInvitationsAsync), currentUser.CorrelationId, id);
                result.Failed++;
                result.Errors.Add(new SendInvitationError(id, refTest, "Failed to send invitation email."));

                // Discard the failed RefTest/job state; later IDs are reloaded before they are saved.
                unitOfWork.DiscardTrackedChanges();
            }
        }

        return result;
    }

    public async Task<SendResultsResult> SendResultsAsync(
        SendResultsCommand command,
        CancellationToken cancellationToken = default)
    {
        var refTests = await refTestRepository.GetByIdsAsync(command.Ids, cancellationToken);
        var result = new SendResultsResult { TotalRequested = command.Ids.Count };

        foreach (var id in command.Ids)
        {
            var refTest = refTests.FirstOrDefault(candidate => candidate.Id == id);
            try
            {
                if (refTest is null)
                    throw new RefTestNotFoundException(id.ToString());

                if (refTest.IsAnonymized)
                    throw new InvalidRefTestStatusException("Cannot send results for a RefTest whose consent has been withdrawn");

                if (refTest.Status != RefTestStatus.Completed)
                    throw new InvalidRefTestStatusException(refTest.Status, RefTestStatus.Completed);

                var payload = new ResultEmailPayload(
                    refTest.Id,
                    refTest.FullName,
                    refTest.Email,
                    refTest.QuestionScore ?? 0,
                    refTest.AnswerScore ?? 0,
                    refTest.QuestionTotal,
                    refTest.AnswerTotal ?? 0,
                    refTest.Percentage ?? 0,
                    refTest.SelectedAnswerIds,
                    refTest.WrongQuestionIds,
                    refTest.WrongAnswerIds);

                await jobEnqueueService.EnqueueResultEmailAsync(payload, cancellationToken: cancellationToken);

                result.SentRefTests.Add(refTest);
                result.SuccessfullySent++;
            }
            catch (Exception exception)
            {

                // Email enqueue failed, the RefTest was not found, or its status was invalid.
                // Keep ordinary per-ID failures isolated so later requests are still processed.
                MutationFailureHandling.LogMutationFailure(
                    logger, exception, nameof(SendResultsAsync), currentUser.CorrelationId, id);
                result.Failed++;
                result.Errors.Add(new SendResultError(id, refTest, "Failed to send result email."));
            }
        }

        return result;
    }

    public async Task<SendReportResult> SendReportAsync(
        SendReportCommand command,
        CancellationToken cancellationToken = default)
    {
        var refTests = await refTestRepository.GetOrderedForReportAsync(command.Ids, cancellationToken);

        if (refTests.Count == 0)
            return new SendReportResult(false, "No RefTests found with the provided IDs", 0);

        var reportData = refTests.Select(refTest => new RefTestReportPayloadData(
            refTest.Id,
            refTest.Title?.Value ?? "Unknown",
            refTest.FirstName,
            refTest.LastName,
            refTest.StartedAt,
            refTest.CompletedAt,
            refTest.QuestionScore,
            refTest.QuestionTotal,
            refTest.AnswerScore,
            refTest.AnswerTotal,
            refTest.Percentage,
            refTest.Percentage >= scoreConfig.PassingPercentage,
            refTest.Language,
            refTest.Duration)).ToList();

        var recipients = reportConfig.RecipientEmails;
        if (recipients.Length == 0)
            return new SendReportResult(false, "No recipient emails configured", refTests.Count);

        try
        {
            var reportPayload = new ReportEmailPayload(
                recipients,
                reportData,
                DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

            await jobEnqueueService.EnqueueReportEmailAsync(reportPayload, cancellationToken: cancellationToken);

            return new SendReportResult(
                true,
                $"Report job successfully enqueued for {recipients.Length} recipient(s)",
                refTests.Count);
        }
        catch (Exception exception)
        {

            MutationFailureHandling.LogMutationFailure(
                logger, exception, nameof(SendReportAsync), currentUser.CorrelationId);
            return new SendReportResult(
                false,
                "Failed to enqueue report job. Please try again later.",
                refTests.Count);
        }
    }
}
