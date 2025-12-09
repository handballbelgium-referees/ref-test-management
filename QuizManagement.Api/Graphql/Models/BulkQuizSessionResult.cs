using Handball.Belgium.Rules.Quiz.Domain;

namespace QuizManagement.Api.Graphql.Models;

public class BulkQuizSessionResult
{
    public int TotalRequested { get; set; }
    public int SuccessfullyCreated { get; set; }
    public int Failed { get; set; }
    public List<QuizSession> CreatedSessions { get; set; } = [];
    public List<BulkCreationError> Errors { get; set; } = [];
}

public class BulkCreationError
{
    public User User { get; set; } = null!;
    public string ErrorMessage { get; set; } = string.Empty;
}

