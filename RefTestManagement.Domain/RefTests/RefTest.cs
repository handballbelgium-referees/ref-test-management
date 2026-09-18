using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using Handball.Belgium.RefTestManagement.Domain.Events;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Domain.RefTests.Events;

namespace Handball.Belgium.RefTestManagement.Domain.RefTests;

/// <summary>
/// Who is completing a RefTest. The only thing this decides is whether the participant's time
/// limit is enforced.
/// </summary>
public enum RefTestCompletionSource
{
    /// <summary>
    /// The participant submitted the test themselves. The time limit is enforced.
    /// </summary>
    Participant,

    /// <summary>
    /// The expiration service auto-completing an overdue test with the answers already saved.
    /// It runs precisely because the deadline has passed, so enforcing the deadline here would
    /// make every overdue test permanently unfinishable.
    /// </summary>
    ExpirationService
}

public class RefTest : IHasDomainEvents, IHasParticipantIdentity
{
    /// <summary>Placeholder used to redact personal data in place — see <see cref="Anonymize"/>.</summary>
    private const string RedactedValue = "***";

    /// <summary>
    /// Length in characters of a generated invitation token. 32 hex characters is 128 bits of
    /// entropy, and matches the width of the value this used to produce
    /// (<c>Guid.NewGuid().ToString("N")</c>) so stored tokens and invitation URLs are unaffected.
    /// </summary>
    private const int TokenLength = 32;

    /// <summary>
    /// Latency and clock-skew allowance applied to the server-side deadline check. A submission
    /// is composed slightly before it arrives, and the participant's clock is not the server's;
    /// without this, an answer sent a fraction of a second before the deadline would be rejected.
    /// </summary>
    private static readonly TimeSpan DeadlineGrace = TimeSpan.FromSeconds(60);

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();
    private void RaiseDomainEvent(IDomainEvent e) => _domainEvents.Add(e);

    string? IHasParticipantIdentity.ParticipantName => FullName;
    string? IHasParticipantIdentity.ParticipantEmail => Email;

