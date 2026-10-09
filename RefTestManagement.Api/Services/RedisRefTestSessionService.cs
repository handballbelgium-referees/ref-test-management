using StackExchange.Redis;

namespace Handball.Belgium.RefTestManagement.Api.Services;

/// <summary>
/// Single-tab session lock shared by every replica. The lock is a Redis key holding the session id
/// with a lease; the subscription renews it on each revalidation tick, so a replica that dies stops
/// renewing and the RefTest unlocks once the lease runs out.
/// </summary>
public sealed class RedisRefTestSessionService : IRefTestSessionService
{
    /// <summary>Three revalidation ticks, so one slow or missed renewal does not drop the lock.</summary>
    internal static readonly TimeSpan DefaultLease = TimeSpan.FromSeconds(90);

    // Renew and release only touch the key while it still holds this session's id, so a session
    // whose lease expired can never extend or delete the lock a newer session took over.
    private const string RenewScript =
        "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('PEXPIRE', KEYS[1], ARGV[2]) else return 0 end";
    private const string ReleaseScript =
        "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end";

    private readonly IDatabase _database;
    private readonly string _keyPrefix;
    private readonly TimeSpan _lease;
    private readonly ILogger<RedisRefTestSessionService> _logger;

    public RedisRefTestSessionService(
        IConnectionMultiplexer redis,
        IHostEnvironment environment,
        ILogger<RedisRefTestSessionService> logger)
        : this(redis, environment, logger, DefaultLease)
    {
    }

    internal RedisRefTestSessionService(
        IConnectionMultiplexer redis,
        IHostEnvironment environment,
        ILogger<RedisRefTestSessionService> logger,
        TimeSpan lease)
    {
        _database = redis.GetDatabase();
        _keyPrefix = $"ref-test-management:{environment.EnvironmentName}:session-lock:";
        _lease = lease;
        _logger = logger;
    }

    public Task<bool> TryAcquireSessionAsync(string refTestKey, string sessionId, CancellationToken cancellationToken) =>
        _database.StringSetAsync(Key(refTestKey), sessionId, _lease, When.NotExists);

    public async Task<bool> RenewSessionAsync(string refTestKey, string sessionId, CancellationToken cancellationToken)
    {
        var renewed = await _database.ScriptEvaluateAsync(
            RenewScript, [Key(refTestKey)], [sessionId, (long)_lease.TotalMilliseconds]);
        return (long)renewed == 1;
    }

    public async Task ReleaseSessionAsync(string refTestKey, string sessionId)
    {
        try
        {
            await _database.ScriptEvaluateAsync(ReleaseScript, [Key(refTestKey)], [sessionId]);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            // The lease still expires on its own; only the next tab's wait gets longer.
            _logger.LogWarning("Releasing a RefTest session lock failed; it unlocks when its lease expires ({Error})",
                exception.GetType().Name);
        }
    }

    private RedisKey Key(string refTestKey) => _keyPrefix + refTestKey;
}
