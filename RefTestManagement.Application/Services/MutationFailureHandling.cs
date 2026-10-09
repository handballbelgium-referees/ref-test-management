using System.Text.RegularExpressions;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Application;

public static partial class MutationFailureHandling
{
    private const string MaskingFailed = "(exception text withheld: personal-data masking failed)";
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
            logger, MaskEmails(exception), operationName, refTestId ?? Guid.Empty, correlationId);

    private static Exception MaskEmails(Exception exception)
    {
        try
        {
            return new MaskedException(
                MaskEmailsInText(exception.Message) ?? string.Empty,
                MaskEmailsInText(exception.ToString()) ?? string.Empty,
                MaskEmailsInText(exception.StackTrace));
        }
        catch (RegexMatchTimeoutException)
        {
            return new MaskedException(MaskingFailed, MaskingFailed, MaskingFailed);
        }
    }

    private static string? MaskEmailsInText(string? text) =>
        string.IsNullOrEmpty(text)
            ? text
            : EmailPattern().Replace(text, match => MaskEmail(match.Value));

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "(none)";

        var separator = email.IndexOf('@');
        return separator <= 0 ? "***" : $"{email[0]}***{email[separator..]}";
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.None, matchTimeoutMilliseconds: 250)]
    private static partial Regex EmailPattern();

    private sealed class MaskedException(string message, string text, string? stackTrace) : Exception(message)
    {
        public override string? StackTrace { get; } = stackTrace;
        public override string ToString() => text;
    }
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
