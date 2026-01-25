using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Update;

public record UpdateRefTestNotificationSettingsInput(
    [property: ID<RefTest>] Guid Id,
    bool? SendInvitationsAutomatically = null,
    bool? SendResultsAutomatically = null
);


