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
        Assert.Contains("client_id=reftest-management-admin", handler.Requests[0].Body);

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

    [Fact]
    public async Task ReadReturnsClientRolesOfEnabledUser()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ScriptedHandler(new KeycloakResponses());
        await using var provider = BuildProvider(handler);
        var sync = provider.GetRequiredService<IIdentityPermissionSync>();

        var permissions = await sync.GetUserPermissionsAsync("user-1", cancellationToken);

        Assert.Equal(["ref-tests:view-list", "superadmin"], permissions.Order());
        Assert.Contains(handler.Requests, r => r.Url == $"{AdminBase}/users/user-1/role-mappings/clients/internal-id");
    }

    [Fact]
    public async Task ReadIncludesGroupRolesAndAncestorGroupRoles()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ScriptedHandler(new KeycloakResponses
        {
            GroupsJson = """[{"id":"g-sub","path":"/handball/referees/sub"}]""",
            GroupsByPath = new Dictionary<string, string>
            {
                ["handball"] = """{"id":"g-root"}""",
                ["handball/referees"] = """{"id":"g-mid"}""",
            },
            GroupMappings = new Dictionary<string, string>
            {
                ["g-sub"] = """[{"id":"role-edit","name":"ref-tests:edit"}]""",
                ["g-root"] = """[{"id":"role-delete","name":"ref-tests:delete"}]""",
            },
        });
        await using var provider = BuildProvider(handler);
        var sync = provider.GetRequiredService<IIdentityPermissionSync>();

        var permissions = await sync.GetUserPermissionsAsync("user-1", cancellationToken);

        Assert.Equal(
            ["ref-tests:delete", "ref-tests:edit", "ref-tests:view-list", "superadmin"],
            permissions.Order());
        Assert.Contains(handler.Requests, r => r.Url == $"{AdminBase}/groups/g-root/role-mappings/clients/internal-id");
        Assert.Contains(handler.Requests, r => r.Url == $"{AdminBase}/groups/g-mid/role-mappings/clients/internal-id");
    }

    [Fact]
    public async Task ReadExpandsCompositeRolesAndStopsOnCycles()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ScriptedHandler(new KeycloakResponses
        {
            CompositeRoles = new Dictionary<string, string>
            {
                ["role-super"] = """[{"id":"role-edit","name":"ref-tests:edit","composite":true},{"id":"role-x","name":"ref-tests:x"}]""",
                ["role-edit"] = """[{"id":"role-super","name":"superadmin","composite":true}]""",
            },
        });
        await using var provider = BuildProvider(handler);
        var sync = provider.GetRequiredService<IIdentityPermissionSync>();

        var permissions = await sync.GetUserPermissionsAsync("user-1", cancellationToken);

        Assert.Equal(["ref-tests:edit", "ref-tests:view-list", "ref-tests:x", "superadmin"], permissions.Order());
        Assert.Equal(1, handler.Requests.Count(r => r.Url.Contains("/roles-by-id/role-super/composites/", StringComparison.Ordinal)));
        Assert.Equal(1, handler.Requests.Count(r => r.Url.Contains("/roles-by-id/role-edit/composites/", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ReadRejectsDisabledUserWithoutReadingRoles()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ScriptedHandler(new KeycloakResponses { UserJson = """{"id":"user-1","enabled":false}""" });
        await using var provider = BuildProvider(handler);
        var sync = provider.GetRequiredService<IIdentityPermissionSync>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sync.GetUserPermissionsAsync("user-1", cancellationToken));
        Assert.DoesNotContain(handler.Requests, r => r.Url.Contains("/role-mappings/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadRejectsMissingUser()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ScriptedHandler(new KeycloakResponses { UserStatus = HttpStatusCode.NotFound });
        await using var provider = BuildProvider(handler);
        var sync = provider.GetRequiredService<IIdentityPermissionSync>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sync.GetUserPermissionsAsync("user-1", cancellationToken));
    }

    [Fact]
    public async Task ReadThrowsOnKeycloakOutageInsteadOfReturningEmpty()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ScriptedHandler(new KeycloakResponses { MappingsStatus = HttpStatusCode.InternalServerError });
        await using var provider = BuildProvider(handler);
        var sync = provider.GetRequiredService<IIdentityPermissionSync>();

        await Assert.ThrowsAnyAsync<HttpRequestException>(() =>
            sync.GetUserPermissionsAsync("user-1", cancellationToken));
    }

    [Fact]
    public async Task ReadRefusesWithoutCallingKeycloakWhenUnconfigured()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new ScriptedHandler(new KeycloakResponses());
        await using var provider = BuildProvider(handler, adminSecret: string.Empty);
        var sync = provider.GetRequiredService<IIdentityPermissionSync>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sync.GetUserPermissionsAsync("user-1", cancellationToken));
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
                ["Identity:Keycloak:AdminClientId"] = "reftest-management-admin",
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

        public HttpStatusCode UserStatus { get; init; } = HttpStatusCode.OK;

        public string UserJson { get; init; } = """{"id":"user-1","enabled":true}""";

        public HttpStatusCode MappingsStatus { get; init; } = HttpStatusCode.OK;

        public string MappingsJson { get; init; } =
            """[{"id":"role-view","name":"ref-tests:view-list"},{"id":"role-super","name":"superadmin","composite":true}]""";

        public string GroupsJson { get; init; } = "[]";

        /// <summary>Client role mappings of a group, keyed by group id.</summary>
        public IReadOnlyDictionary<string, string> GroupMappings { get; init; } = new Dictionary<string, string>();

        /// <summary>Group lookups by path, keyed by the escaped path after <c>group-by-path/</c>.</summary>
        public IReadOnlyDictionary<string, string> GroupsByPath { get; init; } = new Dictionary<string, string>();

        /// <summary>Client composite roles, keyed by parent role id.</summary>
        public IReadOnlyDictionary<string, string> CompositeRoles { get; init; } = new Dictionary<string, string>();

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

            if (url.Contains("/groups/", StringComparison.Ordinal) && url.Contains("/role-mappings/clients/", StringComparison.Ordinal))
            {
                var groupEntry = GroupMappings.FirstOrDefault(entry => url.Contains($"/groups/{entry.Key}/role-mappings/", StringComparison.Ordinal));
                return Json(HttpStatusCode.OK, groupEntry.Value ?? "[]");
            }

            if (url.Contains("/role-mappings/clients/", StringComparison.Ordinal))
            {
                return MappingsStatus == HttpStatusCode.OK
                    ? Json(HttpStatusCode.OK, MappingsJson)
                    : new HttpResponseMessage(MappingsStatus);
            }

            if (url.Contains("/composites/clients/", StringComparison.Ordinal))
            {
                var compositeEntry = CompositeRoles.FirstOrDefault(entry => url.Contains($"/roles-by-id/{entry.Key}/composites/", StringComparison.Ordinal));
                return Json(HttpStatusCode.OK, compositeEntry.Value ?? "[]");
            }

            if (url.Contains("/groups/group-by-path/", StringComparison.Ordinal))
            {
                var pathEntry = GroupsByPath.FirstOrDefault(entry => url.EndsWith($"/group-by-path/{entry.Key}", StringComparison.Ordinal));
                return pathEntry.Value is { } groupJson
                    ? Json(HttpStatusCode.OK, groupJson)
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (url.EndsWith("/users/user-1/groups", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, GroupsJson);
            }

            if (url.StartsWith($"{AdminBase}/users/", StringComparison.Ordinal))
            {
                return UserStatus == HttpStatusCode.OK
                    ? Json(HttpStatusCode.OK, UserJson)
                    : new HttpResponseMessage(UserStatus);
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
