using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Subscriptions;

public record RefTestTimeExtended(
    [property: ID<RefTestDto>] Guid Id,
    int NewMaxTimeInMinutes,
    int AdditionalMinutes,
    DateTime ExtendedAt
);