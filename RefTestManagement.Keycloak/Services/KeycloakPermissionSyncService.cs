using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Handball.Belgium.RefTestManagement.Keycloak;

/// <summary>
/// Synchronises application permissions to Keycloak as client roles on the API client.
/// The sync is additive: missing roles are created, existing roles and user assignments are untouched.
/// </summary>
public sealed partial class KeycloakPermissionSyncService(
    HttpClient httpClient,
    IOptions<KeycloakAdminConfiguration> options,
    ILogger<KeycloakPermissionSyncService> logger) : IIdentityPermissionSync
{
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
