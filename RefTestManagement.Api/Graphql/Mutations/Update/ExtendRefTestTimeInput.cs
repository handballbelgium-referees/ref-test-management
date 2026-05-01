using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Update;

public record ExtendRefTestTimeInput(
    [property: ID<RefTestDto>] Guid Id,
    int AdditionalMinutes
);


