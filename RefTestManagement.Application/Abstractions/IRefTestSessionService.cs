namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

/// <summary>
/// Single-tab lock for a participant's RefTest session. Implementations that span replicas hold the
/// lock as a lease, so the holder renews it while the session lives and a crashed replica cannot
/// keep a RefTest locked forever.
/// </summary>
public interface IRefTestSessionService
{
    /// <summary>
    /// Tries to acquire a session for the given RefTest key.
    /// Returns true if no session currently holds the key, or false if one does.
    /// </summary>
    Task<bool> TryAcquireSessionAsync(string refTestKey, string sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Extends the lock held by <paramref name="sessionId"/>. Returns false when that session no
    /// longer holds it, for example because its lease expired and another session took over.
    /// </summary>
    Task<bool> RenewSessionAsync(string refTestKey, string sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Releases the session for the given RefTest key if it is held by the given sessionId.
    /// </summary>
    Task ReleaseSessionAsync(string refTestKey, string sessionId);
}
