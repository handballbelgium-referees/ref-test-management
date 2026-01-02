using Handball.Belgium.Rules.Quiz.Domain;

namespace QuizManagement.Api.Graphql.Models;

public record GenerateQuizSessionsReportInput(
    [property: ID<QuizSession>] List<Guid> SessionIds);
