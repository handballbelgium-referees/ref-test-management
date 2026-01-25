using System.ComponentModel.DataAnnotations.Schema;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;

namespace Handball.Belgium.RefTestManagement.Domain.RefTests;

public class RefTest
{
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
        Token = Guid.NewGuid().ToString("N");
        CreatedAt = DateTime.UtcNow;
        Status = RefTestStatus.Pending;
        SendResultsAutomatically = sendResultsAutomatically;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

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
        bool sendResultsAutomatically)
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

        return new RefTest(titleId, firstName, lastName, email, numberOfQuestions, maxTimeInMinutes,
            questionIds, sendInvitationAutomatically, sendResultsAutomatically);
    }

    public void SendInvitation()
    {
        InvitationSentAt = DateTime.UtcNow;
    }

    public void Start()
    {
        if (Status != RefTestStatus.Pending)
            throw new InvalidRefTestStatusException("RefTest can only be started from Pending status");

        Status = RefTestStatus.InProgress;
        StartedAt = DateTime.UtcNow;
        CurrentQuestionIndex = 0;
    }

    public void SaveProgress(int currentQuestionIndex, List<string> selectedAnswerIds, string? language = null)
    {
        if (Status != RefTestStatus.InProgress)
            throw new InvalidRefTestStatusException("Can only save progress for in-progress RefTests");

        CurrentQuestionIndex = currentQuestionIndex;
        SelectedAnswerIds = selectedAnswerIds ?? [];
        Language = language;
    }

    public void Complete(int questionScore, int answerScore, int answerTotal, double percentage,
        List<string> selectedAnswerIds, List<string> wrongQuestionIds, List<string> wrongAnswerIds,
        string? language = null)
    {
        if (Status != RefTestStatus.InProgress)
            throw new InvalidRefTestStatusException("Can only complete in-progress RefTests");

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
    }

    public void SendResults()
    {
        ResultsSentAt = DateTime.UtcNow;
    }

    public void Expire()
    {
        if (Status != RefTestStatus.Pending)
            throw new InvalidRefTestStatusException("Only pending RefTests can be marked as expired");

        Status = RefTestStatus.Expired;
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
        if (!CanUpdateBasicDetails())
            throw new InvalidRefTestStatusException(
                "Cannot update participant details for in-progress or completed tests");

        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required", nameof(lastName));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));

        FirstName = firstName;
        LastName = lastName;
        Email = email;
    }

    public void UpdateTestConfiguration(
        Guid titleId,
        int numberOfQuestions,
        int maxTimeInMinutes,
        List<string> questionIds)
    {
        if (!CanUpdateTestConfiguration())
            throw new InvalidRefTestStatusException(
                "Cannot update test configuration for in-progress or completed tests");

        if (numberOfQuestions <= 0)
            throw new ArgumentException("Number of questions must be greater than 0", nameof(numberOfQuestions));
        if (maxTimeInMinutes <= 0)
            throw new ArgumentException("Max time must be greater than 0", nameof(maxTimeInMinutes));

        TitleId = titleId;
        NumberOfQuestions = numberOfQuestions;
        MaxTimeInMinutes = maxTimeInMinutes;
        QuestionIds = questionIds ?? [];
    }

    public void ExtendTime(int additionalMinutes)
    {
        if (!CanExtendTime())
            throw new InvalidRefTestStatusException("Can only extend time for in-progress tests");

        if (additionalMinutes <= 0)
            throw new ArgumentException("Additional minutes must be greater than 0", nameof(additionalMinutes));

        MaxTimeInMinutes += additionalMinutes;
    }

    public void UpdateNotificationSettings(
        bool? sendInvitationsAutomatically = null,
        bool? sendResultsAutomatically = null)
    {
        // Can update sendInvitationsAutomatically only if pending and invitation not yet sent
        if (sendInvitationsAutomatically.HasValue &&
            Status == RefTestStatus.Pending &&
            !InvitationSentAt.HasValue)
        {
            SendInvitationsAutomatically = sendInvitationsAutomatically.Value;
        }

        // Can update sendResultsAutomatically only if results not yet sent and not expired
        if (sendResultsAutomatically.HasValue &&
            Status != RefTestStatus.Expired &&
            !ResultsSentAt.HasValue)
        {
            SendResultsAutomatically = sendResultsAutomatically.Value;
        }
    }

    public void RegenerateToken()
    {
        if (!CanRegenerateToken())
            throw new InvalidRefTestStatusException(
                "Can only regenerate token for pending or expired tests");

        Token = Guid.NewGuid().ToString("N");

        // Clear invitation sent flag so a new invitation will be sent with the new token
        if (InvitationSentAt.HasValue)
        {
            InvitationSentAt = null;
        }
    }

    #endregion

    #region Reset Methods

    public void SoftReset(bool regenerateToken = false)
    {
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
            return;

        // Optionally regenerate token
        Token = Guid.NewGuid().ToString("N");

        // Clear invitation sent flag so a new invitation will be sent with the new token
        InvitationSentAt = null;

        // Keep: CreatedAt (for audit trail)
        // Note: InvitationSentAt is cleared if the token is regenerated, preserved otherwise
    }

    public void HardReset()
    {
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
        Token = Guid.NewGuid().ToString("N");
 }

    public void Revive()
    {
        if (Status != RefTestStatus.Expired)
            throw new InvalidRefTestStatusException("Can only revive expired tests");

        Status = RefTestStatus.Pending;
        Token = Guid.NewGuid().ToString("N");

        // Reset CreatedAt so the expiration timer starts fresh
        CreatedAt = DateTime.UtcNow;

        // Clear invitation sent flag so a new invitation will be sent with the new token
        InvitationSentAt = null;
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

    #endregion
}