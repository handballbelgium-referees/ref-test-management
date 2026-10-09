namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

public interface ILogoService
{
    Task<byte[]?> GetLogoBytesAsync();
    Task<string> GetLogoAsBase64Async();
}
