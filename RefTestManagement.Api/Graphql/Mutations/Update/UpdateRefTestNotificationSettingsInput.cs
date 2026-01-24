namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Update;

public record UpdateRefTestNotificationSettingsInput(
    Guid RefTestId,
    bool? SendInvitationsAutomatically = null,
    bool? SendResultsAutomatically = null
);


