using Handball.Belgium.RefTestManagement.AuditLog;

namespace Handball.Belgium.RefTestManagement.Domain.RefTestTitles.Events;

public record RefTestTitleCreatedEvent(string Value) : DomainEventBase
{
    public override string ActionName => "RefTestTitleCreated";

    public override object? GetChanges() => new { value = Value };
}
