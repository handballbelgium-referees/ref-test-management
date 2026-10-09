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
    public void AddRefTests(IEnumerable<RefTest> refTests) => context.RefTests.AddRange(refTests);

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
