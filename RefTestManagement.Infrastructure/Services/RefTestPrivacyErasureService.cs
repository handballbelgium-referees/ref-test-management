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
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var refTestId = refTest.Id.ToString();
        var jobs = await context.Jobs
            .Where(job => job.Payload.Contains(refTestId))
            .ToListAsync(cancellationToken);

        context.Jobs.RemoveRange(jobs);
        context.AuditEvents.RemoveRange(context.AuditEvents.Where(auditEvent => auditEvent.StreamId == refTestId));

        refTest.MarkDeleted();
        context.RefTests.Remove(refTest);
        await context.SaveChangesAsync(cancellationToken);

        // The audit interceptor records RefTestDeleted during SaveChanges; privacy erasure removes that event too.
        await context.AuditEvents
            .Where(auditEvent => auditEvent.StreamId == refTestId)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}