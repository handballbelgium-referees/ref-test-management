using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Models;

public record SendResultsInput([property: ID<RefTest>] List<Guid> Ids);