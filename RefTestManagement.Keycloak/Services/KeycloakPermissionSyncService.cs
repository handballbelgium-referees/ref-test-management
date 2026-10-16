using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Handball.Belgium.RefTestManagement.Keycloak;

/// <summary>
/// Synchronises application permissions to Keycloak as client roles on the API client,
/// and looks up the users who hold an application permission for approval notifications.
/// The sync is additive: missing roles are created, existing roles and user assignments are untouched.
/// </summary>
public sealed partial class KeycloakPermissionSyncService(
    HttpClient httpClient,
    IOptions<KeycloakAdminConfiguration> options,
    ILogger<KeycloakPermissionSyncService> logger,
    IMemoryCache approverCache) : IIdentityPermissionSync, IIdentityUserDirectory
{
    private const int MaximumApproverDiscoveryRequests = 128;
    private const int PageSize = 100;
    // ponytail: per-permission gates keep unrelated permissions from queueing behind a slow Keycloak call; one gate per permission key is bounded by the permission constants.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ApproverDiscoveryGates = new(StringComparer.Ordinal);
    private static readonly TimeSpan ApproverCacheLifetime = TimeSpan.FromMinutes(5);

    private readonly KeycloakAdminConfiguration _config = options.Value;

    /// <inheritdoc />
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_config.Authority)
        && !string.IsNullOrWhiteSpace(_config.Audience)
        && !string.IsNullOrWhiteSpace(_config.AdminClientId)
        && !string.IsNullOrWhiteSpace(_config.AdminClientSecret);

    /// <inheritdoc />
    public async Task SyncPermissionsAsync(
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        if (!IsConfigured)
        {
            throw new InvalidOperationException("Keycloak permission sync is not configured.");
        }

        var context = await GetAdminContextAsync(cancellationToken).ConfigureAwait(false);

        foreach (var permission in permissions.Distinct(StringComparer.Ordinal))
        {
            var created = await EnsureRoleAsync(
                context.AdminBase, context.ClientInternalId, permission, context.AccessToken, cancellationToken)
                .ConfigureAwait(false);

            if (created)
            {
                LogRoleCreated(logger, permission, _config.Audience);
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Fails closed: a missing or disabled user throws, so the caller rejects the session instead
    /// of caching an empty grant. HTTP failures also throw so an outage is never treated as "no permissions".
    /// </remarks>
    public async Task<IReadOnlySet<string>> GetUserPermissionsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (!IsConfigured)
        {
            throw new InvalidOperationException("Keycloak permission sync is not configured.");
        }

        var context = await GetAdminContextAsync(cancellationToken).ConfigureAwait(false);
        var userUrl = $"{context.AdminBase}/users/{Uri.EscapeDataString(userId)}";

        using (var userResponse = await SendAdminRequestAsync(HttpMethod.Get, userUrl, context.AccessToken, null, cancellationToken)
            .ConfigureAwait(false))
        {
            if (userResponse.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException("Keycloak account could not be found; authorization will fail closed.");
            }

            userResponse.EnsureSuccessStatusCode();

            using var userDocument = await JsonDocument.ParseAsync(
                await userResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var enabled = userDocument.RootElement.TryGetProperty("enabled", out var enabledElement)
                && enabledElement.ValueKind == JsonValueKind.True;

            if (!enabled)
            {
                throw new InvalidOperationException("Keycloak account is disabled; authorization will fail closed.");
            }
        }

        var clientId = Uri.EscapeDataString(context.ClientInternalId);
        var roles = new Dictionary<string, RoleReference>(StringComparer.Ordinal);

        await AddRoleMappingsAsync(
            context,
            $"{context.AdminBase}/users/{Uri.EscapeDataString(userId)}/role-mappings/clients/{clientId}",
            roles,
            cancellationToken).ConfigureAwait(false);

        foreach (var groupId in await GetUserGroupIdsAsync(context, userId, cancellationToken).ConfigureAwait(false))
        {
            await AddRoleMappingsAsync(
                context,
                $"{context.AdminBase}/groups/{Uri.EscapeDataString(groupId)}/role-mappings/clients/{clientId}",
                roles,
                cancellationToken).ConfigureAwait(false);
        }

        await ExpandCompositesAsync(context, roles, cancellationToken).ConfigureAwait(false);

        return roles.Values.Select(role => role.Name).ToHashSet(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Matches the exact permission, the superadmin role, and the namespace wildcard (for example <c>ref-tests:*</c>),
    /// the same rules as the Auth0 directory. Users reach a role directly, through a group, or through a
    /// composite role that contains it; members of subgroups inherit their ancestors' roles.
    /// Disabled users and users without an email address are omitted. Throws when the request budget is
    /// exhausted or Keycloak fails, so a partial recipient list is never returned or cached.
    /// </remarks>
    public async Task<IReadOnlyList<IdentityUser>> GetUsersWithPermissionAsync(
        string permission,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        if (!IsConfigured)
        {
            throw new InvalidOperationException("Keycloak permission sync is not configured.");
        }

        var cacheKey = $"KeycloakApproverDiscovery:{_config.Authority}:{_config.Audience}:{permission}";

        if (approverCache.TryGetValue(cacheKey, out IReadOnlyList<IdentityUser>? cached) && cached is not null)
        {
            return cached;
        }

        var gate = ApproverDiscoveryGates.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (approverCache.TryGetValue(cacheKey, out cached) && cached is not null)
            {
                return cached;
            }

            var users = await DiscoverUsersWithPermissionAsync(permission, cancellationToken).ConfigureAwait(false);
            approverCache.Set(cacheKey, users, ApproverCacheLifetime);
            return users;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyList<IdentityUser>> DiscoverUsersWithPermissionAsync(
        string permission,
        CancellationToken cancellationToken)
    {
        var context = await GetAdminContextAsync(cancellationToken).ConfigureAwait(false);
        var budget = new DiscoveryBudget(MaximumApproverDiscoveryRequests);
        var clientId = Uri.EscapeDataString(context.ClientInternalId);
        var clientRolesUrl = $"{context.AdminBase}/clients/{clientId}/roles";

        var allRoles = await ReadAllPagesAsync<RoleReference>(
            clientRolesUrl, ParseRole, context, budget, cancellationToken).ConfigureAwait(false);

        var wildcard = permission.Contains(':', StringComparison.Ordinal)
            ? $"{permission[..permission.IndexOf(':', StringComparison.Ordinal)]}:*"
            : null;

        var seeds = allRoles.Where(role =>
            role.Name == permission
            || role.Name == Permissions.Superadmin
            || (wildcard is not null && role.Name == wildcard)).ToList();

        // Build the reverse composite graph (child -> parents) so every role that contains a matching role is granted too.
        var parentsByChild = new Dictionary<string, List<RoleReference>>(StringComparer.Ordinal);

        foreach (var parent in allRoles.Where(role => role.Composite))
        {
            var url = $"{context.AdminBase}/roles-by-id/{Uri.EscapeDataString(parent.Id)}/composites/clients/{clientId}";

            using var document = await GetDiscoveryJsonAsync(url, context, budget, cancellationToken).ConfigureAwait(false);

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (ParseRole(element) is { } child)
                {
                    if (!parentsByChild.TryGetValue(child.Id, out var parents))
                    {
                        parents = [];
                        parentsByChild[child.Id] = parents;
                    }

                    parents.Add(parent);
                }
            }
        }

        var grantedRoles = new Dictionary<string, RoleReference>(StringComparer.Ordinal);
        var pending = new Queue<RoleReference>(seeds);

        while (pending.TryDequeue(out var role))
        {
            if (!grantedRoles.TryAdd(role.Id, role))
            {
                continue;
            }

            if (parentsByChild.TryGetValue(role.Id, out var parents))
            {
                foreach (var parent in parents)
                {
                    pending.Enqueue(parent);
                }
            }
        }

        var usersById = new Dictionary<string, IdentityUser>(StringComparer.Ordinal);
        var groupIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var role in grantedRoles.Values)
        {
            var roleUrl = $"{clientRolesUrl}/{Uri.EscapeDataString(role.Name)}";

            foreach (var user in await ReadAllPagesAsync<DirectoryUser>(
                $"{roleUrl}/users", ParseUser, context, budget, cancellationToken).ConfigureAwait(false))
            {
                usersById.TryAdd(user.Id, user.User);
            }

            foreach (var groupId in await ReadAllPagesAsync<string>(
                $"{roleUrl}/groups", ParseGroupId, context, budget, cancellationToken).ConfigureAwait(false))
            {
                groupIds.Add(groupId);
            }
        }

        var groupQueue = new Queue<string>(groupIds);
        var visitedGroups = new HashSet<string>(groupIds, StringComparer.Ordinal);

        while (groupQueue.TryDequeue(out var groupId))
        {
            var groupBase = $"{context.AdminBase}/groups/{Uri.EscapeDataString(groupId)}";

            foreach (var user in await ReadAllPagesAsync<DirectoryUser>(
                $"{groupBase}/members", ParseUser, context, budget, cancellationToken).ConfigureAwait(false))
            {
                usersById.TryAdd(user.Id, user.User);
            }

            foreach (var childGroupId in await ReadAllPagesAsync<string>(
                $"{groupBase}/children", ParseGroupId, context, budget, cancellationToken).ConfigureAwait(false))
            {
                if (visitedGroups.Add(childGroupId))
                {
                    groupQueue.Enqueue(childGroupId);
                }
            }
        }

        return usersById.Values.OrderBy(user => user.Email, StringComparer.Ordinal).ToList();
    }

    private async Task<List<T>> ReadAllPagesAsync<T>(
        string url,
        Func<JsonElement, T?> parse,
        AdminContext context,
        DiscoveryBudget budget,
        CancellationToken cancellationToken) where T : class
    {
        var items = new List<T>();

        for (var first = 0; ; first += PageSize)
        {
            using var document = await GetDiscoveryJsonAsync(
                $"{url}?first={first}&max={PageSize}", context, budget, cancellationToken).ConfigureAwait(false);

            var count = 0;

            foreach (var element in document.RootElement.EnumerateArray())
            {
                count++;

                if (parse(element) is { } item)
                {
                    items.Add(item);
                }
            }

            if (count < PageSize)
            {
                return items;
            }
        }
    }

    private async Task<JsonDocument> GetDiscoveryJsonAsync(
        string url,
        AdminContext context,
        DiscoveryBudget budget,
        CancellationToken cancellationToken)
    {
        budget.Take();
        return await GetJsonAsync(url, context, cancellationToken).ConfigureAwait(false);
    }

    private static string? ParseGroupId(JsonElement element) =>
        element.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } groupId ? groupId : null;

    private static DirectoryUser? ParseUser(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var id) || id.GetString() is not { Length: > 0 } userId)
        {
            return null;
        }

        var enabled = element.TryGetProperty("enabled", out var enabledElement)
            && enabledElement.ValueKind == JsonValueKind.True;

        if (!enabled
            || !element.TryGetProperty("email", out var emailElement)
            || emailElement.GetString() is not { Length: > 0 } email)
        {
            return null;
        }

        var firstName = element.TryGetProperty("firstName", out var first) ? first.GetString() : null;
        var lastName = element.TryGetProperty("lastName", out var last) ? last.GetString() : null;
        var username = element.TryGetProperty("username", out var user) ? user.GetString() : null;

        var fullName = $"{firstName} {lastName}".Trim();
        var displayName = fullName.Length > 0 ? fullName : username ?? email;

        return new DirectoryUser(userId, new IdentityUser(displayName, email));
    }

    private sealed record DirectoryUser(string Id, IdentityUser User);

    /// <summary>Caps the admin API requests one discovery run may make, so a large realm fails closed instead of running unbounded.</summary>
    private sealed class DiscoveryBudget(int limit)
    {
        private int _remaining = limit;

        public void Take()
        {
            if (_remaining <= 0)
            {
                throw new KeycloakDiscoveryBudgetExceededException(
                    "Keycloak approver discovery exceeded its request budget; no partial recipient list was returned.");
            }

            _remaining--;
        }
    }

    /// <summary>
    /// Returns the user's direct group memberships plus their ancestor groups, because Keycloak
    /// grants roles of parent groups to members of subgroups.
    /// </summary>
    private async Task<IReadOnlyList<string>> GetUserGroupIdsAsync(
        AdminContext context,
        string userId,
        CancellationToken cancellationToken)
    {
        var groupIds = new HashSet<string>(StringComparer.Ordinal);

        using var groupsDocument = await GetJsonAsync(
            $"{context.AdminBase}/users/{Uri.EscapeDataString(userId)}/groups",
            context,
            cancellationToken).ConfigureAwait(false);

        foreach (var group in groupsDocument.RootElement.EnumerateArray())
        {
            if (group.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } groupId)
            {
                groupIds.Add(groupId);
            }

            if (group.TryGetProperty("path", out var path) && path.GetString() is { Length: > 0 } groupPath)
            {
                await AddAncestorGroupIdsAsync(context, groupPath, groupIds, cancellationToken).ConfigureAwait(false);
            }
        }

        return groupIds.ToList();
    }

    private async Task AddAncestorGroupIdsAsync(
        AdminContext context,
        string groupPath,
        HashSet<string> groupIds,
        CancellationToken cancellationToken)
    {
        var segments = groupPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        for (var depth = segments.Length - 1; depth > 0; depth--)
        {
            var ancestorPath = string.Join('/', segments.Take(depth).Select(Uri.EscapeDataString));

            using var ancestorDocument = await GetJsonAsync(
                $"{context.AdminBase}/groups/group-by-path/{ancestorPath}",
                context,
                cancellationToken).ConfigureAwait(false);

            if (ancestorDocument.RootElement.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } ancestorId)
            {
                groupIds.Add(ancestorId);
            }
        }
    }

    /// <summary>
    /// Adds the client roles of a role-mappings endpoint. Keycloak returns only direct mappings for each subject.
    /// </summary>
    private async Task AddRoleMappingsAsync(
        AdminContext context,
        string url,
        Dictionary<string, RoleReference> roles,
        CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(url, context, cancellationToken).ConfigureAwait(false);

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (ParseRole(element) is { } role)
            {
                roles.TryAdd(role.Id, role);
            }
        }
    }

    /// <summary>
    /// Expands composite client roles transitively. Each role is expanded once, so cycles terminate.
    /// Only composites on the API client are followed; realm roles that contain API roles are not expanded.
    /// </summary>
    private async Task ExpandCompositesAsync(
        AdminContext context,
        Dictionary<string, RoleReference> roles,
        CancellationToken cancellationToken)
    {
        var pending = new Queue<RoleReference>(roles.Values.Where(role => role.Composite));

        while (pending.TryDequeue(out var parent))
        {
            var url =
                $"{context.AdminBase}/roles-by-id/{Uri.EscapeDataString(parent.Id)}/composites/clients/{Uri.EscapeDataString(context.ClientInternalId)}";

            using var document = await GetJsonAsync(url, context, cancellationToken).ConfigureAwait(false);

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (ParseRole(element) is { } child && roles.TryAdd(child.Id, child) && child.Composite)
                {
                    pending.Enqueue(child);
                }
            }
        }
    }

    private static RoleReference? ParseRole(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var id) || id.GetString() is not { Length: > 0 } roleId)
        {
            return null;
        }

        if (!element.TryGetProperty("name", out var name) || name.GetString() is not { Length: > 0 } roleName)
        {
            return null;
        }

        var composite = element.TryGetProperty("composite", out var compositeElement)
            && compositeElement.ValueKind == JsonValueKind.True;

        return new RoleReference(roleId, roleName, composite);
    }

    private async Task<JsonDocument> GetJsonAsync(string url, AdminContext context, CancellationToken cancellationToken)
    {
        using var response = await SendAdminRequestAsync(HttpMethod.Get, url, context.AccessToken, null, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private sealed record RoleReference(string Id, string Name, bool Composite);

    private async Task<AdminContext> GetAdminContextAsync(CancellationToken cancellationToken)
    {
        var authority = _config.Authority.TrimEnd('/');
        var accessToken = await GetAdminAccessTokenAsync(authority, cancellationToken).ConfigureAwait(false);
        var adminBase = authority.Replace("/realms/", "/admin/realms/", StringComparison.Ordinal);
        var clientInternalId = await GetClientInternalIdAsync(adminBase, accessToken, cancellationToken)
            .ConfigureAwait(false);

        return new AdminContext(adminBase, accessToken, clientInternalId);
    }

    private sealed record AdminContext(string AdminBase, string AccessToken, string ClientInternalId);

    private async Task<string> GetAdminAccessTokenAsync(string authority, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{authority}/protocol/openid-connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _config.AdminClientId,
                ["client_secret"] = _config.AdminClientSecret,
            }),
        };

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Keycloak token response did not contain an access token.");
    }

    private async Task<string> GetClientInternalIdAsync(
        string adminBase,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var url = $"{adminBase}/clients?clientId={Uri.EscapeDataString(_config.Audience)}";
        using var response = await SendAdminRequestAsync(HttpMethod.Get, url, accessToken, null, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        foreach (var client in document.RootElement.EnumerateArray())
        {
            if (client.TryGetProperty("clientId", out var clientId)
                && string.Equals(clientId.GetString(), _config.Audience, StringComparison.Ordinal)
                && client.TryGetProperty("id", out var id)
                && id.GetString() is { Length: > 0 } internalId)
            {
                return internalId;
            }
        }

        throw new InvalidOperationException("Keycloak API client was not found for the configured audience.");
    }

    private async Task<bool> EnsureRoleAsync(
        string adminBase,
        string clientInternalId,
        string roleName,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var rolesUrl = $"{adminBase}/clients/{Uri.EscapeDataString(clientInternalId)}/roles";
        var roleUrl = $"{rolesUrl}/{Uri.EscapeDataString(roleName)}";

        using (var existing = await SendAdminRequestAsync(HttpMethod.Get, roleUrl, accessToken, null, cancellationToken)
            .ConfigureAwait(false))
        {
            if (existing.StatusCode == HttpStatusCode.OK)
            {
                return false;
            }

            if (existing.StatusCode != HttpStatusCode.NotFound)
            {
                existing.EnsureSuccessStatusCode();
            }
        }

        var body = JsonSerializer.Serialize(new { name = roleName });

        using var created = await SendAdminRequestAsync(HttpMethod.Post, rolesUrl, accessToken, body, cancellationToken)
            .ConfigureAwait(false);

        // 409 means another sync created the role concurrently; treat it as already present.
        if (created.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }

        created.EnsureSuccessStatusCode();
        return true;
    }

    private async Task<HttpResponseMessage> SendAdminRequestAsync(
        HttpMethod method,
        string url,
        string accessToken,
        string? jsonBody,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };

        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
        }

        return await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created missing Keycloak role {Permission} on client {ClientId}.")]
    private static partial void LogRoleCreated(ILogger logger, string permission, string clientId);
}
