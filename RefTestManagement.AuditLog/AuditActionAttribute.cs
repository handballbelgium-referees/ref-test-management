namespace Handball.Belgium.RefTestManagement.AuditLog;

/// <summary>
/// Marks a GraphQL mutation method with the audit log action to record.
/// The <see cref="AuditLogTypeInterceptor"/> picks this up automatically —
/// mutations do NOT need to call <see cref="IAuditLogService"/> directly.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AuditActionAttribute : Attribute
{
    public AuditActionAttribute(string action) => Action = action;

    /// <summary>The action identifier, e.g. <see cref="AuditLogAction.RefTest.Create"/>.</summary>
    public string Action { get; }
}
