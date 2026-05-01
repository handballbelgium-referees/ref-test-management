using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Update;

public record UpdateRefTestNotificationSettingsInput(
    [property: ID<RefTestDto>] Guid Id,
    bool? SendInvitationsAutomatically = null,
    bool? SendResultsAutomatically = null
);


