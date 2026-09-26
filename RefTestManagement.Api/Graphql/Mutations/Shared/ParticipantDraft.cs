using Handball.Belgium.RefTestManagement.Domain.Participants;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;

public record ParticipantDraft(
    string FirstName,
    string LastName,
    string Email,
    ParticipantType Type,
    ParticipantLevel? Level);
