namespace Handball.Belgium.RefTestManagement.AuditLog;

/// <summary>Represents a single audited action performed by an authenticated user.</summary>
internal class AuditLog
{
    private AuditLog() { }

    private AuditLog(string action, string userEmail, string? userName, string? resourceId, string? details)
    {
        Action = action;
        UserEmail = userEmail;
        UserName = userName;
        ResourceId = resourceId;
        Details = details;
        PerformedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>The action that was performed, e.g. <see cref="AuditLogAction.RefTest.Create"/>.</summary>
    public string Action { get; private set; } = null!;

    /// <summary>Email address of the user who performed the action.</summary>
    public string UserEmail { get; private set; } = null!;

    /// <summary>Display name of the user who performed the action.</summary>
    public string? UserName { get; private set; }

    /// <summary>
    /// Comma-separated resource identifiers affected by the action, e.g. the ref test IDs.
    /// Null when the action is not resource-specific.
    /// </summary>
    public string? ResourceId { get; private set; }

    /// <summary>Optional JSON blob with extra contextual information.</summary>
    public string? Details { get; private set; }

    /// <summary>UTC timestamp of when the action was performed.</summary>
    public DateTime PerformedAt { get; private set; }

    public static AuditLog Create(
        string action,
        string userEmail,
        string? userName = null,
        string? resourceId = null,
        string? details = null)
        => new(action, userEmail, userName, resourceId, details);
}
