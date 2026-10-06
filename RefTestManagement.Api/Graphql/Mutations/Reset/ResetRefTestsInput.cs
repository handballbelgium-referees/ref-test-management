using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Reset;

public record ResetRefTestsInput(
    [property: ID<RefTestDto>] List<Guid> Ids,
    RefTestResetType ResetType,
    bool RegenerateToken
);

