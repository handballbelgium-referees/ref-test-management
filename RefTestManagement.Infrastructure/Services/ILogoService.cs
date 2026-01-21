namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

public interface ILogoService
{
    Task<byte[]?> GetLogoBytesAsync();
    Task<string> GetLogoAsBase64Async();
}