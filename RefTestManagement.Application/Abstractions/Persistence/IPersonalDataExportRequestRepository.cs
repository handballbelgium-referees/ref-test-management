namespace Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;

public interface IPersonalDataExportRequestRepository
{
    Task ClearForEmailAsync(string email, CancellationToken cancellationToken = default);
}
