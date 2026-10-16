using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Hosting.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class AzureRedisConfigurationTests
{
    [Fact]
    public async Task TokenConnectionUsesTlsResp3AndRefreshableCredential()
    {
        var credential = new TestTokenCredential();
        var redisConfiguration = new RedisConfiguration
        {
            AuthenticationMode = RedisAuthenticationMode.Token,
            Endpoint = "rediss://redis.example:10000"
        };

        var options = await AzureRedisConfiguration.CreateConnectionOptionsAsync(
            redisConfiguration,
            credential);

        Assert.True(options.Ssl);
        Assert.Equal(RedisProtocol.Resp3, options.Protocol);
        Assert.False(options.AbortOnConnectFail);
        Assert.Equal(BacklogPolicy.Default, options.BacklogPolicy);
        var endpoint = Assert.IsType<DnsEndPoint>(Assert.Single(options.EndPoints));
        Assert.Equal("redis.example", endpoint.Host);
        Assert.Equal(10000, endpoint.Port);
        Assert.True(credential.TokenRequests > 0);
    }

    [Theory]
    [InlineData("rediss://:password@redis.example:10000")]
    [InlineData("redis://redis.example:10000")]
    [InlineData("rediss://:10000")]
    [InlineData("not a URI")]
    public async Task TokenConnectionRejectsCredentialsOrNonTlsEndpoints(string endpoint)
    {
        var redisConfiguration = new RedisConfiguration
        {
            AuthenticationMode = RedisAuthenticationMode.Token,
            Endpoint = endpoint
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AzureRedisConfiguration.CreateConnectionOptionsAsync(
                redisConfiguration,
                new TestTokenCredential()));
    }

    [Fact]
    public async Task TokenConnectionRequiresAzureProvider()
    {
        var configuration = CreateConfiguration(
            ("Hosting:Provider", "Local"),
            ("State:Mode", "Shared"),
            ("RedisConfiguration:AuthenticationMode", "Token"),
            ("RedisConfiguration:Endpoint", "rediss://redis.example:10000"),
            ("Azure:Redis:ManagedIdentityClientId", "a5eae9ce-0345-49ba-a2b0-4699eb55ea1a"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AzureRedisConfiguration.ConfigureRedisConnectionAsync(
                new ServiceCollection(),
                configuration));
    }

    [Fact]
    public async Task TokenConnectionRequiresAnAttachedUserAssignedIdentity()
    {
        var configuration = CreateConfiguration(
            ("Hosting:Provider", "Azure"),
            ("State:Mode", "Shared"),
            ("RedisConfiguration:AuthenticationMode", "Token"),
            ("RedisConfiguration:Endpoint", "rediss://redis.example:10000"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AzureRedisConfiguration.ConfigureRedisConnectionAsync(
                new ServiceCollection(),
                configuration));
    }

    [Theory]
    [InlineData("State:Mode", "1")]
    [InlineData("PrivacyChallengeConfiguration:RateLimitBackend", "1")]
    public async Task TokenConnectionRecognizesNumericEnumSettings(string key, string value)
    {
        var values = new Dictionary<string, string?>
        {
            ["Hosting:Provider"] = "Azure",
            ["State:Mode"] = "Local",
            ["PrivacyChallengeConfiguration:RateLimitBackend"] = "Local",
            ["RedisConfiguration:AuthenticationMode"] = "Token",
            ["RedisConfiguration:Endpoint"] = "rediss://redis.example:10000"
        };
        values[key] = value;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AzureRedisConfiguration.ConfigureRedisConnectionAsync(
                new ServiceCollection(),
                configuration));

        Assert.Contains("ManagedIdentityClientId", error.Message);
    }

    [Fact]
    public async Task PasswordModeDoesNotRegisterAzureRedisConnection()
    {
        var configuration = CreateConfiguration(
            ("Hosting:Provider", "Azure"),
            ("State:Mode", "Shared"),
            ("RedisConfiguration:AuthenticationMode", "Password"),
            ("RedisConfiguration:Endpoint", "rediss://:password@redis.example:10000"));
        var services = new ServiceCollection();

        await AzureRedisConfiguration.ConfigureRedisConnectionAsync(services, configuration);

        Assert.Empty(services);
    }

    private static IConfiguration CreateConfiguration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value =>
                new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();

    private sealed class TestTokenCredential : TokenCredential
    {
        public int TokenRequests { get; private set; }

        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            TokenRequests++;
            return new AccessToken(CreateTestToken(), DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            TokenRequests++;
            return ValueTask.FromResult(
                new AccessToken(CreateTestToken(), DateTimeOffset.UtcNow.AddHours(1)));
        }

        private static string CreateTestToken()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var header = Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
            var payload = Encode(JsonSerializer.Serialize(new
            {
                aud = "https://redis.azure.com",
                exp = now + 3600,
                iat = now,
                nbf = now,
                oid = "00000000-0000-0000-0000-000000000001",
                sub = "00000000-0000-0000-0000-000000000001",
                tid = "00000000-0000-0000-0000-000000000002"
            }));

            return $"{header}.{payload}.";
        }

        private static string Encode(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
    }
}
