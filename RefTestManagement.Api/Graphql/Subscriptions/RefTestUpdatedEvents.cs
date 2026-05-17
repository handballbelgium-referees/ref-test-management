using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
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
public record RefTestInvitationSent([property: ID<RefTestDto>] Guid Id, DateTime SentAt) : IRefTestEvent;

[ObjectType]
public record RefTestResultSent([property: ID<RefTestDto>] Guid Id, DateTime SentAt) : IRefTestEvent;

[ObjectType]
public record RefTestStarted([property: ID<RefTestDto>] Guid Id, RefTestStatus Status, DateTime StartedAt) : IRefTestEvent;

[ObjectType]
public record RefTestCompleted(
    [property: ID<RefTestDto>] Guid Id,
    RefTestStatus Status,
    DateTime CompletedAt,
    int QuestionScore,
    int QuestionTotal,
    int AnswerScore,
    int AnswerTotal,
    double Percentage,
    string Language) : IRefTestEvent;

[ObjectType]
public record RefTestExpired([property: ID<RefTestDto>] Guid Id, RefTestStatus Status, DateTime ExpiredAt) : IRefTestEvent;

[ObjectType]
public record RefTestDeleted([property: ID<RefTestDto>] Guid Id, RefTestStatus Status) : IRefTestEvent;

[ObjectType]
public record RefTestReset([property: ID<RefTestDto>] Guid Id, RefTestStatus OldStatus) : IRefTestEvent;

[ObjectType]
public record RefTestRevived([property: ID<RefTestDto>] Guid Id) : IRefTestEvent;

[ObjectType]
public record RefTestCreated(
    [property: ID<RefTestDto>] Guid Id,
    string Name,
    string Email,
    [property: ID<RefTestTitleDto>] Guid? TitleId,
    string? TitleValue,
    bool InvitationSent,
    bool ResultsSent,
    bool SendInvitationsAutomatically,
    bool SendResultsAutomatically,
    RefTestStatus Status,
    int NumberOfQuestions,
    int MaxTimeInMinutes) : IRefTestEvent;

[ObjectType]
public record RefTestApproved([property: ID<RefTestDto>] Guid Id, RefTestStatus Status, DateTime ApprovedAt) : IRefTestEvent;

[ObjectType]
public record RefTestRejected([property: ID<RefTestDto>] Guid Id, RefTestStatus Status, string Reason, DateTime RejectedAt) : IRefTestEvent;
