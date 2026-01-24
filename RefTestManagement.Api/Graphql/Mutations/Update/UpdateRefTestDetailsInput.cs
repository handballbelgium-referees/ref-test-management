namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Update;

public record UpdateRefTestDetailsInput(
    Guid RefTestId,
    string FirstName,
    string LastName,
    string Email,
    bool ResendInvitation = false
);


