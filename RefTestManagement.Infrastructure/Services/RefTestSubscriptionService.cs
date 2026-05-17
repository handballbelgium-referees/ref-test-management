using Handball.Belgium.RefTestManagement.Domain.RefTests;
using HotChocolate.Subscriptions;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

/// <summary>
/// Service for publishing RefTest subscription events
/// </summary>
public interface IRefTestSubscriptionService
{
    /// <summary>
    /// Publish an event when a RefTest time is extended
    /// </summary>
    Task PublishTimeExtendedAsync(
        Guid refTestId,
        int newMaxTimeInMinutes,
        int additionalMinutes,
        DateTime extendedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an event when a RefTest invitation is sent
    /// </summary>
    Task PublishInvitationSentAsync(
        Guid refTestId,
        DateTime sentAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an event when RefTest results are sent
    /// </summary>
    Task PublishResultSentAsync(
        Guid refTestId,
        DateTime sentAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an event when a RefTest is started
    /// </summary>
    Task PublishRefTestStartedAsync(
        Guid refTestId,
        RefTestStatus status,
        DateTime startedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an event when a RefTest is completed
    /// </summary>
    Task PublishRefTestCompletedAsync(
        Guid refTestId,
        RefTestStatus status,
        DateTime completedAt,
        int questionScore,
        int questionTotal,
        int answerScore,
        int answerTotal,
        double percentage,
        string language,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an event when a RefTest expires
    /// </summary>
    Task PublishRefTestExpiredAsync(
        Guid refTestId,
        RefTestStatus status,
        DateTime expiredAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an event when a new RefTest is created
    /// </summary>
    Task PublishRefTestCreatedAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an event when a RefTest is deleted
    /// </summary>
    Task PublishRefTestDeletedAsync(
        Guid refTestId,
        RefTestStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an event when a RefTest is reset (soft or hard) back to Pending
    /// </summary>
    Task PublishRefTestResetAsync(
        Guid refTestId,
        RefTestStatus oldStatus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an event when an expired RefTest is revived back to Pending
    /// </summary>
    Task PublishRefTestRevivedAsync(
        Guid refTestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an event when a RefTest is approved
    /// </summary>
    Task PublishRefTestApprovedAsync(
        Guid refTestId,
        RefTestStatus status,
        DateTime approvedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an event when a RefTest is rejected
    /// </summary>
    Task PublishRefTestRejectedAsync(
        Guid refTestId,
        RefTestStatus status,
        string reason,
        DateTime rejectedAt,
        CancellationToken cancellationToken = default);
}

public class RefTestSubscriptionService(ITopicEventSender eventSender) : IRefTestSubscriptionService
{
    public const string GlobalTopic = "RefTestEvents";
    public const string TimeExtendedTopic = "RefTestTimeExtended-{id}";

    public async Task PublishTimeExtendedAsync(
        Guid refTestId,
        int newMaxTimeInMinutes,
        int additionalMinutes,
        DateTime extendedAt,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestTimeExtendedEvent(refTestId, newMaxTimeInMinutes, additionalMinutes, extendedAt);

        await eventSender.SendAsync(TimeExtendedTopic.Replace("{id}", refTestId.ToString()), evt, cancellationToken);
    }

    public async Task PublishInvitationSentAsync(
        Guid refTestId,
        DateTime sentAt,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestInvitationSentEvent(refTestId, sentAt);

        await Task.WhenAll(
            eventSender.SendAsync<object>(refTestId.ToString(), evt, cancellationToken).AsTask(),
            eventSender.SendAsync<object>(GlobalTopic, evt, cancellationToken).AsTask()
        );
    }

    public async Task PublishResultSentAsync(
        Guid refTestId,
        DateTime sentAt,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestResultSentEvent(refTestId, sentAt);

        await Task.WhenAll(
            eventSender.SendAsync<object>(refTestId.ToString(), evt, cancellationToken).AsTask(),
            eventSender.SendAsync<object>(GlobalTopic, evt, cancellationToken).AsTask()
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
            eventSender.SendAsync<object>(refTestId.ToString(), evt, cancellationToken).AsTask(),
            eventSender.SendAsync<object>(GlobalTopic, evt, cancellationToken).AsTask()
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
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestCompletedEvent(refTestId, status, completedAt, questionScore, questionTotal, answerScore,
            answerTotal, percentage, language);

        await Task.WhenAll(
            eventSender.SendAsync<object>(refTestId.ToString(), evt, cancellationToken).AsTask(),
            eventSender.SendAsync<object>(GlobalTopic, evt, cancellationToken).AsTask()
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
            eventSender.SendAsync<object>(refTestId.ToString(), evt, cancellationToken).AsTask(),
            eventSender.SendAsync<object>(GlobalTopic, evt, cancellationToken).AsTask()
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
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestCreatedEvent(
            refTestId, fullName, email,
            titleId, titleValue,
            invitationSent, resultsSent,
            sendInvitationsAutomatically, sendResultsAutomatically,
            status, numberOfQuestions, maxTimeInMinutes);

        // Only publish to the global topic — there is no per-ID subscription for a brand-new ID.
        await eventSender.SendAsync<object>(GlobalTopic, evt, cancellationToken);
    }

    public async Task PublishRefTestDeletedAsync(
        Guid refTestId,
        RefTestStatus status,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestDeletedEvent(refTestId, status);

        await Task.WhenAll(
            eventSender.SendAsync<object>(refTestId.ToString(), evt, cancellationToken).AsTask(),
            eventSender.SendAsync<object>(GlobalTopic, evt, cancellationToken).AsTask()
        );
    }

    public async Task PublishRefTestResetAsync(
        Guid refTestId,
        RefTestStatus oldStatus,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestResetEvent(refTestId, oldStatus);

        await Task.WhenAll(
            eventSender.SendAsync<object>(refTestId.ToString(), evt, cancellationToken).AsTask(),
            eventSender.SendAsync<object>(GlobalTopic, evt, cancellationToken).AsTask()
        );
    }

    public async Task PublishRefTestRevivedAsync(
        Guid refTestId,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestRevivedEvent(refTestId);

        await Task.WhenAll(
            eventSender.SendAsync<object>(refTestId.ToString(), evt, cancellationToken).AsTask(),
            eventSender.SendAsync<object>(GlobalTopic, evt, cancellationToken).AsTask()
        );
    }

    public async Task PublishRefTestApprovedAsync(
        Guid refTestId,
        RefTestStatus status,
        DateTime approvedAt,
        CancellationToken cancellationToken = default)
    {
        var evt = new RefTestApprovedEvent(refTestId, status, approvedAt);

        await Task.WhenAll(
            eventSender.SendAsync<object>(refTestId.ToString(), evt, cancellationToken).AsTask(),
            eventSender.SendAsync<object>(GlobalTopic, evt, cancellationToken).AsTask()
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
            eventSender.SendAsync<object>(refTestId.ToString(), evt, cancellationToken).AsTask(),
            eventSender.SendAsync<object>(GlobalTopic, evt, cancellationToken).AsTask()
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
    string Language);

public record RefTestExpiredEvent(Guid Id, RefTestStatus Status, DateTime ExpiredAt);

public record RefTestDeletedEvent(Guid Id, RefTestStatus Status);

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
    int MaxTimeInMinutes);

public record RefTestResetEvent(Guid Id, RefTestStatus OldStatus);

public record RefTestRevivedEvent(Guid Id);

public record RefTestApprovedEvent(Guid Id, RefTestStatus Status, DateTime ApprovedAt);

public record RefTestRejectedEvent(Guid Id, RefTestStatus Status, string Reason, DateTime RejectedAt);

public enum RefTestSessionStatus { Acquired, Blocked }

public record RefTestSessionEvent(RefTestSessionStatus Status);