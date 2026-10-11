using Handball.Belgium.RefTestManagement.Application.Configurations;

namespace Handball.Belgium.RefTestManagement.Hosting;

internal static class RedisServiceCollectionExtensions
{
    public static void AddRefTestManagementRedis(
        this IServiceCollection services,
        RedisConfiguration configuration)
    {
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var endpoint = new Uri(configuration.Endpoint!);
            var credentials = endpoint.UserInfo.Split(':', 2);
            var redisOptions = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                Ssl = true,
                User = Uri.UnescapeDataString(credentials[0]),
                Password = Uri.UnescapeDataString(credentials.Length > 1 ? credentials[1] : string.Empty),
                ConnectTimeout = 1000,
                AsyncTimeout = 1000,
                SyncTimeout = 1000
            };
            redisOptions.EndPoints.Add(endpoint.Host, endpoint.Port);
            return ConnectionMultiplexer.Connect(redisOptions);
        });
    }
}
