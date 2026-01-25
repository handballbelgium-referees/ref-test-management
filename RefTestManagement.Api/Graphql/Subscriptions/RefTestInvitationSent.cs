using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Subscriptions;

public record RefTestInvitationSent([property: ID<RefTest>] Guid Id, DateTime SentAt);