namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

public interface IPersonalDataExportRequestService
{
    Task RequestAsync(string? email, CancellationToken cancellationToken);
    Task<bool> ConfirmAsync(string? challengeKey, CancellationToken cancellationToken);
}
