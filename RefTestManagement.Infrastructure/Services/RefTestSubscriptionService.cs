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