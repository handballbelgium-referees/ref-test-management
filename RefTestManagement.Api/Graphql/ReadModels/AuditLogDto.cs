namespace Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

public class AuditLogDto
{
    public Guid Id { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Changes { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string ActorEmail { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
