using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Subscriptions;

/// <summary>
/// Base interface for all RefTest events (enables union type in GraphQL)
/// </summary>
[UnionType("RefTestEvent")]
public interface IRefTestEvent
{
    Guid Id { get; }
}

[ObjectType]
public record RefTestInvitationSent([property: ID<RefTest>] Guid Id, DateTime SentAt) : IRefTestEvent;

[ObjectType]
public record RefTestResultSent([property: ID<RefTest>] Guid Id, DateTime SentAt) : IRefTestEvent;

[ObjectType]
public record RefTestStarted([property: ID<RefTest>] Guid Id, RefTestStatus Status, DateTime StartedAt) : IRefTestEvent;

[ObjectType]
public record RefTestCompleted(
    [property: ID<RefTest>] Guid Id,
    RefTestStatus Status,
    DateTime CompletedAt,
    int QuestionScore,
    int QuestionTotal,
    int AnswerScore,
    int AnswerTotal,
    double Percentage) : IRefTestEvent;

[ObjectType]
public record RefTestExpired([property: ID<RefTest>] Guid Id, RefTestStatus Status, DateTime ExpiredAt) : IRefTestEvent;