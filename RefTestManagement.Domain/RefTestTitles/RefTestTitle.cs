using Handball.Belgium.RefTestManagement.Domain.Events;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles.Events;

namespace Handball.Belgium.RefTestManagement.Domain.RefTestTitles;

public class RefTestTitle : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];
    private void RaiseDomainEvent(IDomainEvent e) => _domainEvents.Add(e);

    private RefTestTitle(string value)
    {
        Value = value;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Value { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    public static RefTestTitle Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Title is required", nameof(value));

        var title = new RefTestTitle(value);
        title.RaiseDomainEvent(new RefTestTitleCreatedEvent(value));
        return title;
    }
}