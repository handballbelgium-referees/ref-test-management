using Azure.Core;
using Azure.Identity;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Handball.Belgium.RefTestManagement.Hosting.Azure;

public static class AzureRedisConfiguration
{
    public static async Task ConfigureRedisConnectionAsync(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConfiguration =
            configuration.GetSection("RedisConfiguration").Get<RedisConfiguration>() ?? new();
        if (redisConfiguration.AuthenticationMode != RedisAuthenticationMode.Token ||
            !redisConfiguration.IsConfigured ||
            !IsRedisRequired(configuration))
            return;

        if (!string.Equals(configuration["Hosting:Provider"], "Azure", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Redis token authentication requires Hosting:Provider=Azure.");

        var managedIdentityClientId = configuration["Azure:Redis:ManagedIdentityClientId"];
        if (!Guid.TryParse(managedIdentityClientId, out var parsedClientId) || parsedClientId == Guid.Empty)
            throw new InvalidOperationException(
                "Azure:Redis:ManagedIdentityClientId must be the client ID of an attached user-assigned identity.");

        var credential = new ManagedIdentityCredential(
            ManagedIdentityId.FromUserAssignedClientId(managedIdentityClientId!));
        var options = await CreateConnectionOptionsAsync(redisConfiguration, credential).ConfigureAwait(false);

        services.Replace(ServiceDescriptor.Singleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(options)));
    }

    public static async Task<ConfigurationOptions> CreateConnectionOptionsAsync(
        RedisConfiguration redisConfiguration,
        TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(redisConfiguration);
        ArgumentNullException.ThrowIfNull(credential);
        redisConfiguration.Validate();

        if (!redisConfiguration.IsConfigured ||
            redisConfiguration.AuthenticationMode != RedisAuthenticationMode.Token)
            throw new InvalidOperationException(
                "Azure Redis connection options require a configured TLS endpoint in Token mode.");

        var endpoint = new Uri(redisConfiguration.Endpoint!);
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            BacklogPolicy = BacklogPolicy.Default,
            Ssl = true,
            Protocol = RedisProtocol.Resp3,
            ConnectTimeout = 1000,
            AsyncTimeout = 1000,
            SyncTimeout = 1000
        };
        options.EndPoints.Add(endpoint.Host, endpoint.Port);

        await options.ConfigureForAzureWithTokenCredentialAsync(credential).ConfigureAwait(false);
        return options;
    }

    private static bool IsRedisRequired(IConfiguration configuration) =>
        configuration.GetSection("State").Get<StateConfiguration>()?.Mode == StateMode.Shared ||
        configuration.GetSection("PrivacyChallengeConfiguration")
            .Get<PrivacyChallengeConfiguration>()?.RateLimitBackend == PrivacyChallengeRateLimitBackend.Redis;
}
