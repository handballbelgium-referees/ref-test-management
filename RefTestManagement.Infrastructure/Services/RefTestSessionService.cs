namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

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
