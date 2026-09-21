using HotChocolate.Execution;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql;

/// <summary>
/// Turns an optimistic concurrency conflict into an error the client can act on.
/// </summary>
/// <remarks>
/// Without this the conflict reaches the client as a masked "Unexpected Execution Error", which
/// tells the user nothing and invites them to retry the same stale write forever. It is not a
/// fault: it means someone else changed the record first, and the only sensible response is to
/// reload and reapply. Reporting it as such is the difference between a dead end and a recoverable
/// step.
///
/// This filter is registered before <see cref="UnhandledExceptionLoggingErrorFilter"/> and drops
/// the exception once handled, so contention is logged as a warning rather than raising an error
/// for something the system is designed to survive.
/// </remarks>
public sealed partial class ConcurrencyErrorFilter(
    ILogger<ConcurrencyErrorFilter> logger) : IErrorFilter
{
    private const string ErrorCode = "CONCURRENT_MODIFICATION";

    public IError OnError(IError error)
    {
        if (error.Exception is not DbUpdateConcurrencyException)
            return error;

        LogConcurrencyConflict(logger, error.Path?.ToString() ?? "(none)");

        return ErrorBuilder.FromError(error)
            .SetMessage(
                "This record was changed by someone else while you were editing it. "
                + "Reload it and apply your change again.")
            .SetCode(ErrorCode)
            .SetException(null)
            .Build();
    }

    [LoggerMessage(
        LogLevel.Warning,
        "Optimistic concurrency conflict at path {path}; the caller was asked to reload and retry")]
    private static partial void LogConcurrencyConflict(ILogger logger, string path);
}
