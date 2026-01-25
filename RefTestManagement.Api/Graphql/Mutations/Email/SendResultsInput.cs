using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Email;

public record SendResultsInput([property: ID<RefTest>] List<Guid> Ids);

