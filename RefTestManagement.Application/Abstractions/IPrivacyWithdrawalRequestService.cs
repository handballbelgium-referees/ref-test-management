namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

public interface IPrivacyWithdrawalRequestService
{
    Task RequestAsync(string? email, CancellationToken cancellationToken);
    Task<bool> RequestForParticipantAsync(string token, CancellationToken cancellationToken);
    Task<bool> ConfirmAsync(string? challengeKey, CancellationToken cancellationToken);
    Task<int> ReconcileIncompleteBatchesAsync(CancellationToken cancellationToken);
}
