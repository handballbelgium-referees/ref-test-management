using Handball.Belgium.RefTestManagement.Domain;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Models;

public record DeleteRefTestsInput([ID<RefTest>] List<Guid> Ids);