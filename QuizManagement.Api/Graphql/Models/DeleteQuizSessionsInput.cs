using Handball.Belgium.Rules.Quiz.Domain;

namespace QuizManagement.Api.Graphql.Models;

public record DeleteQuizSessionsInput([property: ID<QuizSession>] List<Guid> Ids);