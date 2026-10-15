using System.Net;
using System.Text;
using Handball.Belgium.RefTestManagement.Keycloak;
using Handball.Belgium.RefTestManagement.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class KeycloakPermissionSyncServiceTests
{
    private const string Authority = "https://login.example.test/realms/test-realm";
    private const string AdminBase = "https://login.example.test/admin/realms/test-realm";

    [Fact]
    public async Task CreatesEveryMissingDistinctRoleOnApiClient()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ScriptedHandler(new KeycloakResponses());
        await using var provider = BuildProvider(handler);
        var sync = provider.GetRequiredService<IIdentityPermissionSync>();

        await sync.SyncPermissionsAsync(
            ["ref-tests:create", "ref-tests:create", "superadmin"],
            cancellationToken);

        Assert.Equal($"{Authority}/protocol/openid-connect/token", handler.Requests[0].Url);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Contains("grant_type=client_credentials", handler.Requests[0].Body);
        Assert.Contains("client_id=ref-test-management-admin", handler.Requests[0].Body);

        Assert.Equal($"{AdminBase}/clients?clientId=reftest-api", handler.Requests[1].Url);
        Assert.Equal("Bearer test-token", handler.Requests[1].Authorization);

        var creates = handler.Requests.Where(r => r.Method == HttpMethod.Post && r.Url.EndsWith("/roles", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, creates.Count);
        Assert.Equal($"{AdminBase}/clients/internal-id/roles", creates[0].Url);
        Assert.Contains("\"name\":\"ref-tests:create\"", creates[0].Body);
        Assert.Contains("\"name\":\"superadmin\"", creates[1].Body);
    }

    [Fact]
    public async Task ExistingRoleIsNotRecreated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ScriptedHandler(new KeycloakResponses { RoleExists = true });
        await using var provider = BuildProvider(handler);
        var sync = provider.GetRequiredService<IIdentityPermissionSync>();

        await sync.SyncPermissionsAsync(["ref-tests:create"], cancellationToken);

        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post && r.Url.EndsWith("/roles", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConflictOnCreateIsTreatedAsAlreadyPresent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ScriptedHandler(new KeycloakResponses { CreateStatus = HttpStatusCode.Conflict });
        await using var provider = BuildProvider(handler);
        var sync = provider.GetRequiredService<IIdentityPermissionSync>();

        await sync.SyncPermissionsAsync(["ref-tests:create"], cancellationToken);

        Assert.Equal(HttpMethod.Post, handler.Requests[^1].Method);
        Assert.EndsWith("/roles", handler.Requests[^1].Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingApiClientFailsTheSync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ScriptedHandler(new KeycloakResponses { ClientsJson = "[]" });
        await using var provider = BuildProvider(handler);
        var sync = provider.GetRequiredService<IIdentityPermissionSync>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sync.SyncPermissionsAsync(["ref-tests:create"], cancellationToken));
    }

    [Fact]
    public async Task MissingAdminSecretRefusesToSyncWithoutCallingKeycloak()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ScriptedHandler(new KeycloakResponses());
        await using var provider = BuildProvider(handler, adminSecret: string.Empty);
        var sync = provider.GetRequiredService<IIdentityPermissionSync>();

        Assert.False(sync.IsConfigured);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sync.SyncPermissionsAsync(["ref-tests:create"], cancellationToken));
        Assert.Empty(handler.Requests);
    }

    private static ServiceProvider BuildProvider(ScriptedHandler handler, string adminSecret = "test-secret")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Provider"] = "Keycloak",
                ["Identity:Keycloak:Authority"] = Authority,
                ["Identity:Keycloak:Audience"] = "reftest-api",
                ["Identity:Keycloak:AdminClientId"] = "ref-test-management-admin",
                ["Identity:Keycloak:AdminClientSecret"] = adminSecret,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddKeycloakPermissionSync(configuration);
        services.AddTransient<IIdentityPermissionSync>(serviceProvider =>
            serviceProvider.GetRequiredService<KeycloakPermissionSyncService>());
        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler(() => handler));

        return services.BuildServiceProvider();
    }

    private sealed class KeycloakResponses
    {
        public bool RoleExists { get; init; }

        public HttpStatusCode CreateStatus { get; init; } = HttpStatusCode.Created;

        public string ClientsJson { get; init; } =
            """[{"id":"internal-id","clientId":"reftest-api"}]""";

        public HttpResponseMessage Respond(HttpRequestMessage request)
        {
            var url = request.RequestUri!.ToString();

            if (url.EndsWith("/protocol/openid-connect/token", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """{"access_token":"test-token"}""");
            }

            if (url.StartsWith($"{AdminBase}/clients?clientId=", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, ClientsJson);
            }

            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(RoleExists ? HttpStatusCode.OK : HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(CreateStatus);
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private sealed class ScriptedHandler(KeycloakResponses responses) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!.ToString(),
                body,
                request.Headers.Authorization?.ToString()));

            return responses.Respond(request);
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, string Url, string? Body, string? Authorization);
}
