namespace Handball.Belgium.RefTestManagement.Api.Graphql.Models;

public record SendResultsInput([property: ID<Domain.RefTest>] List<Guid> Ids);