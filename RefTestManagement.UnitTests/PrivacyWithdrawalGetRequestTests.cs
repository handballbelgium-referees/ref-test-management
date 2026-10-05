using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Handball.Belgium.RefTestManagement.UnitTests;

[CollectionDefinition("Production API integration", DisableParallelization = true)]
public sealed class ProductionApiIntegrationCollection
{
}

[Collection("Production API integration")]
public sealed class PrivacyWithdrawalGetRequestTests
{
    [Fact]
    public async Task ProductionApiSetsSecurityHeadersAndRejectsGraphQlGetMutations()
    {
        using var testEnvironment = new TestApiEnvironment();
        using var factory = new ProductionApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://security-test.example")
        });
        const string mutation = """
            mutation {
              confirmPrivacyWithdrawal(input: { key: "test-one-time-key" }) {
                privacyWithdrawalConfirmationResult {
                  accepted
                }
              }
            }
            """;

        using var response = await client.GetAsync(
            $"/graphql?query={Uri.EscapeDataString(mutation)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(
            "default-src 'self' https:; script-src 'self'; worker-src 'self' blob:; style-src 'self' 'unsafe-inline'; connect-src 'self' wss:; img-src 'self' data: https:; font-src 'self' data:; base-uri 'self'; form-action 'self';",
            response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal(
            "max-age=2592000",
            response.Headers.GetValues("Strict-Transport-Security").Single());
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        using var errorDocument = JsonDocument.Parse(responseBody);
        Assert.True(errorDocument.RootElement.TryGetProperty("errors", out var errors));
        Assert.NotEmpty(errors.EnumerateArray());
    }

    private sealed class ProductionApiFactory : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                // Do not start the production background workers against the throwaway test database.
                foreach (var descriptor in services
                             .Where(descriptor =>
                                 descriptor.ServiceType == typeof(IHostedService) &&
                                 descriptor.ImplementationType?.Assembly ==
                                 typeof(global::Program).Assembly)
                             .ToArray())
                {
                    services.Remove(descriptor);
                }
            });
        }
    }

    private sealed class TestApiEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> _previousValues = new()
        {
            ["DatabaseProvider"] = Environment.GetEnvironmentVariable("DatabaseProvider"),
            ["ConnectionStrings__RefTestManagement"] =
                Environment.GetEnvironmentVariable("ConnectionStrings__RefTestManagement"),
            ["ForwardedHeadersConfiguration__KnownNetworks__0"] =
                Environment.GetEnvironmentVariable("ForwardedHeadersConfiguration__KnownNetworks__0"),
            ["Auth0__Domain"] = Environment.GetEnvironmentVariable("Auth0__Domain"),
            ["Auth0__Audience"] = Environment.GetEnvironmentVariable("Auth0__Audience"),
            ["Auth0__ClientId"] = Environment.GetEnvironmentVariable("Auth0__ClientId"),
            ["Auth0__ClientSecret"] = Environment.GetEnvironmentVariable("Auth0__ClientSecret")
        };

        public TestApiEnvironment()
        {
            Environment.SetEnvironmentVariable("DatabaseProvider", "SQLite");
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__RefTestManagement",
                "Data Source=:memory:");
            Environment.SetEnvironmentVariable(
                "ForwardedHeadersConfiguration__KnownNetworks__0",
                "127.0.0.1/32");
            Environment.SetEnvironmentVariable("Auth0__Domain", "test.example");
            Environment.SetEnvironmentVariable("Auth0__Audience", "test-api");
            Environment.SetEnvironmentVariable("Auth0__ClientId", "test-client");
            Environment.SetEnvironmentVariable("Auth0__ClientSecret", "unused-test-placeholder");
        }

        public void Dispose()
        {
            foreach (var (name, value) in _previousValues)
                Environment.SetEnvironmentVariable(name, value);
        }
    }
}
