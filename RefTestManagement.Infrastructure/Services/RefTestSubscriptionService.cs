using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using HotChocolate.Subscriptions;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

/// <summary>
/// Publishes live-refresh events after the change they describe has committed. Publishing is best
/// effort: a failed publish is logged and swallowed, because surfacing it would fail a mutation that
/// already committed (inviting a duplicate retry) or a job whose email already went out (sending it
/// again). Clients that miss an event catch up on their next query.
/// </summary>
public class RefTestSubscriptionService(
    ITopicEventSender eventSender,
    ILogger<RefTestSubscriptionService> logger) : IRefTestSubscriptionService
{
    public const string GlobalTopic = "RefTestEvents";
    public const string TimeExtendedTopic = "RefTestTimeExtended-{id}";

    private async Task SendAsync<TMessage>(string topic, TMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await eventSender.SendAsync(topic, message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Only type names: the event and the transport error can both carry participant data.
            ServiceLoggerMessages.LogSubscriptionPublishFailed(
                logger, message?.GetType().Name ?? typeof(TMessage).Name, exception.GetType().Name);
        }
    }

    public async Task PublishTimeExtendedAsync(
        Guid refTestId,
        int newMaxTimeInMinutes,
        int additionalMinutes,
        DateTime extendedAt,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestTimeExtendedEvent(refTestId, newMaxTimeInMinutes, additionalMinutes, extendedAt);

        await SendAsync(TimeExtendedTopic.Replace("{id}", refTestId.ToString()), evt, cancellationToken);
    }

    public async Task PublishInvitationSentAsync(
        Guid refTestId,
        DateTime sentAt,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestInvitationSentEvent(refTestId, sentAt);

        await Task.WhenAll(
            SendAsync<object>(refTestId.ToString(), evt, cancellationToken),
            SendAsync<object>(GlobalTopic, evt, cancellationToken)
        );
    }

    public async Task PublishResultSentAsync(
        Guid refTestId,
        DateTime sentAt,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestResultSentEvent(refTestId, sentAt);

        await Task.WhenAll(
            SendAsync<object>(refTestId.ToString(), evt, cancellationToken),
            SendAsync<object>(GlobalTopic, evt, cancellationToken)
        );
    }

    public async Task PublishRefTestStartedAsync(
        Guid refTestId,
        RefTestStatus status,
        DateTime startedAt,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestStartedEvent(refTestId, status, startedAt);

        await Task.WhenAll(
            SendAsync<object>(refTestId.ToString(), evt, cancellationToken),
            SendAsync<object>(GlobalTopic, evt, cancellationToken)
        );
    }

    public async Task PublishRefTestCompletedAsync(
        Guid refTestId,
        RefTestStatus status,
        DateTime completedAt,
        int questionScore,
        int questionTotal,
        int answerScore,
        int answerTotal,
        double percentage,
        string language,
        IReadOnlyList<string> selectedAnswerIds,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestCompletedEvent(refTestId, status, completedAt, questionScore, questionTotal, answerScore,
            answerTotal, percentage, language, selectedAnswerIds);
        var listEvent = evt with { SelectedAnswerIds = null };

        await Task.WhenAll(
            SendAsync<object>(refTestId.ToString(), evt, cancellationToken),
            SendAsync<object>(GlobalTopic, listEvent, cancellationToken)
        );
    }

    public async Task PublishRefTestExpiredAsync(
        Guid refTestId,
        RefTestStatus status,
        DateTime expiredAt,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestExpiredEvent(refTestId, status, expiredAt);

        await Task.WhenAll(
            SendAsync<object>(refTestId.ToString(), evt, cancellationToken),
            SendAsync<object>(GlobalTopic, evt, cancellationToken)
        );
    }

    public async Task PublishRefTestCreatedAsync(
        Guid refTestId,
        string fullName,
        string email,
        Guid? titleId,
        string? titleValue,
        bool invitationSent,
        bool resultsSent,
        bool sendInvitationsAutomatically,
        bool sendResultsAutomatically,
        RefTestStatus status,
        int numberOfQuestions,
        int maxTimeInMinutes,
        string firstName,
        string lastName,
        DateTime createdAt,
        DateTime? scheduledAt,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestCreatedEvent(
            refTestId, fullName, email,
            titleId, titleValue,
            invitationSent, resultsSent,
            sendInvitationsAutomatically, sendResultsAutomatically,
            status, numberOfQuestions, maxTimeInMinutes,
            firstName, lastName, createdAt, scheduledAt);

        // Only publish to the global topic — there is no per-ID subscription for a brand-new ID.
        await SendAsync<object>(GlobalTopic, evt, cancellationToken);
    }

    public async Task PublishRefTestDeletedAsync(
        Guid refTestId,
        RefTestStatus status,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestDeletedEvent(refTestId, status);

        await Task.WhenAll(
            SendAsync<object>(refTestId.ToString(), evt, cancellationToken),
            SendAsync<object>(GlobalTopic, evt, cancellationToken)
        );
    }

    public async Task PublishRefTestAnonymizedAsync(
        Guid refTestId,
        RefTestStatus status,
        string fullName,
        string email,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestAnonymizedEvent(refTestId, status, fullName, email);

        await Task.WhenAll(
            SendAsync<object>(refTestId.ToString(), evt, cancellationToken),
            SendAsync<object>(GlobalTopic, evt, cancellationToken)
        );
    }

    public async Task PublishRefTestResetAsync(
        Guid refTestId,
        RefTestStatus oldStatus,
        RefTestResetType resetType,
        RefTestStatus status,
        DateTime createdAt,
        bool invitationSent,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestResetEvent(
            refTestId, oldStatus, status, resetType, createdAt, invitationSent);

        await Task.WhenAll(
            SendAsync<object>(refTestId.ToString(), evt, cancellationToken),
            SendAsync<object>(GlobalTopic, evt, cancellationToken)
        );
    }

    public async Task PublishRefTestRevivedAsync(
        Guid refTestId,
        RefTestStatus status,
        DateTime createdAt,
        bool invitationSent,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestRevivedEvent(refTestId, status, createdAt, invitationSent);

        await Task.WhenAll(
            SendAsync<object>(refTestId.ToString(), evt, cancellationToken),
            SendAsync<object>(GlobalTopic, evt, cancellationToken)
        );
    }

    public async Task PublishRefTestApprovedAsync(
        Guid refTestId,
        RefTestStatus oldStatus,
        RefTestStatus status,
        DateTime approvedAt,
        DateTime createdAt,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestApprovedEvent(refTestId, oldStatus, status, approvedAt, createdAt);

        await Task.WhenAll(
            SendAsync<object>(refTestId.ToString(), evt, cancellationToken),
            SendAsync<object>(GlobalTopic, evt, cancellationToken)
        );
    }

    public async Task PublishRefTestRejectedAsync(
        Guid refTestId,
        RefTestStatus status,
        string reason,
        DateTime rejectedAt,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestRejectedEvent(refTestId, status, reason, rejectedAt);

        await Task.WhenAll(
            SendAsync<object>(refTestId.ToString(), evt, cancellationToken),
            SendAsync<object>(GlobalTopic, evt, cancellationToken)
        );
    }
}

