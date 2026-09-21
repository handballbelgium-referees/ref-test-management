namespace Handball.Belgium.RefTestManagement.Domain.Events;

public interface IDomainEvent
{
    string ActionName { get; }
    DateTime OccurredAt { get; }
    object? GetChanges();
}
