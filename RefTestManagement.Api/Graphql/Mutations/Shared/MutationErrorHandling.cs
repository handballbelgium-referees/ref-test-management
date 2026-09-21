using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;

public static class MutationErrorHandling
{
    public const string GenericFailure = "The request could not be completed. Please try again later.";
    public const string NotFoundFailure = "One or more requested RefTests could not be found.";

    public static string GetUserSafeMessage(Exception exception) => exception switch
    {
        RefTestValidationException => exception.Message,
        InvalidRefTestStatusException => exception.Message,
        ArgumentException => exception.Message,
        RefTestNotFoundException => NotFoundFailure,
        _ => GenericFailure
    };

    public static string GetCorrelationId(IHttpContextAccessor? httpContextAccessor) =>
        httpContextAccessor?.HttpContext?.TraceIdentifier ?? "(none)";

    public static void LogMutationFailure(
        ILogger logger,
        Exception exception,
        string operationName,
        string correlationId,
        Guid? refTestId = null)
    {
        logger.LogError(
            LogRedaction.MaskEmails(exception),
            "Mutation {OperationName} failed for RefTest {RefTestId}. CorrelationId={CorrelationId}",
            operationName,
            refTestId ?? Guid.Empty,
            correlationId);
    }
}
