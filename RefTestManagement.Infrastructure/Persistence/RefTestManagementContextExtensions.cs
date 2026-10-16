namespace Handball.Belgium.RefTestManagement.Infrastructure;

public static class RefTestManagementContextExtensions
{
    /// <summary>
    /// Saves changes as a single retriable unit, so the audit interceptor's own query during
    /// SavingChangesAsync doesn't conflict with the SqlServer retrying execution strategy.
    /// </summary>
    public static Task<int> SaveChangesWithRetryAsync(
        this RefTestManagementContext context,
        CancellationToken cancellationToken = default)
    {
        return ((IJobPersistenceContext)context).SaveChangesWithRetryAsync(cancellationToken);
    }
}
