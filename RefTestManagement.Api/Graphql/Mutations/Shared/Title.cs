using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;

[OneOf]
public record Title([property: ID<RefTestTitle>]Guid? Id, string? Name);