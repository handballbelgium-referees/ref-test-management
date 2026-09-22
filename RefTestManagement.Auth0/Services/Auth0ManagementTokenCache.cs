namespace Handball.Belgium.RefTestManagement.Auth0.Services;

internal sealed class Auth0ManagementTokenCache
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    internal readonly record struct CachedToken(string AccessToken, DateTimeOffset ExpiresAt);

    public async Task<string> GetAccessTokenAsync(
        Func<CancellationToken, Task<CachedToken>> tokenFactory,
        CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _accessToken;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
                return _accessToken;

            var token = await tokenFactory(cancellationToken);
            _accessToken = token.AccessToken;
            _tokenExpiry = token.ExpiresAt;
            return _accessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
