using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Participants;

public record UpdateParticipantInput([property: ID<ParticipantDto>] Guid Id, ParticipantDraft Participant);
