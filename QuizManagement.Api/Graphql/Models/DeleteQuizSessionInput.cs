using Handball.Belgium.Rules.Quiz.Domain;

namespace QuizManagement.Api.Graphql.Models;

public record DeleteQuizSessionInput([property: ID<QuizSession>] Guid Id);