using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

public class LogoService(ILogger<LogoService> logger, EmailConfiguration configuration, HttpClient httpClient) : ILogoService
{
    private byte[]? _cachedLogoBytes;

    public async Task<byte[]?> GetLogoBytesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedLogoBytes != null)
            return _cachedLogoBytes;

        var logoUrl = $"{configuration.BaseUrl}/RefTest-logo.png";
        try
        {
            _cachedLogoBytes = await httpClient.GetByteArrayAsync(logoUrl, cancellationToken);
            return _cachedLogoBytes;
        }
        // A missing logo degrades the document; a cancelled caller must still stop.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            ServiceLoggerMessages.LogLogoDownloadFailed(logger, ex, logoUrl);
            return null;
        }
    }

    public async Task<string> GetLogoAsBase64Async(CancellationToken cancellationToken = default)
    {
        var logoBytes = await GetLogoBytesAsync(cancellationToken);
        return logoBytes != null ? Convert.ToBase64String(logoBytes) : string.Empty;
    }
}
