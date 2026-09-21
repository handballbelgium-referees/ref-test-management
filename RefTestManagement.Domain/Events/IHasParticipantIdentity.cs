namespace Handball.Belgium.RefTestManagement.Domain.Events;

/// <summary>
/// Implemented by entities that can be acted upon anonymously (e.g. a RefTest holder using
/// their invitation token, without being an authenticated staff user). When the audit
/// interceptor detects an anonymous, non-system request against such an entity, it attributes
/// the resulting audit event's ActorName/ActorEmail to this identity instead of "System".
/// </summary>
public interface IHasParticipantIdentity
{
    string? ParticipantName { get; }
    string? ParticipantEmail { get; }
}

