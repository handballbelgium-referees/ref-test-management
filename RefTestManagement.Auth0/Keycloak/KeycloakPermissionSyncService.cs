using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Handball.Belgium.RefTestManagement.Auth0.Keycloak;

/// <summary>
/// Synchronises application permissions to Keycloak as client roles on the API client.
/// The sync is additive: missing roles are created, existing roles and user assignments are untouched.
/// </summary>
internal sealed partial class KeycloakPermissionSyncService(
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

        var authority = _config.Authority.TrimEnd('/');
        var accessToken = await GetAdminAccessTokenAsync(authority, cancellationToken).ConfigureAwait(false);
        var adminBase = authority.Replace("/realms/", "/admin/realms/", StringComparison.Ordinal);

        var clientInternalId = await GetClientInternalIdAsync(adminBase, accessToken, cancellationToken)
            .ConfigureAwait(false);

        foreach (var permission in permissions.Distinct(StringComparer.Ordinal))
        {
            var created = await EnsureRoleAsync(adminBase, clientInternalId, permission, accessToken, cancellationToken)
                .ConfigureAwait(false);

            if (created)
            {
                LogRoleCreated(logger, permission, _config.Audience);
            }
        }
    }

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
