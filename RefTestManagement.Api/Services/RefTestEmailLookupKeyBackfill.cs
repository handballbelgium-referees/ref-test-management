using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Services;

/// <summary>
/// Backfills missing RefTest email lookup keys in bounded batches before indexed matching.
/// </summary>
internal static class RefTestEmailLookupKeyBackfill
{
    private const int BatchSize = 250;
    private const int MaximumConcurrencyRetries = 3;

    internal static async Task EnsureEmailLookupKeysBackfilledAsync(
        RefTestManagementContext context,
        CancellationToken cancellationToken)
    {
        var concurrencyRetries = 0;
        while (true)
        {
            var pendingRefTests = await PrivacyWithdrawalQueries
                .EligibleRefTestsMissingEmailLookupKey(context.RefTests)
                .OrderBy(refTest => refTest.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (pendingRefTests.Count == 0)
                return;

            foreach (var refTest in pendingRefTests)
                refTest.BackfillEmailLookupKey();

            try
            {
                await context.SaveChangesWithRetryAsync(cancellationToken);
                foreach (var refTest in pendingRefTests)
                    context.Entry(refTest).State = EntityState.Detached;
                concurrencyRetries = 0;
            }
            catch (DbUpdateConcurrencyException)
            {
                // An email change or erasure won the optimistic-concurrency race. Discard the
                // stale values and reread current database state before deciding lookup is ready.
                foreach (var refTest in pendingRefTests)
                {
                    await context.Entry(refTest).ReloadAsync(cancellationToken);
                    context.Entry(refTest).State = EntityState.Detached;
                }
                if (++concurrencyRetries >= MaximumConcurrencyRetries)
                    throw;
            }
        }
    }
}
