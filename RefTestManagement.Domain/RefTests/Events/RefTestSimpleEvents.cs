using Handball.Belgium.RefTestManagement.AuditLog;

namespace Handball.Belgium.RefTestManagement.Domain.RefTests.Events;

public sealed record RefTestApprovedEvent : DomainEventBase
{
    public override string ActionName => "RefTestApproved";
    public override object? GetChanges() => null;
}

public sealed record RefTestRejectedEvent(string Reason) : DomainEventBase
{
    public override string ActionName => "RefTestRejected";
    public override object? GetChanges() => new { reason = Reason };
}

public sealed record RefTestExpiredEvent : DomainEventBase
{
    public override string ActionName => "RefTestExpired";
    public override object? GetChanges() => null;
}

public sealed record RefTestDeletedEvent : DomainEventBase
{
    public override string ActionName => "RefTestDeleted";
    public override object? GetChanges() => null;
}

public sealed record RefTestAnonymizedEvent : DomainEventBase
{
    public const string EventType = "RefTestAnonymized";
    public override string ActionName => EventType;
    public override object? GetChanges() => null;
}

public sealed record RefTestRevivedEvent : DomainEventBase
{
    public override string ActionName => "RefTestRevived";
    public override object? GetChanges() => null;
}

public sealed record RefTestInvitationSentEvent : DomainEventBase
{
    public override string ActionName => "RefTestInvitationSent";
    public override object? GetChanges() => null;
}

public sealed record RefTestResultsSentEvent : DomainEventBase
{
    public override string ActionName => "RefTestResultsSent";
    public override object? GetChanges() => null;
}

public sealed record RefTestTokenRegeneratedEvent : DomainEventBase
{
    public override string ActionName => "RefTestTokenRegenerated";
    public override object? GetChanges() => null;
}

public sealed record RefTestHardResetEvent : DomainEventBase
{
    public override string ActionName => "RefTestHardReset";
    public override object? GetChanges() => null;
}
