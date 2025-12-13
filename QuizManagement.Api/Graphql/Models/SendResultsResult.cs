using Handball.Belgium.Rules.Quiz.Domain;

namespace QuizManagement.Api.Graphql.Models;

public class SendResultsResult
{
    public int TotalRequested { get; set; }
    public int SuccessfullySent { get; set; }
    public int Failed { get; set; }
    public List<QuizSession> SentSessions { get; set; } = [];
    public List<SendInvitationError> Errors { get; set; } = [];
}

public class SendResultError
{
    public Guid QuizSessionId { get; set; }
    public User? User { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}