using Handball.Belgium.RefTestManagement.Application.Configurations;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

public class LogoService(ILogger<LogoService> logger, EmailConfiguration configuration, HttpClient httpClient) : ILogoService
{
    private byte[]? _cachedLogoBytes;

    public async Task<byte[]?> GetLogoBytesAsync()
    {
        if (_cachedLogoBytes != null)
            return _cachedLogoBytes;

        var logoUrl = $"{configuration.BaseUrl}/RefTest-logo.png";
        try
        {
            _cachedLogoBytes = await httpClient.GetByteArrayAsync(logoUrl);
            return _cachedLogoBytes;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to download logo from {LogoUrl}", logoUrl);
            return null;
        }
    }

    public async Task<string> GetLogoAsBase64Async()
    {
        var logoBytes = await GetLogoBytesAsync();
        return logoBytes != null ? Convert.ToBase64String(logoBytes) : string.Empty;
    }
}
