namespace Handball.Belgium.RefTestManagement.AuditLog;

public class AuditLogEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Changes { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string ActorEmail { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
