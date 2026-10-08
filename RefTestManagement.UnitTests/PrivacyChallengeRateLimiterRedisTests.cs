using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using DotNet.Testcontainers.Builders;
using Testcontainers.Redis;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class PrivacyChallengeRateLimiterRedisTests
{
    [Fact]
    public async Task IndependentInstancesShareAtomicQuotaAndKeepBucketsIsolated()
    {
        await using var redis = new RedisBuilder()
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
            .Build();
        await redis.StartAsync(TestContext.Current.CancellationToken);
        var options = ConfigurationOptions.Parse(redis.GetConnectionString());
        options.AbortOnConnectFail = false;
        using var connectionA = await ConnectionMultiplexer.ConnectAsync(options);
        using var connectionB = await ConnectionMultiplexer.ConnectAsync(options);
        var configuration = new PrivacyChallengeConfiguration
        {
            RateLimitWindowSeconds = 30,
            RequestRateLimitPermitLimit = 5,
            ConfirmationRateLimitPermitLimit = 2,
            RateLimitBackend = PrivacyChallengeRateLimitBackend.Redis,
            HmacSecret = new string('a', 32)
        };
        using var first = NewLimiter(configuration, "Production", connectionA);
        using var second = NewLimiter(configuration, "Production", connectionB);
        var cancellationToken = TestContext.Current.CancellationToken;

        var concurrent = Enumerable.Range(0, 20)
            .Select(index => (index % 2 == 0 ? first : second)
                .TryAcquireRequestAsync("192.0.2.1", cancellationToken));
        Assert.Equal(5, (await Task.WhenAll(concurrent)).Count(allowed => allowed));
        Assert.False(await first.TryAcquireRequestAsync("unknown", cancellationToken));
        Assert.False(await first.TryAcquireRequestAsync("", cancellationToken));
        Assert.True(await first.TryAcquireRequestAsync("192.0.2.2", cancellationToken));
        Assert.True(await first.TryAcquireConfirmationAsync("192.0.2.1", cancellationToken));
        Assert.True(await first.TryAcquireConfirmationAsync("192.0.2.1", cancellationToken));
        Assert.False(await second.TryAcquireConfirmationAsync("192.0.2.1", cancellationToken));

        using var staging = NewLimiter(configuration, "Staging", connectionA);
        Assert.True(await staging.TryAcquireRequestAsync("192.0.2.1", cancellationToken));
        using var rotated = NewLimiter(new PrivacyChallengeConfiguration
            {
                RateLimitWindowSeconds = 30,
                RequestRateLimitPermitLimit = 5,
                ConfirmationRateLimitPermitLimit = 2,
                RateLimitBackend = PrivacyChallengeRateLimitBackend.Redis,
                HmacSecret = new string('b', 32)
            },
            "Production", connectionA);
        Assert.True(await rotated.TryAcquireRequestAsync("192.0.2.1", cancellationToken));

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => first.TryAcquireRequestAsync("192.0.2.3", canceled.Token));

        using var boundary = NewLimiter(new PrivacyChallengeConfiguration
        {
            RateLimitWindowSeconds = 1,
            RequestRateLimitPermitLimit = 1,
            RateLimitBackend = PrivacyChallengeRateLimitBackend.Redis,
            HmacSecret = new string('c', 32)
        }, "Boundary", connectionA);
        Assert.True(await boundary.TryAcquireRequestAsync("192.0.2.4", cancellationToken));
        await Task.Delay(TimeSpan.FromMilliseconds(700), cancellationToken);
        Assert.False(await boundary.TryAcquireRequestAsync("192.0.2.4", cancellationToken));
        await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken);
        Assert.True(await boundary.TryAcquireRequestAsync("192.0.2.4", cancellationToken));

        var server = connectionA.GetServer(connectionA.GetEndPoints().Single());
        await server.ExecuteAsync("ACL", "SETUSER", "default", "-EXPIRE", "-PEXPIRE");
        Assert.False(await first.TryAcquireRequestAsync("192.0.2.6", cancellationToken));
        Assert.False(await first.TryAcquireRequestAsync("192.0.2.6", cancellationToken));
        await server.ExecuteAsync("ACL", "SETUSER", "default", "+EXPIRE", "+PEXPIRE");
        Assert.True(await first.TryAcquireRequestAsync("192.0.2.6", cancellationToken));
    }

    [Fact]
    public void RedisConfigurationRequiresAuthenticatedTlsAndStrongHmacSecret()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PrivacyChallengeRateLimiter.ValidateRateLimitConfiguration(new PrivacyChallengeConfiguration
            {
                RateLimitBackend = PrivacyChallengeRateLimitBackend.Redis
            }, null));
        Assert.Throws<InvalidOperationException>(() =>
            PrivacyChallengeRateLimiter.ValidateRateLimitConfiguration(new PrivacyChallengeConfiguration
            {
                RateLimitBackend = PrivacyChallengeRateLimitBackend.Redis,
                RedisEndpoint = "redis://:password@localhost:6379",
                HmacSecret = new string('x', 32)
            }, null));
        Assert.Throws<InvalidOperationException>(() =>
            PrivacyChallengeRateLimiter.ValidateRateLimitConfiguration(new PrivacyChallengeConfiguration
            {
                RateLimitBackend = PrivacyChallengeRateLimitBackend.Redis,
                RedisEndpoint = "rediss://:password@localhost:6379",
                HmacSecret = "short"
            }, null));
        PrivacyChallengeRateLimiter.ValidateRateLimitConfiguration(new PrivacyChallengeConfiguration
        {
            RateLimitBackend = PrivacyChallengeRateLimitBackend.Redis,
            RedisEndpoint = "rediss://:password@localhost:6379",
            HmacSecret = new string('x', 32)
        }, null);
    }

    [Fact]
    public async Task ProductionSingleInstanceCanUseLocalLimiterWithoutRedis()
    {
        var configuration = new PrivacyChallengeConfiguration { RequestRateLimitPermitLimit = 1 };
        using var limiter = new PrivacyChallengeRateLimiter(configuration,
            new TestHostEnvironment(Environments.Production),
            NullLogger<PrivacyChallengeRateLimiter>.Instance);

        Assert.True(await limiter.TryAcquireRequestAsync("192.0.2.5", TestContext.Current.CancellationToken));
        Assert.False(await limiter.TryAcquireRequestAsync("192.0.2.5", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ContainerAppsRequiresSharedRedisBackend()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PrivacyChallengeRateLimiter.ValidateRateLimitConfiguration(
                new PrivacyChallengeConfiguration(),
                "ref-test-api"));
        PrivacyChallengeRateLimiter.ValidateRateLimitConfiguration(
            new PrivacyChallengeConfiguration
            {
                RateLimitBackend = PrivacyChallengeRateLimitBackend.Redis,
                RedisEndpoint = "rediss://:password@localhost:6379",
                HmacSecret = new string('x', 32)
            },
            "ref-test-api");
    }

    [Fact]
    public async Task DevelopmentRetainsLocalLimiterWithoutRedis()
    {
        var configuration = new PrivacyChallengeConfiguration { RequestRateLimitPermitLimit = 1 };
        using var limiter = new PrivacyChallengeRateLimiter(configuration,
            new TestHostEnvironment(Environments.Development),
            NullLogger<PrivacyChallengeRateLimiter>.Instance);

        Assert.True(await limiter.TryAcquireRequestAsync("192.0.2.5", TestContext.Current.CancellationToken));
        Assert.False(await limiter.TryAcquireRequestAsync("192.0.2.5", TestContext.Current.CancellationToken));
    }

    private static PrivacyChallengeRateLimiter NewLimiter(
        PrivacyChallengeConfiguration configuration,
        string environmentName,
        IConnectionMultiplexer redis) =>
        new(configuration, new TestHostEnvironment(environmentName),
            NullLogger<PrivacyChallengeRateLimiter>.Instance, redis, skipProductionValidationForTests: true);

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "RefTestManagement";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
