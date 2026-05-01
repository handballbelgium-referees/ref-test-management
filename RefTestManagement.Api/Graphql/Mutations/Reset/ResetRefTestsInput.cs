using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Reset;

public enum RefTestResetType
{
    Soft,
    Hard
}

public record ResetRefTestsInput(
    [property: ID<RefTestDto>] List<Guid> Ids,
    RefTestResetType ResetType,
    bool RegenerateToken
);


