using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Update;

public record UpdateRefTestDetailsInput(
    [property: ID<RefTest>] Guid Id,
    string FirstName,
    string LastName,
    string Email,
    bool ResendInvitation = false
);


