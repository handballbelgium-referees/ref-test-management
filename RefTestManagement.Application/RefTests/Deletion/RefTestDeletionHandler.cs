using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests.Deletion;

public sealed class RefTestDeletionHandler(
    IRefTestRepository refTestRepository,
    IRefTestPrivacyErasureService privacyErasureService,
    IRefTestSubscriptionService subscriptionService)
{
    public async Task<DeleteRefTestsResult> HandleAsync(
        DeleteRefTestsCommand command,
        CancellationToken cancellationToken = default)
    {
        var refTests = await refTestRepository.GetByIdsAsync(command.Ids, cancellationToken);
        var result = new DeleteRefTestsResult { TotalRequested = command.Ids.Count };

        foreach (var id in command.Ids)
        {
            var refTest = refTests.FirstOrDefault(candidate => candidate.Id == id);
            try
            {
                if (refTest is null)
                    throw new RefTestNotFoundException(id.ToString());

                var deletedSnapshot = DeletedRefTestSnapshot.Capture(refTest);
                await privacyErasureService.EraseAndDeleteAsync(
                    refTest, ErasureInitiator.Operator, cancellationToken);
                result.SuccessfullyDeleted++;
                result.DeletedRefTests.Add(deletedSnapshot);
            }
            catch (Exception exception)
            {
                result.Failed++;
                result.Errors.Add(new DeleteRefTestError(id, exception.Message));
            }
        }

        foreach (var deletedRefTest in result.DeletedRefTests)
            await subscriptionService.PublishRefTestDeletedAsync(
                deletedRefTest.RefTest.Id, deletedRefTest.RefTest.Status, cancellationToken);

        return result;
    }
}
