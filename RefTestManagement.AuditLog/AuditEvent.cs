namespace Handball.Belgium.RefTestManagement.AuditLog;

public class AuditEvent
{
    public long SeqId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public string StreamId { get; init; } = string.Empty;
    public long Version { get; init; }
    public string? Data { get; init; }
    public string Type { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string ActorName { get; init; } = string.Empty;
    public string ActorEmail { get; init; } = string.Empty;
    public string? Headers { get; init; }
    public bool IsArchived { get; init; }
}
