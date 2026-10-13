using Handball.Belgium.RefTestManagement.Application.Configurations;
using Microsoft.AspNetCore.DataProtection;

namespace Handball.Belgium.RefTestManagement.Hosting;

public interface IHostingProviderAdapter
{
    string ProviderName { get; }

    Action<IDataProtectionBuilder>? CreateDataProtectionKeyProtection(
        IConfiguration configuration,
        IHostEnvironment environment);

    // Password-mode Redis is configured by shared Hosting; adapters customize Redis only for provider auth.
    bool SupportsRedisTokenAuthentication => false;

    Task ConfigureRedisConnectionAsync(
        IServiceCollection services,
        IConfiguration configuration) => Task.CompletedTask;
}

public sealed class HostingProviderConfiguration
{
    private const string LocalProviderName = "Local";

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly IHostingProviderAdapter? _adapter;

    private HostingProviderConfiguration(
        string provider,
        IConfiguration configuration,
        IHostEnvironment environment,
        IHostingProviderAdapter? adapter)
    {
        Provider = provider;
        _configuration = configuration;
        _environment = environment;
        _adapter = adapter;
    }

    public string Provider { get; }

    public static HostingProviderConfiguration Create(
        IConfiguration configuration,
        IHostEnvironment environment,
        IEnumerable<IHostingProviderAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(adapters);

        var provider = GetProvider(configuration);
        if (string.Equals(provider, LocalProviderName, StringComparison.OrdinalIgnoreCase))
            return new HostingProviderConfiguration(provider, configuration, environment, null);

        var matchingAdapters = adapters
            .Where(adapter => string.Equals(adapter.ProviderName, provider, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matchingAdapters.Length != 1)
            throw new InvalidOperationException(
                matchingAdapters.Length == 0
                    ? $"No adapter is registered for Hosting:Provider={provider}."
                    : $"More than one adapter is registered for Hosting:Provider={provider}.");

        return new HostingProviderConfiguration(provider, configuration, environment, matchingAdapters[0]);
    }

    public static string GetProvider(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var value = configuration["Hosting:Provider"];
        return string.IsNullOrWhiteSpace(value) ? LocalProviderName : value;
    }

    public Action<IDataProtectionBuilder>? CreateDataProtectionKeyProtection()
    {
        if (string.Equals(Provider, LocalProviderName, StringComparison.OrdinalIgnoreCase))
        {
            if (!_environment.IsDevelopment())
                throw new InvalidOperationException(
                    "A non-development host must configure a Data Protection key-encryption provider.");

            return null;
        }

        return _adapter!.CreateDataProtectionKeyProtection(_configuration, _environment);
    }

    public Task ConfigureRedisConnectionAsync(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var redisConfiguration =
            _configuration.GetSection("RedisConfiguration").Get<RedisConfiguration>() ?? new();
        var redisIsRequired =
            _configuration.GetSection("State").Get<StateConfiguration>()?.Mode == StateMode.Shared ||
            _configuration.GetSection("PrivacyChallengeConfiguration")
                .Get<PrivacyChallengeConfiguration>()?.RateLimitBackend == PrivacyChallengeRateLimitBackend.Redis;

        if (redisConfiguration.AuthenticationMode == RedisAuthenticationMode.Token &&
            redisConfiguration.IsConfigured &&
            redisIsRequired &&
            _adapter?.SupportsRedisTokenAuthentication != true)
        {
            throw new InvalidOperationException(
                $"Redis token authentication is not supported by Hosting:Provider={Provider}.");
        }

        return _adapter?.ConfigureRedisConnectionAsync(services, _configuration) ?? Task.CompletedTask;
    }
}
