namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Reset;

public enum RefTestResetType
{
    Soft,
    Hard
}

public record ResetRefTestsInput(
    List<Guid> RefTestIds,
    RefTestResetType ResetType,
    bool RegenerateToken = true
);


