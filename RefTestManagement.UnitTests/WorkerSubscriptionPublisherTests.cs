using DotNet.Testcontainers.Builders;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Hosting;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using HotChocolate.Subscriptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class WorkerSubscriptionPublisherTests
{
    [Fact]
    public async Task WorkerPublishesTypedEventsToApiSubscribersThroughSharedRedis()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var redis = await StartRedisAsync(cancellationToken);
        var apiServices = new ServiceCollection();
        var workerServices = new ServiceCollection();
        var configuration = CreateSharedConfiguration();

        apiServices.AddRefTestManagementHosting(configuration, HostingMode.Api);
        apiServices.AddGraphQLServer()
            .AddRedisSubscriptions(sp => sp.GetRequiredService<IConnectionMultiplexer>());
        workerServices.AddRefTestManagementHosting(configuration, HostingMode.Worker);
        AddRedisConnection(apiServices, redis);
        AddRedisConnection(workerServices, redis);

        using var apiProvider = apiServices.BuildServiceProvider();
        using var workerProvider = workerServices.BuildServiceProvider();
        var receiver = apiProvider.GetRequiredService<ITopicEventReceiver>();
        await using var source = await receiver.SubscribeAsync<RefTestSubscriptionMessage>(
            RefTestSubscriptionService.GlobalTopic,
            cancellationToken);
        var messageTask = ReadFirstAsync(source.ReadEventsAsync(), cancellationToken);

        var id = Guid.NewGuid();
        var sentAt = DateTime.UtcNow;
        using var workerScope = workerProvider.CreateScope();
        var publisher = workerScope.ServiceProvider.GetRequiredService<IRefTestSubscriptionService>();
        await publisher.PublishInvitationSentAsync(id, sentAt, cancellationToken);

        var message = await messageTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.Equal(nameof(RefTestInvitationSentEvent), message.EventType);
        var invitationSent = Assert.IsType<RefTestInvitationSentEvent>(message.DeserializeEvent());
        Assert.Equal(id, invitationSent.Id);
        Assert.Equal(sentAt, invitationSent.SentAt);
    }

    private static async Task<RedisContainer> StartRedisAsync(CancellationToken cancellationToken)
    {
        var redis = new RedisBuilder("redis:latest")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
            .Build();
        await redis.StartAsync(cancellationToken);
        return redis;
    }

    private static void AddRedisConnection(IServiceCollection services, RedisContainer redis)
    {
        var options = ConfigurationOptions.Parse(redis.GetConnectionString());
        options.AbortOnConnectFail = false;
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));
    }

    private static IConfiguration CreateSharedConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "SQLite",
                ["ConnectionStrings:RefTestManagement"] = "Data Source=:memory:",
                ["AuditLogConfiguration:EnableCleanup"] = "false",
                ["State:Mode"] = "Shared",
                ["RedisConfiguration:Endpoint"] = "rediss://:test@localhost:6379",
                ["PrivacyChallengeConfiguration:RateLimitBackend"] = "Redis",
                ["PrivacyChallengeConfiguration:HmacSecret"] = new string('x', 32)
            })
            .Build();

    private static async Task<TMessage> ReadFirstAsync<TMessage>(
        IAsyncEnumerable<TMessage> messages,
        CancellationToken cancellationToken)
    {
        await foreach (var message in messages.WithCancellation(cancellationToken))
            return message;

        throw new InvalidOperationException("The subscription ended without an event.");
    }
}
