namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

public interface ILogoService
{
    Task<byte[]?> GetLogoBytesAsync(CancellationToken cancellationToken = default);
    Task<string> GetLogoAsBase64Async(CancellationToken cancellationToken = default);
}
