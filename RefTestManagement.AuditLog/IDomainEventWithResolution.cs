namespace Handball.Belgium.RefTestManagement.AuditLog;

/// <summary>
/// Implemented by domain events that need to resolve entity IDs to human-readable values
/// at the infrastructure layer (e.g. a TitleId → title name lookup).
/// The interceptor supplies the resolver; the event uses it to build its changes payload.
/// </summary>
public interface IDomainEventWithResolution : IDomainEvent
{
    /// <summary>Returns the changes payload, resolving entity IDs via the provided resolver.</summary>
    /// <param name="entityResolver">Resolves (entityTypeName, id) → display string, or null if not found.</param>
    object? GetChanges(Func<string, Guid, string?> entityResolver);
}
