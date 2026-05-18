namespace Handball.Belgium.RefTestManagement.AuditLog;

public interface IDomainEvent
{
    string ActionName { get; }
    DateTime OccurredAt { get; }
    object? GetChanges();
}
