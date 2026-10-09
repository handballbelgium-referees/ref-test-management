using System.Text.RegularExpressions;

namespace Handball.Belgium.RefTestManagement.Application.Services;

/// <summary>Pure PII masking helpers shared by Application and Infrastructure logging.</summary>
public static partial class PiiRedaction
{
    private const string MaskingFailed = "(exception text withheld: personal-data masking failed)";

    /// <summary>
    /// Masks the local part of an email address so log lines stay useful for operational diagnosis
    /// (which domain, which provider) without recording who the recipient was.
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "(none)";
        var separator = email.IndexOf('@');
        return separator <= 0 ? "***" : $"{email[0]}***{email[separator..]}";
    }

    /// <summary>
    /// Masks every email address embedded in free-form text, using the same shape as
    /// <see cref="MaskEmail"/>.
    /// </summary>
    public static string? MaskEmailsInText(string? text) =>
        string.IsNullOrEmpty(text)
            ? text
            : EmailPattern().Replace(text, match => MaskEmail(match.Value));

    /// <summary>
    /// Wraps an exception so a logging provider cannot render an unmasked email address from it.
    /// The stack trace remains text and is masked along with messages and inner exceptions.
    /// </summary>
    public static Exception MaskEmails(Exception exception, bool withholdStackTraceOnFailure = false)
    {
        try
        {
            return new RedactedException(
                MaskEmailsInText(exception.Message) ?? string.Empty,
                MaskEmailsInText(exception.ToString()) ?? string.Empty,
                MaskEmailsInText(exception.StackTrace));
        }
        catch (RegexMatchTimeoutException)
        {
            return new RedactedException(
                MaskingFailed,
                MaskingFailed,
                withholdStackTraceOnFailure ? MaskingFailed : exception.StackTrace);
        }
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.None, matchTimeoutMilliseconds: 250)]
    private static partial Regex EmailPattern();
}

/// <summary>
/// A masked stand-in for an exception on its way to a logging provider. It carries the original's
/// text with every email address masked and its stack trace as safe text.
/// </summary>
public sealed class RedactedException(string message, string text, string? stackTrace) : Exception(message)
{
    public override string? StackTrace { get; } = stackTrace;
    public override string ToString() => text;
}
