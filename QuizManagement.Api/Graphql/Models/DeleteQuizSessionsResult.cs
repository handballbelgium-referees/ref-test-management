using Handball.Belgium.Rules.Quiz.Domain;

namespace QuizManagement.Api.Graphql.Models;

public class DeleteQuizSessionsResult
{
    public int TotalRequested { get; set; }
    public int SuccessfullySent { get; set; }
    public int Failed { get; set; }
    public List<QuizSession> DeletedSessions { get; set; } = [];
    public List<DeleteQuizSessionError> Errors { get; set; } = [];
}

public class DeleteQuizSessionError
{
    public Guid QuizSessionId { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}