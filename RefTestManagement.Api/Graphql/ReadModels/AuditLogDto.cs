namespace Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

public class AuditLogDto
{
    public long SeqId { get; init; }
    public Guid Id { get; init; }
    public string StreamId { get; init; } = string.Empty;
    public long Version { get; init; }
    public string? Data { get; init; }
    public string Type { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string ActorEmail { get; init; } = string.Empty;
    public string? Headers { get; init; }
    public bool IsArchived { get; init; }
    /// <summary>True when the referenced RefTest aggregate still exists in the database.</summary>
    public bool RefTestExists { get; init; }
}
