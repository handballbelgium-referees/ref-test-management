using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Reset;

public enum RefTestResetType
{
    Soft,
    Hard
}

public record ResetRefTestsInput(
    [property: ID<RefTest>] List<Guid> Ids,
    RefTestResetType ResetType,
    bool RegenerateToken = true
);


