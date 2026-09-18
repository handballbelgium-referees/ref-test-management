using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Interceptors;

/// <summary>
/// Advances the optimistic concurrency token of every modified entity before it is written.
/// </summary>
/// <remarks>
/// A counter token is portable across all four supported providers, but unlike a SQL Server
/// <c>rowversion</c> nothing advances it automatically — the application has to. Doing that here
/// rather than in each domain method means a new mutation cannot forget: there is one place that
/// decides, and it runs for every save on every path, including ones written later.
///
/// The next value is derived from the <em>original</em> value rather than the current one, so the
/// counter still advances exactly once per save even if something else had already touched the
/// property. EF compares against that same original value, so the <c>WHERE</c> clause keeps
/// carrying the version that was actually read; only the value being written is new.
/// </remarks>
public sealed class ConcurrencyTokenInterceptor : SaveChangesInterceptor
{
    /// <summary>Name of the token property. Entities opt in simply by declaring it.</summary>
    private const string VersionProperty = "Version";

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AdvanceTokens(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AdvanceTokens(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void AdvanceTokens(DbContext? context)
    {
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified)
                continue;

            var version = entry.Metadata.FindProperty(VersionProperty);
            if (version is null || version.ClrType != typeof(long) || !version.IsConcurrencyToken)
                continue;

            var property = entry.Property(VersionProperty);
            property.CurrentValue = (long)(property.OriginalValue ?? 0L) + 1;
        }
    }
}
