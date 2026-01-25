using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Update;

public record ExtendRefTestTimeInput(
    [property: ID<RefTest>] Guid Id,
    int AdditionalMinutes
);


