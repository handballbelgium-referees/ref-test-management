using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Application.Services;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Application;

public static partial class MutationFailureHandling
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

    public static void LogMutationFailure(
        ILogger logger,
        Exception exception,
        string operationName,
        string correlationId,
        Guid? refTestId = null) =>
        MutationLoggerMessages.LogFailure(
            logger, PiiRedaction.MaskEmails(exception, withholdStackTraceOnFailure: true),
            operationName, refTestId ?? Guid.Empty, correlationId);
}

internal static partial class MutationLoggerMessages
{
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Mutation {OperationName} failed for RefTest {RefTestId}. CorrelationId={CorrelationId}")]
    public static partial void LogFailure(
        ILogger logger,
        Exception exception,
        string operationName,
        Guid refTestId,
        string correlationId);
}
