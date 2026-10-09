using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests;

/// <summary>A requested email that was not queued, with the participant when the RefTest exists.</summary>
public sealed record RefTestEmailFailure(Guid RefTestId, RefTestParticipant? Participant, Exception Exception);

/// <param name="Sent">RefTests whose email was queued, in request order.</param>
public sealed record SendRefTestEmailsOutcome(IReadOnlyList<RefTest> Sent, IReadOnlyList<RefTestEmailFailure> Failures);

/// <summary>
/// Queues invitation and result emails on request. Each RefTest is saved on its own, so one
/// failure rolls back only that RefTest and the batch continues.
/// </summary>
public static class RefTestEmailHandler
{
    /// <summary>
    /// Sends a fresh invitation to each pending RefTest. A new token is issued every time, so an
    /// earlier invitation link stops working.
    /// </summary>
    public static Task<SendRefTestEmailsOutcome> SendInvitationsAsync(
        IReadOnlyList<Guid> ids, IRefTestUnitOfWork unitOfWork, CancellationToken cancellationToken) =>
        SendAsync(ids, unitOfWork, async refTest =>
        {
            if (refTest.IsAnonymized)
                throw new InvalidRefTestStatusException("Cannot send an invitation for a RefTest whose consent has been withdrawn");
            if (refTest.Status != RefTestStatus.Pending)
                throw new InvalidRefTestStatusException(refTest.Status, RefTestStatus.Pending);

            refTest.RegenerateToken();
            await unitOfWork.StageInvitationEmailAsync(refTest, executeAfter: null, cancellationToken);
        }, cancellationToken);

    /// <summary>Sends the results of each completed RefTest.</summary>
    public static Task<SendRefTestEmailsOutcome> SendResultsAsync(
        IReadOnlyList<Guid> ids, IRefTestUnitOfWork unitOfWork, CancellationToken cancellationToken) =>
        SendAsync(ids, unitOfWork, refTest =>
        {
            if (refTest.IsAnonymized)
                throw new InvalidRefTestStatusException("Cannot send results for a RefTest whose consent has been withdrawn");
            if (refTest.Status != RefTestStatus.Completed)
                throw new InvalidRefTestStatusException(refTest.Status, RefTestStatus.Completed);

            return unitOfWork.StageResultEmailAsync(
                new ResultEmailPayload(
                    refTest.Id,
                    refTest.FullName,
                    refTest.Email,
                    refTest.QuestionScore ?? 0,
                    refTest.AnswerScore ?? 0,
                    refTest.QuestionTotal,
                    refTest.AnswerTotal ?? 0,
                    refTest.Percentage ?? 0,
                    [.. refTest.SelectedAnswerIds],
                    [.. refTest.WrongQuestionIds],
                    [.. refTest.WrongAnswerIds]),
                cancellationToken);
        }, cancellationToken);

    private static async Task<SendRefTestEmailsOutcome> SendAsync(
        IReadOnlyList<Guid> ids,
        IRefTestUnitOfWork unitOfWork,
        Func<RefTest, Task> stage,
        CancellationToken cancellationToken)
    {
        var refTests = (await unitOfWork.GetRefTestsAsync([.. ids], cancellationToken)).ToList();
        var sent = new List<RefTest>();
        var failures = new List<RefTestEmailFailure>();

        foreach (var id in ids)
        {
            var refTest = refTests.FirstOrDefault(rt => rt.Id == id);
            try
            {
                if (refTest is null)
                    throw new RefTestNotFoundException(id);

                await stage(refTest);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                sent.Add(refTest);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                failures.Add(new RefTestEmailFailure(
                    id,
                    refTest is null ? null : new RefTestParticipant(refTest.FirstName, refTest.LastName, refTest.Email),
                    ex));

                if (refTest is not null)
                    await ResetRefTestsHandler.ReplaceWithPersistedAsync(refTests, refTest, unitOfWork, cancellationToken);
            }
        }

        return new SendRefTestEmailsOutcome(sent, failures);
    }
}
