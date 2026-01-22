using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Models;

public record SendInvitationsInput([property: ID<RefTest>] List<Guid> Ids);