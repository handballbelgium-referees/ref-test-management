using Handball.Belgium.RefTestManagement.Domain;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Models;

public record SendInvitationsInput([ID<RefTest>] List<Guid> Ids);