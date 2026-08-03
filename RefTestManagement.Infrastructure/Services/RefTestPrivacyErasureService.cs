using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

public interface IRefTestPrivacyErasureService
{
    Task EraseAsync(RefTest refTest, CancellationToken cancellationToken = default);
}

public sealed class RefTestPrivacyErasureService(RefTestManagementContext context) : IRefTestPrivacyErasureService
{
    public async Task EraseAsync(RefTest refTest, CancellationToken cancellationToken = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        // The retrying execution strategy must own the transaction as a single retriable unit.
        await strategy.ExecuteAsync(cancellationToken, async ct =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            var refTestId = refTest.Id.ToString();
            var jobs = await context.Jobs
                .Where(job => job.Payload.Contains(refTestId))
                .ToListAsync(ct);

            context.Jobs.RemoveRange(jobs);
            context.AuditEvents.RemoveRange(context.AuditEvents.Where(auditEvent => auditEvent.StreamId == refTestId));

            refTest.MarkDeleted();
            context.RefTests.Remove(refTest);
            await context.SaveChangesAsync(ct);

            // The audit interceptor records RefTestDeleted during SaveChanges; privacy erasure removes that event too.
            await context.AuditEvents
                .Where(auditEvent => auditEvent.StreamId == refTestId)
                .ExecuteDeleteAsync(ct);

            await transaction.CommitAsync(ct);
        });
    }
}