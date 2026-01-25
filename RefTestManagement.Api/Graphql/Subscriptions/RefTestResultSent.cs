using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Subscriptions;

public record RefTestResultSent([property: ID<RefTest>] Guid Id, DateTime SentAt);