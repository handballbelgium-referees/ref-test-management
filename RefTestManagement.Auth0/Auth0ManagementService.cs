using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Handball.Belgium.RefTestManagement.Auth0;

/// <summary>
/// Calls the Auth0 Management API using a cached M2M client credentials token.
/// </summary>
internal sealed class Auth0ManagementService(
    HttpClient httpClient,
    IOptions<Auth0ManagementConfiguration> options,
    ILogger<Auth0ManagementService> logger)
    : IAuth0ManagementService
{
    private readonly Auth0ManagementConfiguration _config = options.Value;

    // --- Token cache --------------------------------------------------------

    private string? _accessToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _accessToken;

        var response = await httpClient.PostAsync(
            $"https://{_config.Domain}/oauth/token",
            JsonContent.Create(new
            {
                client_id = _config.ManagementClientId,
                client_secret = _config.ManagementClientSecret,
                audience = $"https://{_config.Domain}/api/v2/",
                grant_type = "client_credentials"
            }),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Auth0 returned an empty token response");

        _accessToken = token.AccessToken;
        // Refresh 60 seconds before actual expiry to avoid edge-case races
        _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn - 60);
        return _accessToken;
    }

    // --- IAuth0ManagementService --------------------------------------------

    /// <inheritdoc />
    public async Task<IReadOnlyList<Auth0User>> GetUsersWithPermissionAsync(
        string permission,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        // Collect user IDs from roles that include the permission
        var userIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var roles = await GetAllPagesAsync<RoleResponse>(
            $"https://{_config.Domain}/api/v2/roles?per_page=100",
            token,
            cancellationToken);

        foreach (var role in roles)
        {
            var rolePermissions = await GetAllPagesAsync<PermissionResponse>(
                $"https://{_config.Domain}/api/v2/roles/{role.Id}/permissions?per_page=100",
                token,
                cancellationToken);

            if (!rolePermissions.Any(p => string.Equals(p.PermissionName, permission, StringComparison.OrdinalIgnoreCase)))
                continue;

            var roleUsers = await GetAllPagesAsync<UserIdResponse>(
                $"https://{_config.Domain}/api/v2/roles/{role.Id}/users?per_page=100",
                token,
                cancellationToken);

            foreach (var u in roleUsers)
                userIds.Add(u.UserId);
        }

        // Also check direct user-permission grants
        // Auth0 doesn't offer a "list users with permission" endpoint directly,
        // so we rely on role-based assignments only (which is the recommended Auth0 RBAC approach).

        if (userIds.Count == 0)
        {
            logger.LogInformation("No users found with permission '{Permission}'", permission);
            return [];
        }

        var users = new List<Auth0User>(userIds.Count);
        foreach (var userId in userIds)
        {
            var user = await GetUserAsync(userId, token, cancellationToken);
            if (user is not null)
                users.Add(user);
        }

        logger.LogInformation(
            "Found {Count} user(s) with permission '{Permission}'",
            users.Count, permission);

        return users;
    }

    /// <inheritdoc />
    public async Task SyncPermissionsAsync(
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        // Find the API resource server by audience
        var apis = await GetAllPagesAsync<ResourceServerResponse>(
            $"https://{_config.Domain}/api/v2/resource-servers?per_page=100",
            token,
            cancellationToken);

        var api = apis.FirstOrDefault(a =>
            string.Equals(a.Identifier, _config.Audience, StringComparison.OrdinalIgnoreCase));

        if (api is null)
        {
            logger.LogWarning(
                "Auth0 resource server with audience '{Audience}' not found — skipping permission sync",
                _config.Audience);
            return;
        }

        var existing = (api.Scopes ?? [])
            .Select(s => s.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = permissions
            .Where(p => !existing.Contains(p))
            .Select(p => new { value = p, description = p })
            .ToList();

        if (toAdd.Count == 0)
        {
            logger.LogInformation(
                "Permission sync complete: all {Count} permission(s) already registered",
                permissions.Count);
            return;
        }

        // PATCH is additive: send only the new scopes
        var existingScopes = existing.Select(s => new { value = s, description = s }).ToList();
        var allScopes = existingScopes.Concat(toAdd).ToList();
        var patchBody = new { scopes = allScopes };
        var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"https://{_config.Domain}/api/v2/resource-servers/{api.Id}");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(patchBody);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        logger.LogInformation(
            "Permission sync complete: added {Added} new permission(s), {Existing} already existed",
            toAdd.Count, existing.Count);
    }

    // --- Helpers ------------------------------------------------------------

    private async Task<List<T>> GetAllPagesAsync<T>(
        string url,
        string token,
        CancellationToken cancellationToken)
    {
        var results = new List<T>();
        int page = 0;
        const int perPage = 100;

        while (true)
        {
            var separator = url.Contains('?') ? '&' : '?';
            var pagedUrl = $"{url}{separator}page={page}&per_page={perPage}";

            var request = new HttpRequestMessage(HttpMethod.Get, pagedUrl);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var items = await response.Content.ReadFromJsonAsync<List<T>>(
                cancellationToken: cancellationToken) ?? [];

            results.AddRange(items);

            if (items.Count < perPage)
                break;

            page++;
        }

        return results;
    }

    private async Task<Auth0User?> GetUserAsync(
        string userId,
        string token,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://{_config.Domain}/api/v2/users/{Uri.EscapeDataString(userId)}");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Could not fetch Auth0 user '{UserId}': {Status}", userId, response.StatusCode);
            return null;
        }

        var user = await response.Content.ReadFromJsonAsync<UserResponse>(
            cancellationToken: cancellationToken);

        return user is null ? null : new Auth0User(user.Name ?? user.Email, user.Email);
    }

    // --- Private response models --------------------------------------------

    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private record RoleResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name);

    private record PermissionResponse(
        [property: JsonPropertyName("permission_name")] string PermissionName,
        [property: JsonPropertyName("resource_server_identifier")] string ResourceServerIdentifier);

    private record UserIdResponse(
        [property: JsonPropertyName("user_id")] string UserId);

    private record UserResponse(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("email")] string Email);

    private record ResourceServerResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("identifier")] string Identifier,
        [property: JsonPropertyName("scopes")] List<ScopeItem>? Scopes);

    private record ScopeItem(
        [property: JsonPropertyName("value")] string Value);
}