    private RefTest(
        Guid titleId,
        string firstName,
        string lastName,
        string email,
        int numberOfQuestions,
        int maxTimeInMinutes,
        List<string> questionIds,
        bool sendInvitationsAutomatically,
        bool sendResultsAutomatically)
    {
        TitleId = titleId;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        SendInvitationsAutomatically = sendInvitationsAutomatically;
        NumberOfQuestions = numberOfQuestions;
        MaxTimeInMinutes = maxTimeInMinutes;
        QuestionIds = questionIds ?? [];
        Token = GenerateToken();
        CreatedAt = DateTime.UtcNow;
        Status = RefTestStatus.Pending;
        SendResultsAutomatically = sendResultsAutomatically;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// Optimistic concurrency token. Incremented on every update by the infrastructure layer, so a
    /// save built from a stale read fails instead of silently overwriting someone else's edit.
    /// </summary>
    /// <remarks>
    /// A plain counter rather than a database-native token because the application supports SQL
    /// Server, PostgreSQL, MySQL and SQLite: <c>rowversion</c> is SQL Server only and <c>xmin</c>
    /// is PostgreSQL only, so a native token would mean four different mappings and four different
    /// migration shapes for one behaviour.
    ///
    /// Incrementing is safe against concurrent writers even though two of them may compute the
    /// same next value: EF compares the <em>original</em> value it loaded, so both would emit
    /// <c>WHERE Version = 5</c> and only one can match. It is preferred over a random token
    /// because it is half the width, orders naturally, and says something useful when read.
    /// </remarks>
    public long Version { get; private set; } = 1;

    public Guid TitleId { get; private set; }
    public RefTestTitle? Title { get; init; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }

    [NotMapped] public string FullName => $"{FirstName} {LastName}";

    public string Email { get; private set; }
    public string Token { get; private set; }
    public bool SendInvitationsAutomatically { get; private set; }
    public DateTime? InvitationSentAt { get; private set; }
    public int NumberOfQuestions { get; private set; }
    public int MaxTimeInMinutes { get; private set; }
    public List<string> QuestionIds { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? ExpiredAt { get; private set; }
    public RefTestStatus Status { get; private set; }
    public int? CurrentQuestionIndex { get; private set; }
    public int? QuestionScore { get; private set; }
    public int? AnswerScore { get; private set; }
    public int QuestionTotal => QuestionIds.Count;
    public int? AnswerTotal { get; private set; }
    public double? Percentage { get; private set; }
    public List<string> SelectedAnswerIds { get; private set; } = [];
    public List<string> WrongQuestionIds { get; private set; } = [];
    public List<string> WrongAnswerIds { get; private set; } = [];
    public bool SendResultsAutomatically { get; private set; }
    public DateTime? ResultsSentAt { get; private set; }
    public string? Language { get; private set; }
    public string? PrivacyNoticeVersion { get; private set; }
    public DateTime? PrivacyNoticeAcceptedAt { get; private set; }

    public string? RejectionReason { get; private set; }

    /// <summary>
    /// True once this RefTest's personal data (name, email, token) has been redacted as part
    /// of privacy erasure. The record itself and its audit trail are kept for accountability.
    /// </summary>
    public bool IsAnonymized { get; private set; }

    public DateTime? AnonymizedAt { get; private set; }

    /// <summary>Name of the Auth0 user who created this RefTest.</summary>
    public string CreatorName { get; private set; } = string.Empty;

    /// <summary>Email of the Auth0 user who created this RefTest.</summary>
    public string CreatorEmail { get; private set; } = string.Empty;

    /// <summary>
    /// The date/time from which this RefTest can be started. Set during approval when
    /// SendInvitationsAutomatically is true — the invitation email fires at this time.
    /// </summary>
    public DateTime? ScheduledAt { get; private set; }

    [NotMapped]
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;

    public static RefTest Create(
        Guid titleId,
        string firstName,
        string lastName,
        string email,
        int numberOfQuestions,
        int maxTimeInMinutes,
        List<string> questionIds,
        bool sendInvitationAutomatically,
        bool sendResultsAutomatically,
        bool requiresApproval = false,
        DateTime? scheduledAt = null,
        string creatorName = "",
        string creatorEmail = "")
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required", nameof(firstName));

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required", nameof(lastName));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));

        if (numberOfQuestions <= 0)
            throw new ArgumentException("Number of questions must be greater than 0", nameof(numberOfQuestions));

        if (maxTimeInMinutes <= 0)
            throw new ArgumentException("Max time must be greater than 0", nameof(maxTimeInMinutes));

        var refTest = new RefTest(titleId, firstName, lastName, email, numberOfQuestions, maxTimeInMinutes,
            questionIds, sendInvitationAutomatically, sendResultsAutomatically)
        {
            Status = requiresApproval ? RefTestStatus.PendingApproval : RefTestStatus.Pending,
            ScheduledAt = scheduledAt,
            CreatorName = creatorName,
            CreatorEmail = creatorEmail
        };

        refTest.RaiseDomainEvent(new RefTestCreatedEvent(
            firstName, lastName, email,
            TitleId: titleId,
            numberOfQuestions, maxTimeInMinutes,
            sendInvitationAutomatically, sendResultsAutomatically,
            requiresApproval));

        return refTest;
    }

    public void Approve()
    {
        if (IsAnonymized)
            throw new InvalidRefTestStatusException("Cannot approve a RefTest whose consent has been withdrawn");

        if (Status != RefTestStatus.PendingApproval && Status != RefTestStatus.Rejected)
            throw new InvalidRefTestStatusException(
                "Only RefTests in PendingApproval or Rejected status can be approved");

        Status = RefTestStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        RejectionReason = null;
        // ScheduledAt is preserved as set by the creator
        RaiseDomainEvent(new RefTestApprovedEvent());
    }

    public void Reject(string reason)
    {
        if (IsAnonymized)
            throw new InvalidRefTestStatusException("Cannot reject a RefTest whose consent has been withdrawn");

        if (Status != RefTestStatus.PendingApproval)
            throw new InvalidRefTestStatusException(
                "Only RefTests in PendingApproval status can be rejected");

        if (string.IsNullOrWhiteSpace(reason))
            throw new RefTestValidationException("Rejection reason is required");

        Status = RefTestStatus.Rejected;
        RejectionReason = reason;
        RaiseDomainEvent(new RefTestRejectedEvent(reason));
    }

    public void SendInvitation()
    {
        InvitationSentAt = DateTime.UtcNow;
        RaiseDomainEvent(new RefTestInvitationSentEvent());
    }

    public void AcceptPrivacyNotice(string noticeVersion)
    {
        if (string.IsNullOrWhiteSpace(noticeVersion))
            throw new RefTestValidationException("Privacy notice version is required");

        PrivacyNoticeVersion = noticeVersion;
        PrivacyNoticeAcceptedAt = DateTime.UtcNow;

        RaiseDomainEvent(new RefTestPrivacyNoticeAcceptedEvent(noticeVersion, FirstName, LastName, Email));
    }

    public void Start(string requiredPrivacyNoticeVersion)
    {
        if (IsAnonymized)
            throw new InvalidRefTestStatusException("Cannot start a RefTest whose consent has been withdrawn");

        if (Status != RefTestStatus.Pending)
            throw new InvalidRefTestStatusException("RefTest can only be started from Pending status");

        if (ScheduledAt.HasValue && ScheduledAt.Value > DateTime.UtcNow)
            throw new RefTestValidationException(
                $"This ref test is not yet available. It can be started from {ScheduledAt.Value:yyyy-MM-dd HH:mm} UTC");

        if (PrivacyNoticeVersion != requiredPrivacyNoticeVersion || !PrivacyNoticeAcceptedAt.HasValue)
            throw new RefTestValidationException("The current privacy notice must be accepted before starting");

        Status = RefTestStatus.InProgress;
        StartedAt = DateTime.UtcNow;
        CurrentQuestionIndex = 0;

        RaiseDomainEvent(new RefTestStartedEvent(FirstName, LastName, Email));
    }

    public void SaveProgress(int currentQuestionIndex, List<string> selectedAnswerIds, string? language = null)
    {
        if (IsAnonymized)
            throw new InvalidRefTestStatusException("Cannot save progress for a RefTest whose consent has been withdrawn");

        if (Status != RefTestStatus.InProgress)
            throw new InvalidRefTestStatusException("Can only save progress for in-progress RefTests");

        if (HasPassedDeadline())
            throw new InvalidRefTestStatusException("The time limit for this RefTest has passed");

        CurrentQuestionIndex = currentQuestionIndex;
        SelectedAnswerIds = selectedAnswerIds ?? [];
        Language = language;
    }

    public void Complete(int questionScore, int answerScore, int answerTotal, double percentage,
        List<string> selectedAnswerIds, List<string> wrongQuestionIds, List<string> wrongAnswerIds,
        string? language = null,
        RefTestCompletionSource source = RefTestCompletionSource.Participant)
    {
        if (IsAnonymized)
            throw new InvalidRefTestStatusException("Cannot complete a RefTest whose consent has been withdrawn");

        if (Status != RefTestStatus.InProgress)
            throw new InvalidRefTestStatusException("Can only complete in-progress RefTests");

        if (source == RefTestCompletionSource.Participant && HasPassedDeadline())
            throw new InvalidRefTestStatusException("The time limit for this RefTest has passed");

        Status = RefTestStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        QuestionScore = questionScore;
        AnswerScore = answerScore;
        AnswerTotal = answerTotal;
        Percentage = percentage;
        SelectedAnswerIds = selectedAnswerIds;
        WrongQuestionIds = wrongQuestionIds;
        WrongAnswerIds = wrongAnswerIds;
        Language = language;

        RaiseDomainEvent(new RefTestCompletedEvent(
            questionScore, answerScore, answerTotal, percentage,
            FirstName, LastName, Email));
    }

    public void SendResults()
    {
        ResultsSentAt = DateTime.UtcNow;
        RaiseDomainEvent(new RefTestResultsSentEvent());
    }

    public void Expire()
    {
        if (IsAnonymized)
            throw new InvalidRefTestStatusException("Cannot expire a RefTest whose consent has been withdrawn");

        if (Status != RefTestStatus.Pending)
            throw new InvalidRefTestStatusException("Only pending RefTests can be marked as expired");

        Status = RefTestStatus.Expired;
        ExpiredAt = DateTime.UtcNow;
        RaiseDomainEvent(new RefTestExpiredEvent());
    }

    public bool IsExpired(TimeSpan expirationIfNotStarted)
    {
        if (Status == RefTestStatus.Completed)
            return false;

        if (StartedAt.HasValue)
        {
            // In-progress tests that exceed the time limit are auto-completed, not expired
            var elapsedTime = DateTime.UtcNow - StartedAt.Value;
            return elapsedTime.TotalMinutes > MaxTimeInMinutes;
        }

        // RefTest expires after configured time if not started (Pending status only)
        var timeSinceCreation = DateTime.UtcNow - CreatedAt;
        return timeSinceCreation > expirationIfNotStarted;
    }

    #region Update Methods

    public void UpdateBasicDetails(string firstName, string lastName, string email)
    {
        if (IsAnonymized)
            throw new InvalidRefTestStatusException("Cannot update details for a RefTest whose consent has been withdrawn");

        if (!CanUpdateBasicDetails())
            throw new InvalidRefTestStatusException(
                "Cannot update participant details for in-progress or completed tests");

        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required", nameof(lastName));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));

        var oldFirstName = FirstName;
        var oldLastName = LastName;
        var oldEmail = Email;

        FirstName = firstName;
        LastName = lastName;
        Email = email;

        RaiseDomainEvent(new RefTestDetailsUpdatedEvent(
            oldFirstName, firstName,
            oldLastName, lastName,
            oldEmail, email));
    }

    public void UpdateTestConfiguration(
        Guid titleId,
        int numberOfQuestions,
        int maxTimeInMinutes,
        List<string> questionIds)
    {
        if (IsAnonymized)
            throw new InvalidRefTestStatusException("Cannot update configuration for a RefTest whose consent has been withdrawn");

        if (!CanUpdateTestConfiguration())
            throw new InvalidRefTestStatusException(
                "Cannot update test configuration for in-progress or completed tests");

        if (numberOfQuestions <= 0)
            throw new ArgumentException("Number of questions must be greater than 0", nameof(numberOfQuestions));
        if (maxTimeInMinutes <= 0)
            throw new ArgumentException("Max time must be greater than 0", nameof(maxTimeInMinutes));

        var oldNumberOfQuestions = NumberOfQuestions;
        var oldMaxTimeInMinutes = MaxTimeInMinutes;
        var oldTitleId = TitleId;
        var oldQuestionPoolCount = QuestionIds.Count;

        TitleId = titleId;
        NumberOfQuestions = numberOfQuestions;
        MaxTimeInMinutes = maxTimeInMinutes;
        QuestionIds = questionIds ?? [];

        RaiseDomainEvent(new RefTestConfigurationUpdatedEvent(
            OldTitleId: oldTitleId,
            NewTitleId: titleId,
            oldNumberOfQuestions, numberOfQuestions,
            oldMaxTimeInMinutes, maxTimeInMinutes,
            oldQuestionPoolCount, QuestionIds.Count));
    }

    public void ExtendTime(int additionalMinutes)
    {
        if (IsAnonymized)
            throw new InvalidRefTestStatusException("Cannot extend time for a RefTest whose consent has been withdrawn");

        if (!CanExtendTime())
            throw new InvalidRefTestStatusException("Can only extend time for in-progress tests");

        if (additionalMinutes <= 0)
            throw new ArgumentException("Additional minutes must be greater than 0", nameof(additionalMinutes));

        var oldMax = MaxTimeInMinutes;
        MaxTimeInMinutes += additionalMinutes;
        RaiseDomainEvent(new RefTestTimeExtendedEvent(additionalMinutes, oldMax, MaxTimeInMinutes));
    }

    public void UpdateNotificationSettings(
        bool? sendInvitationsAutomatically = null,
        bool? sendResultsAutomatically = null)
    {
        var oldSendInvite = SendInvitationsAutomatically;
        var oldSendResults = SendResultsAutomatically;

        // Can update sendInvitationsAutomatically only if pending and invitation not yet sent
        if (sendInvitationsAutomatically.HasValue &&
            !IsAnonymized &&
            Status == RefTestStatus.Pending &&
            !InvitationSentAt.HasValue)
        {
            SendInvitationsAutomatically = sendInvitationsAutomatically.Value;
        }

        // Can update sendResultsAutomatically only if results not yet sent and not expired
        if (sendResultsAutomatically.HasValue &&
            !IsAnonymized &&
            Status != RefTestStatus.Expired &&
            !ResultsSentAt.HasValue)
        {
            SendResultsAutomatically = sendResultsAutomatically.Value;
        }

        RaiseDomainEvent(new RefTestNotificationSettingsUpdatedEvent(
            oldSendInvite, SendInvitationsAutomatically,
            oldSendResults, SendResultsAutomatically));
    }

    public void RegenerateToken()
    {
        if (IsAnonymized)
            throw new InvalidRefTestStatusException("Cannot regenerate token for a RefTest whose consent has been withdrawn");

        if (!CanRegenerateToken())
            throw new InvalidRefTestStatusException(
                "Can only regenerate token for pending or expired tests");

        Token = GenerateToken();

        // Clear invitation sent flag so a new invitation will be sent with the new token
        if (InvitationSentAt.HasValue)
        {
            InvitationSentAt = null;
        }

        RaiseDomainEvent(new RefTestTokenRegeneratedEvent());
    }

    #endregion

    #region Reset Methods

    public void SoftReset(bool regenerateToken = false)
    {
        if (IsAnonymized)
            throw new InvalidRefTestStatusException("Cannot reset a RefTest whose consent has been withdrawn");

        if (Status != RefTestStatus.InProgress && Status != RefTestStatus.Completed)
            throw new InvalidRefTestStatusException(
                "Cannot reset a pending or expired test - it's already in initial state when state is pending, when expired use revive instead");

        // Clear progress data
        Status = RefTestStatus.Pending;
        StartedAt = null;
        CompletedAt = null;
        CurrentQuestionIndex = null;

        // Clear results
        QuestionScore = null;
        AnswerScore = null;
        AnswerTotal = null;
        Percentage = null;
        SelectedAnswerIds = [];
        WrongQuestionIds = [];
        WrongAnswerIds = [];
        ResultsSentAt = null;
        Language = null;

        if (!regenerateToken)
        {
            RaiseDomainEvent(new RefTestSoftResetEvent(TokenRegenerated: false));
            return;
        }

        // Optionally regenerate token
        Token = GenerateToken();

        // Clear invitation sent flag so a new invitation will be sent with the new token
        InvitationSentAt = null;

        // Keep: CreatedAt (for audit trail)
        // Note: InvitationSentAt is cleared if the token is regenerated, preserved otherwise
        RaiseDomainEvent(new RefTestSoftResetEvent(TokenRegenerated: true));
    }

    public void HardReset()
    {
        if (IsAnonymized)
            throw new InvalidRefTestStatusException("Cannot hard reset a RefTest whose consent has been withdrawn");

        if (Status != RefTestStatus.InProgress && Status != RefTestStatus.Completed)
            throw new InvalidRefTestStatusException(
                "Cannot hard reset a pending or expired test - use update operations instead when status is pending, when expired use revive instead");

        // Clear all progress and history
        Status = RefTestStatus.Pending;
        StartedAt = null;
        CompletedAt = null;
        CurrentQuestionIndex = null;
        InvitationSentAt = null;
        ResultsSentAt = null;

        // Clear results
        QuestionScore = null;
        AnswerScore = null;
        AnswerTotal = null;
        Percentage = null;
        SelectedAnswerIds = [];
        WrongQuestionIds = [];
        WrongAnswerIds = [];
        Language = null;

        // Reset timestamps - IMPORTANT: This resets the expiration timer for the RefTestExpirationService
        CreatedAt = DateTime.UtcNow;

        // Always regenerate token for security
        Token = GenerateToken();
        RaiseDomainEvent(new RefTestHardResetEvent());
    }

    public void Revive()
    {
        if (IsAnonymized)
            throw new InvalidRefTestStatusException("Cannot revive a RefTest whose consent has been withdrawn");

        if (Status != RefTestStatus.Expired)
            throw new InvalidRefTestStatusException("Can only revive expired tests");

        Status = RefTestStatus.Pending;
        Token = GenerateToken();
        ExpiredAt = null;

        // Reset CreatedAt so the expiration timer starts fresh
        CreatedAt = DateTime.UtcNow;

        // Clear invitation sent flag so a new invitation will be sent with the new token
        InvitationSentAt = null;
        RaiseDomainEvent(new RefTestRevivedEvent());
    }

    /// <summary>
    /// Records, purely for the audit trail, that this RefTest was explicitly deleted by staff
    /// (a "RefTestDeleted" event, distinct from "RefTestAnonymized"). Raise this immediately
    /// before actually removing the record from the database — the audit trail itself is kept
    /// and expires on its own per the normal retention schedule, since it carries no reference
    /// back to the (now-gone) row.
    /// </summary>
    public void MarkDeleted()
    {
        RaiseDomainEvent(new RefTestDeletedEvent());
    }

    /// <summary>
    /// Irreversibly redacts personal data (name, email, token) in place while preserving the
    /// RefTest record and its audit trail for accountability — used by the public self-service
    /// "withdraw consent" flow, which (unlike a staff delete) never removes the record itself.
    /// Idempotent — calling it again once already anonymized is a no-op.
    /// </summary>
    public void Anonymize()
    {
        if (IsAnonymized)
            return;

        FirstName = RedactedValue;
        LastName = RedactedValue;
        Email = RedactedValue;
        // Token must stay unique (unique index) — a random placeholder still hides the real
        // token value while satisfying that constraint, unlike a fixed "***" for every RefTest.
        Token = $"erased-{Guid.NewGuid():N}";
        // PrivacyNoticeVersion and PrivacyNoticeAcceptedAt are deliberately retained. GDPR
        // Art. 7(1) requires the controller to be able to demonstrate that consent was given;
        // neither field identifies the participant once name, email and token are erased, so
        // keeping them costs nothing in privacy terms and preserves the evidence.
        IsAnonymized = true;
        AnonymizedAt = DateTime.UtcNow;

        RaiseDomainEvent(new RefTestAnonymizedEvent());
    }

    #endregion

    #region Validation Helpers

    private bool CanUpdateBasicDetails() =>
        Status != RefTestStatus.InProgress && Status != RefTestStatus.Completed;

    private bool CanUpdateTestConfiguration() =>
        Status != RefTestStatus.InProgress && Status != RefTestStatus.Completed;

    private bool CanExtendTime() =>
        Status == RefTestStatus.InProgress;

    private bool CanRegenerateToken() =>
        Status == RefTestStatus.Pending;

    /// <summary>
    /// True once the participant's time limit has elapsed, including any admin extension
    /// (<see cref="ExtendTime"/> raises <see cref="MaxTimeInMinutes"/> in place) plus
    /// <see cref="DeadlineGrace"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="RefTestStatus.InProgress"/> on its own does not mean the test is still open:
    /// overdue tests are only closed when the expiration service next runs, so without this check
    /// a participant can keep answering during the gap between the deadline and that sweep.
    /// </remarks>
    public bool HasPassedDeadline() =>
        StartedAt.HasValue &&
        DateTime.UtcNow > StartedAt.Value.AddMinutes(MaxTimeInMinutes).Add(DeadlineGrace);

    /// <summary>
    /// Generates an invitation token. Tokens are the only credential guarding a participant's
    /// personal data and results, so they come from a cryptographic RNG — a GUID is unique but
    /// makes no secrecy guarantee.
    /// </summary>
    private static string GenerateToken() =>
        RandomNumberGenerator.GetHexString(TokenLength, lowercase: true);

    #endregion
}