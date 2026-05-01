using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;

[OneOf]
public record Title([property: ID<RefTestTitleDto>]Guid? Id, string? Name);
