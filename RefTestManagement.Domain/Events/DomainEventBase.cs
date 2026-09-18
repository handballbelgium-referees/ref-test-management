namespace Handball.Belgium.RefTestManagement.Domain.Events;

public abstract record DomainEventBase : IDomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public abstract string ActionName { get; }
    public abstract object? GetChanges();
}
