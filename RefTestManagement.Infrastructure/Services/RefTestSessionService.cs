namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

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

public class RefTestSessionService : IRefTestSessionService
{
    private readonly Dictionary<string, string> _sessions = new();
    private readonly Lock _lock = new();

    public bool TryAcquireSession(string refTestKey, string sessionId)
    {
        lock (_lock)
        {
            return _sessions.TryAdd(refTestKey, sessionId);
        }
    }

    public void ReleaseSession(string refTestKey, string sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(refTestKey, out var existing) && existing == sessionId)
                _sessions.Remove(refTestKey);
        }
    }
}
