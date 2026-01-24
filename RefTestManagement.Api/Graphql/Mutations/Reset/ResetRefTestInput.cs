namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Reset;

public enum RefTestResetType
{
    Soft,
    Hard
}

public record ResetRefTestInput(
    Guid RefTestId,
    RefTestResetType ResetType,
    bool RegenerateToken = true
);


