using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Subscriptions;

public record RefTestTimeExtended(
    [property: ID<RefTest>]Guid Id,
    int NewMaxTimeInMinutes,
    int AdditionalMinutes,
    DateTime ExtendedAt
);
