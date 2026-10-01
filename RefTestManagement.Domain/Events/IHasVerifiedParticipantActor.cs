namespace Handball.Belgium.RefTestManagement.Domain.Events;

/// <summary>
/// Marks domain events raised after a participant has verified control of their mailbox.
/// Audit attribution must use the verified participant context even if the confirmation POST
/// happens to carry an authenticated staff session.
/// </summary>
public interface IHasVerifiedParticipantActor
{
    /// <summary>Whether the entity has completed its one-time participant verification.</summary>
    bool IsVerifiedParticipantActor { get; }
}
