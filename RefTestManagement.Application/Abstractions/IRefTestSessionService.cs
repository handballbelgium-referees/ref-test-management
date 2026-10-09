namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

public interface IRefTestSessionService
{
    /// <summary>
    /// Tries to acquire a session for the given RefTest key.
    /// Returns true if no session currently holds the key, or false if one does.
    /// </summary>
    bool TryAcquireSession(string refTestKey, string sessionId);

    /// <summary>
    /// Releases the session for the given RefTest key if it is held by the given sessionId.
    /// </summary>
    void ReleaseSession(string refTestKey, string sessionId);
}
