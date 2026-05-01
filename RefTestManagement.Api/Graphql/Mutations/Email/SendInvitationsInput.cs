using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Email;

public record SendInvitationsInput([property: ID<RefTestDto>] List<Guid> Ids);

