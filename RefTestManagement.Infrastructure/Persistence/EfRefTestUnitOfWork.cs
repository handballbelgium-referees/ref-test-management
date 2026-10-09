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

    public async Task<RefTest?> DiscardChangesAsync(RefTest refTest, CancellationToken cancellationToken)
    {
        var refTestId = refTest.Id;
        var changedEntries = context.ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        try
        {
            foreach (var entry in changedEntries)
            {
                if (entry.State == EntityState.Added)
                    entry.State = EntityState.Detached;
                else
                    await entry.ReloadAsync(cancellationToken);
            }
        }
        finally
        {
            // Domain events are transient and are not restored by EF's ReloadAsync.
            refTest.ClearDomainEvents();
        }

        // Reload does not restore RefTest's transient IssuedToken. Detach it and query a clean
        // aggregate so a failed token rotation cannot leak into a later item in this batch.
        context.Entry(refTest).State = EntityState.Detached;
        return await context.RefTests.FirstOrDefaultAsync(candidate => candidate.Id == refTestId, cancellationToken);
    }

    public Task CancelPendingJobsAsync(Guid refTestId, CancellationToken cancellationToken) =>
        jobEnqueueService.CancelPendingJobsForRefTestAsync(
            refTestId, saveChanges: false, unitOfWorkContext: context, cancellationToken: cancellationToken);

    public Task CancelPendingResultEmailsAsync(Guid refTestId, CancellationToken cancellationToken) =>
        jobEnqueueService.CancelPendingResultEmailsAsync(
            refTestId, saveChanges: false, unitOfWorkContext: context, cancellationToken: cancellationToken);

    public Task StageApprovalDecisionEmailAsync(ApprovalDecisionEmailPayload payload, CancellationToken cancellationToken) =>
        jobEnqueueService.EnqueueApprovalDecisionEmailAsync(
            payload,
            saveChanges: false,
            unitOfWorkContext: context,
            cancellationToken: cancellationToken);

    public Task StageResultEmailAsync(ResultEmailPayload payload, DateTime? executeAfter, CancellationToken cancellationToken) =>
        jobEnqueueService.EnqueueResultEmailAsync(
            payload,
            executeAfter,
            saveChanges: false,
            unitOfWorkContext: context,
            cancellationToken: cancellationToken);

    public async Task ClearPersonalDataExportRequestsAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var requests = await context.PersonalDataExportRequests
            .Where(request => request.Email.Trim().ToUpper() == normalizedEmail)
            .ToListAsync(cancellationToken);
        foreach (var request in requests)
            request.ClearForPrivacyErasure();
    }

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
