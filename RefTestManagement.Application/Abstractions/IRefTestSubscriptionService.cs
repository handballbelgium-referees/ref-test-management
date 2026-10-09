using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

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
        IReadOnlyList<string> selectedAnswerIds,
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
        string firstName,
        string lastName,
        DateTime createdAt,
        DateTime? scheduledAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an event when a RefTest is deleted
    /// </summary>
    Task PublishRefTestDeletedAsync(
        Guid refTestId,
        RefTestStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an event when a RefTest is anonymized (privacy erasure/consent withdrawal).
    /// Unlike deletion, the record itself is kept in redacted form.
    /// </summary>
    Task PublishRefTestAnonymizedAsync(
        Guid refTestId,
        RefTestStatus status,
        string fullName,
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an event when a RefTest is reset (soft or hard) back to Pending
    /// </summary>
    Task PublishRefTestResetAsync(
        Guid refTestId,
        RefTestStatus oldStatus,
        RefTestResetType resetType,
        RefTestStatus status,
        DateTime createdAt,
        bool invitationSent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an event when an expired RefTest is revived back to Pending
    /// </summary>
    Task PublishRefTestRevivedAsync(
        Guid refTestId,
        RefTestStatus status,
        DateTime createdAt,
        bool invitationSent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an event when a RefTest is approved
    /// </summary>
    Task PublishRefTestApprovedAsync(
        Guid refTestId,
        RefTestStatus oldStatus,
        RefTestStatus status,
        DateTime approvedAt,
        DateTime createdAt,
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
