using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Update;

public record UpdateRefTestDetailsInput(
    [property: ID<RefTestDto>] Guid Id,
    string FirstName,
    string LastName,
    string Email,
    bool ResendInvitation = false
);


