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

    public void Complete(int questionScore, int answerScore, int answerTotal, double percentage, List<string> selectedAnswerIds, List<string> wrongQuestionIds, List<string> wrongAnswerIds, string? language = null)
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
        if (Status == RefTestStatus.Completed)
            return;

        Status = RefTestStatus.Expired;
    }

    public bool IsExpired(TimeSpan expirationIfNotStarted)
    {
        if (Status == RefTestStatus.Completed)
            return false;

        if (StartedAt.HasValue)
        {
            var elapsedTime = DateTime.UtcNow - StartedAt.Value;
            return elapsedTime.TotalMinutes > MaxTimeInMinutes;
        }

        // RefTest expires after configured time if not started
        var timeSinceCreation = DateTime.UtcNow - CreatedAt;
        return timeSinceCreation > expirationIfNotStarted;
    }
}