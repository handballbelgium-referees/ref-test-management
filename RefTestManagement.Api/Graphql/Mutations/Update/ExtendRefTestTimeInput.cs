namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Update;

public record ExtendRefTestTimeInput(
    Guid RefTestId,
    int AdditionalMinutes
);


