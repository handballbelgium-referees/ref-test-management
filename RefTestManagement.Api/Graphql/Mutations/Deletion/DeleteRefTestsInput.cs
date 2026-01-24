using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Deletion;

public record DeleteRefTestsInput([property: ID<RefTest>] List<Guid> Ids);

