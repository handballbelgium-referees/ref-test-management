using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Auth0;
using Handball.Belgium.RefTestManagement.Auth0.Services;
using Handball.Belgium.RefTestManagement.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class Auth0ManagementServiceTests
{
    private const int MaximumApproverDiscoveryRequests = 128;

    [Fact]
    public async Task GetUsersWithPermissionAsyncRequiresMatchingAudienceForExactWildcardAndSuperadminGrants()
    {
        using var provider = Auth0ManagementTestServices.CreateProvider();
        var auth0Service = provider.GetRequiredService<IAuth0ManagementService>();

        var users = await auth0Service.GetUsersWithPermissionAsync(
            Permissions.RefTests.Approve,
            TestContext.Current.CancellationToken);

        var expected = new[]
        {
            "direct-target-exact@example.org",
            "direct-target-superadmin@example.org",
            "direct-target-wildcard@example.org",
            "role-target-exact@example.org",
            "role-target-superadmin@example.org",
            "role-target-wildcard@example.org"
        };

        Assert.Equal(
            expected.OrderBy(email => email, StringComparer.Ordinal),
            users.Select(user => user.Email).OrderBy(email => email, StringComparer.Ordinal));
    }

    [Fact]
    public async Task GetUserPermissionsAsyncCombinesDirectAndRoleGrantsForConfiguredAudience()
    {
        using var provider = Auth0ManagementTestServices.CreateProvider();
        var auth0Service = provider.GetRequiredService<IAuth0ManagementService>();

        var permissions = await auth0Service.GetUserPermissionsAsync(
            "fresh-user",
            TestContext.Current.CancellationToken);

        Assert.Equal(
            new[] { Permissions.Questions.Search, Permissions.RefTests.ViewList }.OrderBy(value => value),
            permissions.OrderBy(value => value));
    }

    [Theory]
    [InlineData("blocked-user", true)]
    [InlineData("unknown-user", null)]
    public async Task GetUserPermissionsAsyncFailsClosedForBlockedOrIndeterminateAccounts(
        string userId,
        bool? blocked)
    {
        var handler = new AccountStatusHandler(blocked);
        using var provider = Auth0ManagementTestServices.CreateProvider(handler);
        var auth0Service = provider.GetRequiredService<IAuth0ManagementService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            auth0Service.GetUserPermissionsAsync(userId, TestContext.Current.CancellationToken));

        Assert.Equal(1, handler.AccountStatusRequests);
        Assert.Equal(0, handler.PermissionRequests);
    }

    [Fact]
    public async Task GetUsersWithPermissionAsyncFailsInsteadOfCachingAnIncompleteBoundedResult()
    {
        var handler = new LargeDirectoryHandler();
        using var provider = Auth0ManagementTestServices.CreateProvider(
            handler,
            $"bounded-{Guid.NewGuid():N}.example.test");
        var auth0Service = provider.GetRequiredService<IAuth0ManagementService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            auth0Service.GetUsersWithPermissionAsync(
                Permissions.RefTests.Approve,
                TestContext.Current.CancellationToken));
        var firstCallCount = handler.ManagementApiRequests;

        Assert.Equal(MaximumApproverDiscoveryRequests, firstCallCount);
        Assert.Equal(
            MaximumApproverDiscoveryRequests - 6,
            handler.UserPermissionRequests);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            auth0Service.GetUsersWithPermissionAsync(
                Permissions.RefTests.Approve,
                TestContext.Current.CancellationToken));
        Assert.Equal(firstCallCount * 2, handler.ManagementApiRequests);
    }

    [Fact]
    public async Task GetUsersWithPermissionAsyncFailsWhenBudgetPreventsRoleApproverLookup()
    {
        var handler = new LargeDirectoryHandler(includeRoleFallback: true);
        using var provider = Auth0ManagementTestServices.CreateProvider(
            handler,
            $"role-fallback-{Guid.NewGuid():N}.example.test");
        var auth0Service = provider.GetRequiredService<IAuth0ManagementService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            auth0Service.GetUsersWithPermissionAsync(
                Permissions.RefTests.Approve,
                TestContext.Current.CancellationToken));

        Assert.Equal(MaximumApproverDiscoveryRequests, handler.ManagementApiRequests);
        Assert.Equal(123, handler.UserPermissionRequests);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            auth0Service.GetUsersWithPermissionAsync(
                Permissions.RefTests.Approve,
                TestContext.Current.CancellationToken));
        Assert.Equal(MaximumApproverDiscoveryRequests * 2, handler.ManagementApiRequests);
    }

    [Fact]
    public async Task SyncPermissionsAsyncPreservesExistingScopeDescriptions()
    {
        var handler = new ScopeSynchronizationHandler();
        using var provider = Auth0ManagementTestServices.CreateProvider(handler);
        var auth0Service = provider.GetRequiredService<IAuth0ManagementService>();

        await auth0Service.SyncPermissionsAsync(
            [Permissions.RefTests.Approve, Permissions.RefTests.Create],
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Patch, handler.PatchMethod);
        using var payload = JsonDocument.Parse(handler.PatchBody!);
        Assert.Equal(new[] { "scopes" }, payload.RootElement.EnumerateObject()
            .Select(property => property.Name));

        var scopes = payload.RootElement.GetProperty("scopes").EnumerateArray().ToArray();
        Assert.Equal(
            new[] { "custom:read", Permissions.RefTests.Approve, Permissions.RefTests.Create },
            scopes.Select(scope => scope.GetProperty("value").GetString()));
        Assert.Equal(
            new[] { "Tenant-managed description", "Custom approver description", Permissions.RefTests.Create },
            scopes.Select(scope => scope.GetProperty("description").GetString()));
    }

    [Fact]
    public async Task SyncPermissionsAsyncDoesNotOverwriteUnavailableScopeList()
    {
        var handler = new ScopeSynchronizationHandler(includeScopes: false);
        using var provider = Auth0ManagementTestServices.CreateProvider(handler);
        var auth0Service = provider.GetRequiredService<IAuth0ManagementService>();

        await auth0Service.SyncPermissionsAsync(
            [Permissions.RefTests.Create],
            TestContext.Current.CancellationToken);

        Assert.Null(handler.PatchMethod);
        Assert.Null(handler.PatchBody);
    }

    [Fact]
    public async Task GetUserPermissionsAsyncDoesNotLogSubjectThroughTypedClientPipeline()
    {
        const string subject = "auth0_subject_logging_sentinel_77";
        var handler = new Auth0SubjectSentinelHandler(subject);
        using var loggerProvider = new CapturingLoggerProvider();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth0:Domain"] = "tenant.example.test",
                ["Auth0:Audience"] = Auth0ManagementTestServices.ApiAudience,
                ["Auth0:ManagementClientId"] = "unit-test-client",
                ["Auth0:ManagementClientSecret"] = "unit-test-secret"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder =>
            builder.AddProvider(loggerProvider).SetMinimumLevel(LogLevel.Trace));
        services.AddMemoryCache();
        services.AddAuth0ManagementServices(configuration);
        services.Configure<HttpClientFactoryOptions>(
            nameof(IAuth0ManagementService),
            options => options.HttpMessageHandlerBuilderActions.Add(
                builder => builder.PrimaryHandler = handler));

        using var provider = services.BuildServiceProvider();
        var auth0Service = provider.GetRequiredService<IAuth0ManagementService>();

        var permissions = await auth0Service.GetUserPermissionsAsync(
            subject,
            TestContext.Current.CancellationToken);

        Assert.Empty(permissions);
        Assert.Equal(2, handler.UserPermissionRequestCount);
        Assert.Contains(
            handler.RequestUris,
            uri => uri.Contains($"/api/v2/users/{subject}/permissions", StringComparison.Ordinal));
        Assert.Contains(
            handler.RequestUris,
            uri => uri.Contains($"/api/v2/users/{subject}/roles", StringComparison.Ordinal));
        Assert.DoesNotContain(
            loggerProvider.Messages,
            message => message.Contains(subject, StringComparison.Ordinal));
    }

    private sealed class Auth0SubjectSentinelHandler(string subject) : HttpMessageHandler
    {
        private readonly ConcurrentQueue<string> _requestUris = new();
        private int _userPermissionRequestCount;

        public string[] RequestUris => _requestUris.ToArray();

        public int UserPermissionRequestCount => Volatile.Read(ref _userPermissionRequestCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var requestUri = request.RequestUri!;
            _requestUris.Enqueue(requestUri.AbsoluteUri);

            if (request.Method == HttpMethod.Post && requestUri.AbsolutePath == "/oauth/token")
                return Task.FromResult(Json(new { access_token = "unit-test-token", expires_in = 3600 }));

            if (request.Method == HttpMethod.Get &&
                requestUri.AbsolutePath == $"/api/v2/users/{subject}")
                return Task.FromResult(Json(new { blocked = false }));

            if (request.Method == HttpMethod.Get &&
                requestUri.AbsolutePath == $"/api/v2/users/{subject}/permissions")
            {
                if (Interlocked.Increment(ref _userPermissionRequestCount) == 1)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

                return Task.FromResult(Json(Array.Empty<object>()));
            }

            if (request.Method == HttpMethod.Get &&
                requestUri.AbsolutePath == $"/api/v2/users/{subject}/roles")
                return Task.FromResult(Json(Array.Empty<object>()));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage Json<T>(T value) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public string[] Messages => _messages.ToArray();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);
                messages.Enqueue(exception is null
                    ? message
                    : $"{message}{Environment.NewLine}{exception}");
            }
        }
    }

    private sealed class AccountStatusHandler(bool? blocked) : HttpMessageHandler
    {
        private int _accountStatusRequests;
        private int _permissionRequests;

        public int AccountStatusRequests => Volatile.Read(ref _accountStatusRequests);

        public int PermissionRequests => Volatile.Read(ref _permissionRequests);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path == "/oauth/token")
                return Task.FromResult(Json(new { access_token = "unit-test-token", expires_in = 3600 }));

            if (request.Method == HttpMethod.Get &&
                path.StartsWith("/api/v2/users/", StringComparison.Ordinal) &&
                path.Count(character => character == '/') == 4)
            {
                Interlocked.Increment(ref _accountStatusRequests);
                object response = blocked is null
                    ? new Dictionary<string, object?>()
                    : new { blocked = blocked.Value };
                return Task.FromResult(Json(response));
            }

            if (path.EndsWith("/permissions", StringComparison.Ordinal) ||
                path.EndsWith("/roles", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _permissionRequests);
                return Task.FromResult(Json(Array.Empty<object>()));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage Json<T>(T value) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }

    private sealed class LargeDirectoryHandler(bool includeRoleFallback = false) : HttpMessageHandler
    {
        private int _managementApiRequests;
        private int _userPermissionRequests;

        public int ManagementApiRequests => Volatile.Read(ref _managementApiRequests);

        public int UserPermissionRequests => Volatile.Read(ref _userPermissionRequests);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path == "/oauth/token")
                return Task.FromResult(Json(new { access_token = "unit-test-token", expires_in = 3600 }));

            Interlocked.Increment(ref _managementApiRequests);
            if (path == "/api/v2/roles")
            {
                object[] roles = includeRoleFallback
                    ? [new { id = "approver-role", name = "Approver" }]
                    : [];
                return Task.FromResult(Json(roles));
            }

            if (path == "/api/v2/roles/approver-role/permissions")
            {
                object[] permissions =
                [
                    new
                    {
                        permission_name = Permissions.RefTests.Approve,
                        resource_server_identifier = Auth0ManagementTestServices.ApiAudience
                    }
                ];
                return Task.FromResult(Json(permissions));
            }

            if (path == "/api/v2/roles/approver-role/users")
                return Task.FromResult(Json(new[] { new { user_id = "outside-directory-user" } }));

            if (path == "/api/v2/users")
            {
                var query = request.RequestUri.Query;
                var page = int.Parse(
                    query.Split("page=")[1].Split('&')[0],
                    System.Globalization.CultureInfo.InvariantCulture);
                var firstUser = page * 100;
                var userCount = includeRoleFallback ? 123 : 450;
                var users = Enumerable.Range(firstUser, Math.Min(100, userCount - firstUser))
                    .Select(index => (object)new
                    {
                        user_id = $"directory-user-{index}",
                        name = $"Directory User {index}",
                        email = $"directory-user-{index}@example.org"
                    })
                    .ToArray();
                return Task.FromResult(Json(users));
            }

            if (path.StartsWith("/api/v2/users/", StringComparison.Ordinal) &&
                path.EndsWith("/permissions", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _userPermissionRequests);
                return Task.FromResult(Json(Array.Empty<object>()));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage Json<T>(T value) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }

    private sealed class ScopeSynchronizationHandler(bool includeScopes = true) : HttpMessageHandler
    {
        public HttpMethod? PatchMethod { get; private set; }

        public string? PatchBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path == "/oauth/token")
                return Json(new { access_token = "unit-test-token", expires_in = 3600 });

            if (request.Method == HttpMethod.Get && path == "/api/v2/resource-servers")
            {
                object? scopes = includeScopes
                    ? new object[]
                    {
                        new { value = "custom:read", description = "Tenant-managed description" },
                        new
                        {
                            value = Permissions.RefTests.Approve,
                            description = "Custom approver description"
                        }
                    }
                    : null;
                return Json(new[]
                {
                    new { id = "api-resource", identifier = Auth0ManagementTestServices.ApiAudience, scopes }
                });
            }

            if (request.Method == HttpMethod.Patch && path == "/api/v2/resource-servers/api-resource")
            {
                PatchMethod = request.Method;
                PatchBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json<T>(T value) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}

