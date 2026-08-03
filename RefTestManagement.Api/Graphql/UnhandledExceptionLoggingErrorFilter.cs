using HotChocolate;
using HotChocolate.Execution;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Api.Graphql;

/// <summary>
/// Logs the real exception behind a GraphQL error before HotChocolate masks it in the client response.
/// </summary>
public sealed partial class UnhandledExceptionLoggingErrorFilter(
    ILogger<UnhandledExceptionLoggingErrorFilter> logger) : IErrorFilter
{
    public IError OnError(IError error)
    {
        if (error.Exception is not null)
            LogUnhandledGraphQlException(logger, error.Exception, error.Path?.ToString() ?? "(none)");

        return error;
    }

    [LoggerMessage(LogLevel.Error, "Unhandled GraphQL resolver exception at path {path}")]
    private static partial void LogUnhandledGraphQlException(ILogger logger, Exception ex, string path);
}
