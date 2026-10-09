using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core unit of work over one <see cref="RefTestManagementContext"/>. Jobs are staged through
/// <see cref="IJobEnqueueService"/> on the same context with <c>saveChanges: false</c>, so they
/// commit in the same transaction as the RefTests.
/// </summary>
public sealed class EfRefTestUnitOfWork(RefTestManagementContext context, IJobEnqueueService jobEnqueueService)
    : IRefTestUnitOfWork
{
    public async Task<IReadOnlyList<RefTest>> GetRefTestsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await context.RefTests.Where(refTest => ids.Contains(refTest.Id)).ToListAsync(cancellationToken);

    public void AddRefTests(IEnumerable<RefTest> refTests) => context.RefTests.AddRange(refTests);

    public async Task<RefTest?> RevertRefTestAsync(RefTest refTest, CancellationToken cancellationToken)
    {
        // Detaching drops the in-memory changes; the domain events they raised must go too, or the
        // audit interceptor would record a transition that was never saved.
        refTest.ClearDomainEvents();
        context.Entry(refTest).State = EntityState.Detached;
        return await context.RefTests.FirstOrDefaultAsync(candidate => candidate.Id == refTest.Id, cancellationToken);
    }

    public Task StageApprovalDecisionEmailAsync(ApprovalDecisionEmailPayload payload, CancellationToken cancellationToken) =>
        jobEnqueueService.EnqueueApprovalDecisionEmailAsync(
            payload,
            saveChanges: false,
            unitOfWorkContext: context,
            cancellationToken: cancellationToken);

    public Task StageInvitationEmailAsync(RefTest refTest, DateTime? executeAfter, CancellationToken cancellationToken) =>
        jobEnqueueService.EnqueueInvitationEmailAsync(
            refTest,
            executeAfter: executeAfter,
            saveChanges: false,
            unitOfWorkContext: context,
            cancellationToken: cancellationToken);

    public Task StageApprovalNotificationAsync(ApprovalNotificationEmailPayload payload, CancellationToken cancellationToken) =>
        jobEnqueueService.EnqueueApprovalNotificationAsync(
            payload,
            saveChanges: false,
            unitOfWorkContext: context,
            cancellationToken: cancellationToken);

    public Action BeginJobStaging()
    {
        var trackedJobIds = context.Jobs.Local.Select(job => job.Id).ToHashSet();
        return () =>
        {
            foreach (var job in context.Jobs.Local.Where(job => !trackedJobIds.Contains(job.Id)).ToList())
                context.Entry(job).State = EntityState.Detached;
        };
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesWithRetryAsync(cancellationToken);
}
