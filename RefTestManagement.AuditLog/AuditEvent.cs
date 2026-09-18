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

    /// <summary>
    /// When the personal data on this row was redacted. Tracked separately from
    /// <see cref="IsArchived"/> because that flag also hides the row from the admin UI, so it
    /// cannot be reused as the retention sweep's cursor: rows archived by a deployment that did
    /// not yet redact would be skipped forever.
    /// </summary>
    public DateTime? RedactedAt { get; init; }
}
