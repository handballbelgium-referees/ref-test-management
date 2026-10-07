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
    private const string TestIndexHtml = "<!doctype html><html><body><app-root></app-root></body></html>";

    [Fact]
    public async Task ProductionApiSetsSecurityHeadersAndRejectsGraphQlGetMutations()
    {
        using var testEnvironment = new TestApiEnvironment();
        using var factory = new ProductionApiFactory(testEnvironment.WebRootPath);
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

        var responsePolicy = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("default-src 'self';", responsePolicy);
        Assert.Contains("script-src 'self';", responsePolicy);
        Assert.Contains("style-src 'self';", responsePolicy);
        Assert.Contains("object-src 'none';", responsePolicy);
        Assert.Contains("frame-src 'none';", responsePolicy);
        Assert.Contains("connect-src 'self';", responsePolicy);
        Assert.DoesNotContain("wss:", responsePolicy);
        Assert.Contains("style-src-elem 'self' 'unsafe-inline';", responsePolicy);
        Assert.Contains("style-src-attr 'unsafe-inline';", responsePolicy);
        Assert.DoesNotContain("https:", responsePolicy);
        Assert.DoesNotContain("script-src 'self' 'unsafe-inline'", responsePolicy);
        Assert.DoesNotContain("style-src 'self' 'unsafe-inline'", responsePolicy);
        Assert.DoesNotContain("nonce-", responsePolicy);
        Assert.Equal(
            "max-age=31536000",
            response.Headers.GetValues("Strict-Transport-Security").Single());
        Assert.DoesNotContain(
            "includeSubDomains",
            response.Headers.GetValues("Strict-Transport-Security").Single(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        using var errorDocument = JsonDocument.Parse(responseBody);
        Assert.True(errorDocument.RootElement.TryGetProperty("errors", out var errors));
        Assert.NotEmpty(errors.EnumerateArray());

        using var indexResponse = await client.GetAsync("/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        var indexPolicy = indexResponse.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Equal(responsePolicy, indexPolicy);
        var indexHtml = await indexResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TestIndexHtml, indexHtml);

        using var directIndexResponse = await client.GetAsync(
            "/index.html",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, directIndexResponse.StatusCode);
        Assert.Equal(
            TestIndexHtml,
            await directIndexResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private sealed class ProductionApiFactory(string webRootPath) : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseWebRoot(webRootPath);
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
        public string WebRootPath { get; } =
            Path.Combine(Path.GetTempPath(), $"ref-test-management-api-{Guid.NewGuid():N}");

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
            Directory.CreateDirectory(WebRootPath);
            File.WriteAllText(
                Path.Combine(WebRootPath, "index.html"),
                TestIndexHtml);

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

            Directory.Delete(WebRootPath, recursive: true);
        }
    }
}
