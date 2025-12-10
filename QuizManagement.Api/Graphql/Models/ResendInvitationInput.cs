using Handball.Belgium.Rules.Quiz.Domain;

namespace QuizManagement.Api.Graphql.Models;

public record ResendInvitationInput([property: ID<QuizSession>]Guid Id);