internal static class Auth0ManagementTestServices
{
    internal const string ApiAudience = "https://api.ref-test.example";

    private const string OtherAudience = "https://other-api.ref-test.example";

    internal static ServiceProvider CreateProvider(
        HttpMessageHandler? messageHandler = null,
        string domain = "tenant.example.test")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth0:Domain"] = domain,
                ["Auth0:Audience"] = ApiAudience,
                ["Auth0:ManagementClientId"] = "unit-test-client",
                ["Auth0:ManagementClientSecret"] = "unit-test-secret"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddAuth0ManagementServices(configuration);
        if (messageHandler is null)
        {
            services.AddSingleton<IHttpClientFactory, FakeHttpClientFactory>();
        }
        else
        {
            services.Configure<HttpClientFactoryOptions>(
                nameof(IAuth0ManagementService),
                options => options.HttpMessageHandlerBuilderActions.Add(
                    builder => builder.PrimaryHandler = messageHandler));
        }

        return services.BuildServiceProvider();
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new FakeAuth0ApiHandler());
    }

    private sealed class FakeAuth0ApiHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Post && path == "/oauth/token")
                return Task.FromResult(Json(new { access_token = "unit-test-token", expires_in = 3600 }));

            if (request.Method != HttpMethod.Get)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.MethodNotAllowed));

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 4 && segments[0] == "api" && segments[1] == "v2" &&
                segments[2] == "users")
                return Task.FromResult(Json(UserAccountStatus(segments[3])));

            if (path == "/api/v2/roles")
                return Task.FromResult(Json(Roles()));

            if (path == "/api/v2/users")
                return Task.FromResult(Json(Users()));

            if (segments.Length == 5 && segments[0] == "api" && segments[1] == "v2")
            {
                if (segments[2] == "roles")
                {
                    var response = segments[4] switch
                    {
                        "permissions" => RolePermissions(segments[3]),
                        "users" => RoleUsers(segments[3]),
                        _ => null
                    };

                    return Task.FromResult(response is null
                        ? new HttpResponseMessage(HttpStatusCode.NotFound)
                        : Json(response));
                }

                if (segments[2] == "users" && segments[4] == "permissions")
                    return Task.FromResult(Json(UserPermissions(segments[3])));

                if (segments[2] == "users" && segments[4] == "roles")
                    return Task.FromResult(Json(UserRoles(segments[3])));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage Json(object body) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

        private static object[] Roles() =>
        [
            new { id = "role-target-exact", name = "Target exact" },
            new { id = "role-target-wildcard", name = "Target wildcard" },
            new { id = "role-target-superadmin", name = "Target superadmin" },
            new { id = "role-other-exact", name = "Other exact" },
            new { id = "role-other-wildcard", name = "Other wildcard" },
            new { id = "role-other-superadmin", name = "Other superadmin" }
        ];

        private static object[] Users() =>
        [
            User("role-target-exact"),
            User("role-target-wildcard"),
            User("role-target-superadmin"),
            User("role-other-exact"),
            User("role-other-wildcard"),
            User("role-other-superadmin"),
            User("direct-target-exact"),
            User("direct-target-wildcard"),
            User("direct-target-superadmin"),
            User("direct-other-exact"),
            User("direct-other-wildcard"),
            User("direct-other-superadmin")
        ];

        private static object User(string userId) => new
        {
            user_id = userId,
            name = userId,
            email = $"{userId}@example.org"
        };

        private static object UserAccountStatus(string userId) =>
            userId switch
            {
                "blocked-user" => new { blocked = true },
                "unknown-user" => new Dictionary<string, object?>(),
                _ => new { blocked = false }
            };

        private static object[] RolePermissions(string roleId) =>
            roleId switch
            {
                "role-target-exact" => Grant(Permissions.RefTests.Approve, ApiAudience),
                "role-target-wildcard" => Grant("ref-tests:*", ApiAudience),
                "role-target-superadmin" => Grant(Permissions.Superadmin, ApiAudience),
                "role-other-exact" => Grant(Permissions.RefTests.Approve, OtherAudience),
                "role-other-wildcard" => Grant("ref-tests:*", OtherAudience),
                "role-other-superadmin" => Grant(Permissions.Superadmin, OtherAudience),
                "fresh-role" => Grant(Permissions.RefTests.ViewList, ApiAudience)
                    .Concat(Grant(Permissions.RefTests.Approve, OtherAudience))
                    .ToArray(),
                _ => []
            };

        private static object[] RoleUsers(string roleId) =>
            roleId switch
            {
                "role-target-exact" or "role-target-wildcard" or "role-target-superadmin" or
                    "role-other-exact" or "role-other-wildcard" or "role-other-superadmin" =>
                    [new { user_id = roleId }],
                _ => []
            };

        private static object[] UserPermissions(string userId) =>
            userId switch
            {
                "fresh-user" => Grant(Permissions.Questions.Search, ApiAudience)
                    .Concat(Grant(Permissions.Questions.View, OtherAudience))
                    .ToArray(),
                "direct-target-exact" => Grant(Permissions.RefTests.Approve, ApiAudience),
                "direct-target-wildcard" => Grant("ref-tests:*", ApiAudience),
                "direct-target-superadmin" => Grant(Permissions.Superadmin, ApiAudience),
                "direct-other-exact" => Grant(Permissions.RefTests.Approve, OtherAudience),
                "direct-other-wildcard" => Grant("ref-tests:*", OtherAudience),
                "direct-other-superadmin" => Grant(Permissions.Superadmin, OtherAudience),
                _ => []
            };

        private static object[] UserRoles(string userId) =>
            userId == "fresh-user" ? [new { id = "fresh-role", name = "Fresh role" }] : [];

        private static object[] Grant(string permission, string audience) =>
            [new { permission_name = permission, resource_server_identifier = audience }];
    }
}
