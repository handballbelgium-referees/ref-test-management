using Handball.Belgium.RefTestManagement.Hosting;
using Handball.Belgium.RefTestManagement.Hosting.BackgroundServices;
using Handball.Belgium.RefTestManagement.Hosting.Services;
using Handball.Belgium.RefTestManagement.Auth0.Services;
using HotChocolate.Execution;
using Microsoft.Extensions.Caching.Memory;
using HotChocolate.Subscriptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using QuestPDF.Infrastructure;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class HostingModeTests
{
    [Fact]
    public void ApiDefaultsToAll()
    {
        var configuration = CreateConfiguration();

        Assert.Equal(HostingMode.All, HostingModeConfiguration.GetApiMode(configuration));
    }

    [Theory]
    [InlineData("All", HostingMode.All)]
    [InlineData("Api", HostingMode.Api)]
    [InlineData("aPi", HostingMode.Api)]
    public void ApiAcceptsAllAndApiModes(string configuredMode, HostingMode expectedMode)
    {
        var configuration = CreateConfiguration(("Hosting:Mode", configuredMode));

        Assert.Equal(expectedMode, HostingModeConfiguration.GetApiMode(configuration));
    }

    [Fact]
    public void ApiRejectsWorkerMode()
    {
        var configuration = CreateConfiguration(("Hosting:Mode", "Worker"));

        Assert.Throws<InvalidOperationException>(() =>
            HostingModeConfiguration.GetApiMode(configuration));
    }

    [Fact]
    public void WorkerDefaultsToWorker()
    {
        var configuration = CreateConfiguration();

        Assert.Equal(HostingMode.Worker, HostingModeConfiguration.GetWorkerMode(configuration));
    }

    [Fact]
    public void WorkerRejectsApiModes()
    {
        foreach (var configuredMode in new[] { "All", "Api" })
        {
            var configuration = CreateConfiguration(("Hosting:Mode", configuredMode));

            Assert.Throws<InvalidOperationException>(() =>
                HostingModeConfiguration.GetWorkerMode(configuration));
        }
    }

    [Fact]
    public void InvalidModeFailsWithSupportedValues()
    {
        var configuration = CreateConfiguration(("Hosting:Mode", "ContainerApps"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            HostingModeConfiguration.GetApiMode(configuration));

        Assert.Contains("All, Api, Worker", exception.Message);
    }

    [Theory]
    [InlineData(HostingMode.All, 6)]
    [InlineData(HostingMode.Api, 0)]
    [InlineData(HostingMode.Worker, 6)]
    public void OnlyAllAndWorkerModesRegisterBackgroundServices(
        HostingMode mode,
        int expectedServiceCount)
    {
        var configuration = CreateHostingConfiguration(mode == HostingMode.Worker);
        var services = new ServiceCollection();

        services.AddRefTestManagementHosting(configuration, mode);

        var backgroundServices = new HashSet<Type>
        {
            typeof(PermissionSyncService),
            typeof(RefTestExpirationService),
            typeof(PrivacyRetentionService),
            typeof(PersonalDataExportRequestCleanupService),
            typeof(PrivacyWithdrawalCleanupService),
            typeof(BackgroundJobService),
            typeof(AuditLogCleanupService)
        };
        Assert.Equal(expectedServiceCount, services.Count(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType is { } type &&
            backgroundServices.Contains(type)));
        Assert.Equal(
            mode is HostingMode.All or HostingMode.Worker,
            services.Any(descriptor => descriptor.ImplementationType == typeof(BackgroundJobService)));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IMemoryCache));
        Assert.Equal(LicenseType.Community, QuestPDF.Settings.License);
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IAuth0ManagementService>());

        if (mode == HostingMode.Worker)
        {
            Assert.Contains(services, descriptor =>
                descriptor.ServiceType == typeof(IRefTestSessionService) &&
                descriptor.ImplementationType == typeof(RedisRefTestSessionService));
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITopicEventSender));
        }
    }

    [Fact]
    public void WorkerRequiresSharedState()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddRefTestManagementHosting(CreateHostingConfiguration(), HostingMode.Worker));
    }

    [Fact]
    public void WorkerModeDoesNotRegisterAnHttpServer()
    {
        var services = new ServiceCollection();

        services.AddRefTestManagementHosting(
            CreateHostingConfiguration(sharedState: true),
            HostingMode.Worker);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IServer));
    }

    [Fact]
    public async Task WorkerGraphQlSchemaHasAQueryRoot()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRefTestManagementHosting(
            CreateHostingConfiguration(sharedState: true),
            HostingMode.Worker);

        await using var provider = services.BuildServiceProvider();
        var executorProvider = provider.GetRequiredService<IRequestExecutorProvider>();
        var executor = await executorProvider.GetExecutorAsync(
            Assert.Single(executorProvider.SchemaNames),
            TestContext.Current.CancellationToken);

        Assert.Equal("WorkerQuery", executor.Schema.QueryType.Name);
        Assert.Contains(executor.Schema.QueryType.Fields, field => field.Name == "role");
    }

    private static IConfiguration CreateHostingConfiguration(bool sharedState = false)
    {
        var values = new Dictionary<string, string?>
        {
            ["DatabaseProvider"] = "SQLite",
            ["ConnectionStrings:RefTestManagement"] = "Data Source=:memory:",
            ["AuditLogConfiguration:EnableCleanup"] = "false"
        };
        if (sharedState)
        {
            values["State:Mode"] = "Shared";
            values["RedisConfiguration:Endpoint"] = "rediss://:test@localhost:6379";
            values["PrivacyChallengeConfiguration:RateLimitBackend"] = "Redis";
            values["PrivacyChallengeConfiguration:HmacSecret"] = new string('x', 32);
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static IConfiguration CreateConfiguration(params (string Key, string Value)[] values)
    {
        var settings = values.ToDictionary(value => value.Key, value => (string?)value.Value);
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
