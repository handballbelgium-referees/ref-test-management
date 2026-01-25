using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Subscriptions;

public record RefTestStarted([property: ID<RefTest>] Guid Id, RefTestStatus Status, DateTime StartedAt);
public record RefTestCompleted([property: ID<RefTest>] Guid Id, RefTestStatus Status, DateTime CompletedAt);
public record RefTestExpired([property: ID<RefTest>] Guid Id, RefTestStatus Status, DateTime ExpiredAt);
