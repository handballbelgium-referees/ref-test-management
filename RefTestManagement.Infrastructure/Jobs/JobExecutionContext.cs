namespace Handball.Belgium.RefTestManagement.Infrastructure.Jobs;

/// <summary>
/// The job the background worker is running in this scope, if any. Side effects that are not
/// idempotent (sending an email) key themselves on it, so a retry of the same job is recognised by
/// the provider instead of repeating the effect.
/// </summary>
public sealed class JobExecutionContext
{
    public Guid? CurrentJobId { get; set; }
}
