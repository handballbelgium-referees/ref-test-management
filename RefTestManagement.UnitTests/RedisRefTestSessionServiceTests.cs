using Handball.Belgium.RefTestManagement.Api.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using DotNet.Testcontainers.Builders;
using Testcontainers.Redis;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class RedisRefTestSessionServiceTests
{
    [Fact]
    public async Task TheLockIsSharedAcrossReplicasAndOnlyItsHolderCanRenewOrReleaseIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var redis = await StartRedisAsync(cancellationToken);
        using var connectionA = await ConnectAsync(redis);
        using var connectionB = await ConnectAsync(redis);
        var replicaA = NewService(connectionA, TimeSpan.FromMinutes(1));
        var replicaB = NewService(connectionB, TimeSpan.FromMinutes(1));

        Assert.True(await replicaA.TryAcquireSessionAsync("ref-test", "tab-1", cancellationToken));
        Assert.False(await replicaB.TryAcquireSessionAsync("ref-test", "tab-2", cancellationToken));
        Assert.True(await replicaB.TryAcquireSessionAsync("other-ref-test", "tab-2", cancellationToken));

        Assert.True(await replicaB.RenewSessionAsync("ref-test", "tab-1", cancellationToken));
        Assert.False(await replicaB.RenewSessionAsync("ref-test", "tab-2", cancellationToken));

        await replicaB.ReleaseSessionAsync("ref-test", "tab-2");
        Assert.False(await replicaB.TryAcquireSessionAsync("ref-test", "tab-2", cancellationToken));

        await replicaB.ReleaseSessionAsync("ref-test", "tab-1");
        Assert.True(await replicaB.TryAcquireSessionAsync("ref-test", "tab-2", cancellationToken));
    }

    [Fact]
    public async Task AnUnrenewedLeaseExpiresAndItsFormerHolderCannotTakeItBack()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var redis = await StartRedisAsync(cancellationToken);
        using var connection = await ConnectAsync(redis);
        var service = NewService(connection, TimeSpan.FromMilliseconds(300));

        Assert.True(await service.TryAcquireSessionAsync("ref-test", "crashed-tab", cancellationToken));
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

        Assert.True(await service.TryAcquireSessionAsync("ref-test", "new-tab", cancellationToken));
        Assert.False(await service.RenewSessionAsync("ref-test", "crashed-tab", cancellationToken));
        await service.ReleaseSessionAsync("ref-test", "crashed-tab");
        Assert.True(await service.RenewSessionAsync("ref-test", "new-tab", cancellationToken));
    }

    private static async Task<RedisContainer> StartRedisAsync(CancellationToken cancellationToken)
    {
        var redis = new RedisBuilder("redis:latest")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
            .Build();
        await redis.StartAsync(cancellationToken);
        return redis;
    }

    private static async Task<ConnectionMultiplexer> ConnectAsync(RedisContainer redis)
    {
        var options = ConfigurationOptions.Parse(redis.GetConnectionString());
        options.AbortOnConnectFail = false;
        return await ConnectionMultiplexer.ConnectAsync(options);
    }

    private static RedisRefTestSessionService NewService(IConnectionMultiplexer redis, TimeSpan lease) =>
        new(redis, new TestHostEnvironment(), NullLogger<RedisRefTestSessionService>.Instance, lease);

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "RefTestManagement";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
