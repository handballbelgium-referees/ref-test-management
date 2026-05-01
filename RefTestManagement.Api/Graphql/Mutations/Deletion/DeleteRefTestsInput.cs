using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Deletion;

public record DeleteRefTestsInput([property: ID<RefTestDto>] List<Guid> Ids);

