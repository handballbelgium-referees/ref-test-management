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
    public async Task PublishTimeExtendedAsync(
        Guid refTestId,
        int newMaxTimeInMinutes,
        int additionalMinutes,
        DateTime extendedAt,
        CancellationToken cancellationToken = default)
    {
        await eventSender.SendAsync(
            refTestId.ToString(),
            new RefTestTimeExtendedEvent(refTestId, newMaxTimeInMinutes, additionalMinutes, extendedAt),
            cancellationToken);
    }

    public async Task PublishInvitationSentAsync(
        Guid refTestId,
        DateTime sentAt,
        CancellationToken cancellationToken = default)
    {
        await eventSender.SendAsync(
            refTestId.ToString(),
            new RefTestInvitationSentEvent(refTestId, sentAt),
            cancellationToken);
    }

    public async Task PublishResultSentAsync(
        Guid refTestId,
        DateTime sentAt,
        CancellationToken cancellationToken = default)
    {
        await eventSender.SendAsync(
            refTestId.ToString(),
            new RefTestResultSentEvent(refTestId, sentAt),
            cancellationToken);
    }

    public async Task PublishRefTestStartedAsync(
        Guid refTestId,
        RefTestStatus status,
        DateTime startedAt,
        CancellationToken cancellationToken = default)
    {
        await eventSender.SendAsync(
            refTestId.ToString(),
            new RefTestStartedEvent(refTestId, status, startedAt),
            cancellationToken);
    }

    public async Task PublishRefTestCompletedAsync(
        Guid refTestId,
        RefTestStatus status,
        DateTime completedAt,
        CancellationToken cancellationToken = default)
    {
        await eventSender.SendAsync(
            refTestId.ToString(),
            new RefTestCompletedEvent(refTestId, status, completedAt),
            cancellationToken);
    }

    public async Task PublishRefTestExpiredAsync(
        Guid refTestId,
        RefTestStatus status,
        DateTime expiredAt,
        CancellationToken cancellationToken = default)
    {
        await eventSender.SendAsync(
            refTestId.ToString(),
            new RefTestExpiredEvent(refTestId, status, expiredAt),
            cancellationToken);
    }
}

// Internal event records used for publishing
public record RefTestTimeExtendedEvent(Guid Id, int NewMaxTimeInMinutes, int AdditionalMinutes, DateTime ExtendedAt);
public record RefTestInvitationSentEvent(Guid Id, DateTime SentAt);
public record RefTestResultSentEvent(Guid Id, DateTime SentAt);
public record RefTestStartedEvent(Guid Id, RefTestStatus Status, DateTime StartedAt);
public record RefTestCompletedEvent(Guid Id, RefTestStatus Status, DateTime CompletedAt);
public record RefTestExpiredEvent(Guid Id, RefTestStatus Status, DateTime ExpiredAt);
