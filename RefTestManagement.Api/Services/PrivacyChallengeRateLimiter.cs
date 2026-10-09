using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.Threading.RateLimiting;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Handball.Belgium.RefTestManagement.Api.Services;

/// <summary>Applies independent fixed-window limits to public privacy challenge operations.</summary>
public interface IPrivacyChallengeRateLimiter
{
    Task<bool> TryAcquireRequestAsync(string clientAddress, CancellationToken cancellationToken);
    Task<bool> TryAcquireConfirmationAsync(string clientAddress, CancellationToken cancellationToken);
}

public sealed class PrivacyChallengeRateLimiter : IPrivacyChallengeRateLimiter, IDisposable
{
    private readonly PrivacyChallengeConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly IDatabase? _database;
    private readonly byte[]? _hmacKey;
    private readonly ILogger<PrivacyChallengeRateLimiter> _logger;
    private readonly PartitionedRateLimiter<string>? _requestLocal;
    private readonly PartitionedRateLimiter<string>? _confirmationLocal;
    private int _outageLogged;

    internal PrivacyChallengeRateLimiter(PrivacyChallengeConfiguration configuration)
        : this(configuration, new DevelopmentHostEnvironment(), NullLogger<PrivacyChallengeRateLimiter>.Instance)
    {
    }

    public PrivacyChallengeRateLimiter(
        PrivacyChallengeConfiguration configuration,
        IHostEnvironment environment,
        ILogger<PrivacyChallengeRateLimiter> logger,
        IConnectionMultiplexer? redis = null)
        : this(configuration, environment, logger, redis, false)
    {
    }

    internal PrivacyChallengeRateLimiter(
        PrivacyChallengeConfiguration configuration,
        IHostEnvironment environment,
        ILogger<PrivacyChallengeRateLimiter> logger,
        IConnectionMultiplexer? redis,
        bool skipProductionValidationForTests)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
        if (configuration.RateLimitBackend == PrivacyChallengeRateLimitBackend.Local)
        {
            var window = TimeSpan.FromSeconds(configuration.RateLimitWindowSeconds);
            _requestLocal = CreateLocal(configuration.RequestRateLimitPermitLimit, window);
            _confirmationLocal = CreateLocal(configuration.ConfirmationRateLimitPermitLimit, window);
            return;
        }

        if (!skipProductionValidationForTests)
            ValidateRateLimitConfiguration(configuration, redis is not null, null);
        _hmacKey = Encoding.UTF8.GetBytes(configuration.HmacSecret!);
        _database = (redis ?? throw new InvalidOperationException("Redis limiter dependency is missing."))
            .GetDatabase();
    }

    public Task<bool> TryAcquireRequestAsync(string clientAddress, CancellationToken cancellationToken) =>
        TryAcquireAsync(clientAddress, "request", _configuration.RequestRateLimitPermitLimit, _requestLocal, cancellationToken);

    public Task<bool> TryAcquireConfirmationAsync(string clientAddress, CancellationToken cancellationToken) =>
        TryAcquireAsync(clientAddress, "confirmation", _configuration.ConfirmationRateLimitPermitLimit, _confirmationLocal, cancellationToken);

    public void Dispose()
    {
        _requestLocal?.Dispose();
        _confirmationLocal?.Dispose();
    }

    public static void ValidateRateLimitConfiguration(
        PrivacyChallengeConfiguration configuration,
        bool redisConfigured,
        string? containerAppName)
    {
        if (!string.IsNullOrWhiteSpace(containerAppName)
            && configuration.RateLimitBackend != PrivacyChallengeRateLimitBackend.Redis)
            throw new InvalidOperationException(
                "Azure Container Apps requires the shared Redis privacy challenge rate-limit backend.");

        if (configuration.RateLimitBackend == PrivacyChallengeRateLimitBackend.Redis
            && (!redisConfigured
                || string.IsNullOrWhiteSpace(configuration.HmacSecret)
                || Encoding.UTF8.GetByteCount(configuration.HmacSecret) < 32))
            throw new InvalidOperationException(
                "The Redis privacy challenge rate-limit backend requires RedisConfiguration:Endpoint and an HMAC secret of at least 32 UTF-8 bytes.");
    }

    private async Task<bool> TryAcquireAsync(
        string address,
        string operation,
        int limit,
        PartitionedRateLimiter<string>? local,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(address) || address.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            return false;
        if (_configuration.RateLimitBackend == PrivacyChallengeRateLimitBackend.Local)
        {
            using var lease = local!.AttemptAcquire(address);
            return lease.IsAcquired;
        }

        var normalizedAddress = address.Trim().ToLowerInvariant();
        var digest = Convert.ToHexString(HMACSHA256.HashData(_hmacKey!, Encoding.UTF8.GetBytes(normalizedAddress)));
        var key = $"ref-test-management:{_environment.EnvironmentName}:{operation}:{digest}";
        var started = Stopwatch.GetTimestamp();
        try
        {
            var count = await IncrementWithFirstAttemptExpiryAsync(
                    key,
                    TimeSpan.FromSeconds(_configuration.RateLimitWindowSeconds))
                .WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
            if (Interlocked.Exchange(ref _outageLogged, 0) == 1)
                _logger.LogInformation("Privacy challenge limiter dependency recovered for {Dependency} during {Operation}; duration {DurationMs} ms",
                    "Redis", operation, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return count <= limit;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException or OperationCanceledException or InvalidOperationException)
        {
            if (Interlocked.Exchange(ref _outageLogged, 1) == 0)
                _logger.LogWarning("Privacy challenge limiter dependency failure for {Dependency} during {Operation}; category {ErrorCategory}; duration {DurationMs} ms",
                    "Redis", operation, exception is TimeoutException or OperationCanceledException ? "timeout" : "redis",
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return false;
        }
    }

    private async Task<long> IncrementWithFirstAttemptExpiryAsync(string key, TimeSpan window)
    {
        var transaction = _database!.CreateTransaction();
        var countTask = transaction.StringIncrementAsync(key);
        var expiryTask = transaction.KeyExpireAsync(key, window, ExpireWhen.HasNoExpiry);
        var ttlTask = transaction.KeyTimeToLiveAsync(key);
        if (!await transaction.ExecuteAsync())
            throw new InvalidOperationException("Redis rate limit transaction was not executed.");

        var count = await countTask;
        var expiryApplied = await expiryTask;
        var ttl = await ttlTask;
        if (!expiryApplied && ttl is null)
            throw new InvalidOperationException("Redis rate limit bucket has no expiry.");
        if (ttl is null || ttl <= TimeSpan.Zero)
            throw new InvalidOperationException("Redis rate limit bucket has no remaining lifetime.");
        return count;
    }

    private static PartitionedRateLimiter<string> CreateLocal(int limit, TimeSpan window) =>
        PartitionedRateLimiter.Create<string, string>(address =>
            RateLimitPartition.GetFixedWindowLimiter(address, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    private sealed class DevelopmentHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "RefTestManagement";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
