using Handball.Belgium.RefTestManagement.Domain;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Models;

public record SendResultsInput([ID<RefTest>] List<Guid> Ids);