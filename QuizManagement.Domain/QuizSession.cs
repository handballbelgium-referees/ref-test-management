namespace Handball.Belgium.Rules.Quiz.Domain;

public class QuizSession
{
    private QuizSession(
        Guid titleId,
        string firstName,
        string lastName,
        string email,
        int numberOfQuestions,
        int maxTimeInMinutes,
        List<string> questionIds)
    {
        TitleId = titleId;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        NumberOfQuestions = numberOfQuestions;
        MaxTimeInMinutes = maxTimeInMinutes;
        QuestionIds = questionIds ?? [];
        Token = Guid.NewGuid().ToString("N");
        CreatedAt = DateTime.UtcNow;
        Status = QuizSessionStatus.Pending;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    
    public Guid TitleId { get; private set; }
    public QuizTitle? Title { get; init; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string Email { get; private set; }
    public string Token { get; private set; }
    public bool InvitationSent { get; private set; }
    public int NumberOfQuestions { get; private set; }
    public int MaxTimeInMinutes { get; private set; }
    public List<string> QuestionIds { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public QuizSessionStatus Status { get; private set; }
    public int? Score { get; private set; }
    public int? TotalQuestions { get; private set; }
    public double? Percentage { get; private set; }
    public List<string> WrongQuestionIds { get; private set; } = [];
    public List<string> WrongAnswerIds { get; private set; } = [];

    public static QuizSession Create(
        Guid titleId,
        string firstName,
        string lastName,
        string email,
        int numberOfQuestions,
        int maxTimeInMinutes,
        List<string> questionIds)
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

        return new QuizSession(titleId, firstName, lastName, email, numberOfQuestions, maxTimeInMinutes,
            questionIds);
    }

    public void SendInvitation()
    {
        InvitationSent = true;
    }

    public void StartSession()
    {
        if (Status != QuizSessionStatus.Pending)
            throw new InvalidQuizSessionStatusException("Quiz session can only be started from Pending status");

        Status = QuizSessionStatus.InProgress;
        StartedAt = DateTime.UtcNow;
    }

    public void CompleteSession(int score, int totalQuestions, double percentage, List<string> wrongQuestionIds,
        List<string> wrongAnswerIds)
    {
        if (Status != QuizSessionStatus.InProgress)
            throw new InvalidQuizSessionStatusException("Can only complete in-progress quiz sessions");

        Status = QuizSessionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        Score = score;
        TotalQuestions = totalQuestions;
        Percentage = percentage;
        WrongQuestionIds = wrongQuestionIds;
        WrongAnswerIds = wrongAnswerIds;
    }

    public void ExpireSession()
    {
        if (Status == QuizSessionStatus.Completed)
            return;

        Status = QuizSessionStatus.Expired;
    }

    public bool IsExpired()
    {
        if (Status == QuizSessionStatus.Completed)
            return false;

        if (StartedAt.HasValue)
        {
            var elapsedTime = DateTime.UtcNow - StartedAt.Value;
            return elapsedTime.TotalMinutes > MaxTimeInMinutes;
        }

        // Session expires 7 days after creation if not started
        var timeSinceCreation = DateTime.UtcNow - CreatedAt;
        return timeSinceCreation.TotalDays > 7;
    }
}