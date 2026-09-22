using HotChocolate.Execution;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;

namespace Handball.Belgium.RefTestManagement.Api.Graphql;

/// <summary>
/// Logs the real exception behind a GraphQL error before HotChocolate masks it in the client response.
/// </summary>
public sealed class UnhandledExceptionLoggingErrorFilter(
    ILogger<UnhandledExceptionLoggingErrorFilter> logger,
    IHttpContextAccessor httpContextAccessor) : IErrorFilter
{
    public IError OnError(IError error)
    {
        if (error.Exception is not null)
            logger.LogError(
                LogRedaction.MaskEmails(error.Exception),
                "Unhandled GraphQL resolver exception at path {Path}; correlationId={CorrelationId}",
                error.Path?.ToString() ?? "(none)",
                httpContextAccessor.HttpContext?.TraceIdentifier ?? "(none)");

        return error;
    }
}
