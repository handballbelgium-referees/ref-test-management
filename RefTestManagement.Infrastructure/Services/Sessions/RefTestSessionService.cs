namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

/// <summary>
/// Process-local session lock for development and single-instance hosting. The host switches to the
/// Redis lease when Redis is configured, so the lock spans replicas.
/// </summary>
public class RefTestSessionService : IRefTestSessionService
{
    private readonly Dictionary<string, string> _sessions = new();
    private readonly Lock _lock = new();

    public Task<bool> TryAcquireSessionAsync(string refTestKey, string sessionId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult(_sessions.TryAdd(refTestKey, sessionId));
        }
    }

    public Task<bool> RenewSessionAsync(string refTestKey, string sessionId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult(_sessions.TryGetValue(refTestKey, out var existing) && existing == sessionId);
        }
    }

    public Task ReleaseSessionAsync(string refTestKey, string sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(refTestKey, out var existing) && existing == sessionId)
                _sessions.Remove(refTestKey);
        }

        return Task.CompletedTask;
    }
}