// Internal event records used for publishing
public record RefTestTimeExtendedEvent(Guid Id, int NewMaxTimeInMinutes, int AdditionalMinutes, DateTime ExtendedAt);

public record RefTestInvitationSentEvent(Guid Id, DateTime SentAt);

public record RefTestResultSentEvent(Guid Id, DateTime SentAt);

public record RefTestStartedEvent(Guid Id, RefTestStatus Status, DateTime StartedAt);

public record RefTestCompletedEvent(
    Guid Id,
    RefTestStatus Status,
    DateTime CompletedAt,
    int QuestionScore,
    int QuestionTotal,
    int AnswerScore,
    int AnswerTotal,
    double Percentage,
    string Language,
    IReadOnlyList<string>? SelectedAnswerIds);

public record RefTestExpiredEvent(Guid Id, RefTestStatus Status, DateTime ExpiredAt);

public record RefTestDeletedEvent(Guid Id, RefTestStatus Status);

public record RefTestAnonymizedEvent(Guid Id, RefTestStatus Status, string FullName, string Email);

public record RefTestCreatedEvent(
    Guid Id,
    string FullName,
    string Email,
    Guid? TitleId,
    string? TitleValue,
    bool InvitationSent,
    bool ResultsSent,
    bool SendInvitationsAutomatically,
    bool SendResultsAutomatically,
    RefTestStatus Status,
    int NumberOfQuestions,
    int MaxTimeInMinutes,
    string FirstName,
    string LastName,
    DateTime CreatedAt,
    DateTime? ScheduledAt);

public record RefTestResetEvent(
    Guid Id,
    RefTestStatus OldStatus,
    RefTestStatus Status,
    RefTestResetType ResetType,
    DateTime CreatedAt,
    bool InvitationSent);

public record RefTestRevivedEvent(
    Guid Id,
    RefTestStatus Status,
    DateTime CreatedAt,
    bool InvitationSent);

public record RefTestApprovedEvent(
    Guid Id,
    RefTestStatus OldStatus,
    RefTestStatus Status,
    DateTime ApprovedAt,
    DateTime CreatedAt);

public record RefTestRejectedEvent(Guid Id, RefTestStatus Status, string Reason, DateTime RejectedAt);

public enum RefTestSessionStatus { Acquired, Blocked }

public record RefTestSessionEvent(RefTestSessionStatus Status);