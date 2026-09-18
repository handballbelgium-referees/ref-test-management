using Handball.Belgium.RefTestManagement.Domain.Jobs;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;

/// <summary>
/// Handles one <see cref="JobType"/>.
/// </summary>
/// <remarks>
/// Handlers are registered keyed by <see cref="JobType"/> and resolved from the per-iteration DI
/// scope, so each one declares its own dependencies in its constructor rather than pulling them
/// out of an <see cref="IServiceProvider"/> at the point of use. That is the point of the split: a
/// handler is now a class you can construct in a test, and the queue mechanics around it —
/// claiming, leasing, retrying, cleanup — no longer have to be stood up to exercise one.
///
/// A handler signals failure by throwing. <see cref="BackgroundJobService"/> owns the whole failure
/// policy: it decides between a retry and a permanent failure, scrubs the message of personal data,
/// and persists the outcome. Handlers must not swallow exceptions and must not set job state
/// themselves.
/// </remarks>
public interface IJobHandler
{
    Task HandleAsync(Job job, CancellationToken cancellationToken);
}
