namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

public interface IPersonalDataExportRequestCleanup
{
    Task<int> ClearExpiredChallengesAsync(DateTime now, CancellationToken cancellationToken);
}
