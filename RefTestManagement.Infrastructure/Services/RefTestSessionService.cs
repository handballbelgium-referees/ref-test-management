namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

public interface IRefTestSessionService
{
    /// <summary>
    /// Tries to acquire a session for the given token.
    /// Returns true if the session was acquired (new) or already held by the same sessionId (refresh).
    /// Returns false if another sessionId already holds the session.
    /// </summary>
    bool TryAcquireSession(string token, string sessionId);

    /// <summary>
    /// Releases the session for the given token if it is held by the given sessionId.
    /// Uses a connection count to safely handle refreshes where the new connection
    /// is established before the old one drops (count: 1 → 2 → 1 instead of 1 → 0 → 1).
    /// </summary>
    void ReleaseSession(string token, string sessionId);
}

public class RefTestSessionService : IRefTestSessionService
{
    private readonly Dictionary<string, string> _sessions = new();
    private readonly Lock _lock = new();

    public bool TryAcquireSession(string token, string sessionId)
    {
        lock (_lock)
        {
            return _sessions.TryAdd(token, sessionId);
        }
    }

    public void ReleaseSession(string token, string sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(token, out var existing) && existing == sessionId)
                _sessions.Remove(token);
        }
    }
}
