using System.Text.RegularExpressions;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Logging;

/// <summary>
/// Helpers for keeping personal data out of log output.
/// </summary>
public static partial class LogRedaction
{
    /// <summary>
    /// Masks the local part of an email address so log lines stay useful for operational
    /// diagnosis (which domain, which provider) without recording who the recipient was.
    /// <c>john.doe@example.com</c> becomes <c>j***@example.com</c>. Use this wherever no
    /// RefTest id is available to identify the record instead.
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "(none)";

        var separator = email.IndexOf('@');

        return separator <= 0
            ? "***"
            : $"{email[0]}***{email[separator..]}";
    }

    /// <summary>
    /// Masks every email address embedded anywhere in free-form text, using the same
    /// <see cref="MaskEmail"/> shape.
    /// <para>
    /// Use this on text the application did not compose itself — exception messages and
    /// third-party API responses — before it is logged or, more importantly, persisted.
    /// Those strings are outside the privacy erasure path, so an address that reaches them
    /// would survive a participant's erasure request.
    /// </para>
    /// </summary>
    public static string? MaskEmailsInText(string? text) =>
        string.IsNullOrEmpty(text)
            ? text
            : EmailPattern().Replace(text, match => MaskEmail(match.Value));

    /// <summary>
    /// Wraps an exception so a logging provider cannot render an unmasked email address from it.
    /// <para>
    /// Masking an exception's <i>message string</i> is not enough: logging providers render the
    /// exception object itself, and <see cref="Exception.ToString"/> re-exposes the raw message
    /// along with every inner exception's. Pass the result of this method wherever an exception
    /// is handed to <c>ILogger</c> on a path that can carry a participant's address — the email
    /// provider's client and anything that deserializes a job payload.
    /// </para>
    /// <para>
    /// The stack trace is preserved as text rather than dropped, so diagnosis is unaffected.
    /// Never throws: it is only ever called from a catch block, where a secondary failure would
    /// lose the original error entirely.
    /// </para>
    /// </summary>
    public static Exception MaskEmails(Exception exception)
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
            // Masking is the whole point of this call; if it cannot be done, log a placeholder
            // rather than the original text.
            return new RedactedException(MaskingFailed, MaskingFailed, exception.StackTrace);
        }
    }

    private const string MaskingFailed = "(exception text withheld: personal-data masking failed)";

    // Deliberately broad rather than RFC-exact: the goal is to catch anything that looks like
    // an address, and over-matching only costs a few masked characters in a diagnostic string.
    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.None, matchTimeoutMilliseconds: 250)]
    private static partial Regex EmailPattern();
}

/// <summary>
/// A masked stand-in for an exception on its way to a logging provider. Produced by
/// <see cref="LogRedaction.MaskEmails"/>; it carries the original's text with every email
/// address masked, and its stack trace verbatim.
/// </summary>
public sealed class RedactedException(string message, string text, string? stackTrace) : Exception(message)
{
    public override string? StackTrace { get; } = stackTrace;

    public override string ToString() => text;
}

/// <summary>
/// Centralized source-generated logger messages for common service operations.
/// Reduces boilerplate code across services while maintaining zero-allocation logging.
/// <para>
/// These templates must never interpolate personal data — no names, email addresses,
/// invitation tokens, test URLs or assessment scores. Logs are retained far longer than the
/// records they describe and are not covered by the privacy erasure path, so anything written
/// here survives a participant's erasure request. Identify records by their RefTest id, and
/// mask recipient addresses with <see cref="LogRedaction.MaskEmail"/> where no id is available.
/// </para>
/// </summary>
public static partial class ServiceLoggerMessages
{
    // ========================================
    // Generic Service Operations
    // ========================================
    
    [LoggerMessage(LogLevel.Information, "Service {serviceName} is starting")]
    public static partial void LogServiceStarting(ILogger logger, string serviceName);

    [LoggerMessage(LogLevel.Information, "Service {serviceName} is stopping")]
    public static partial void LogServiceStopping(ILogger logger, string serviceName);

    [LoggerMessage(LogLevel.Error, "Error occurred in service {serviceName}")]
    public static partial void LogServiceError(ILogger logger, Exception ex, string serviceName);

    // ========================================
    // Email Service Operations
    // ========================================

    [LoggerMessage(LogLevel.Information, "Sending email to {recipient} with subject: {subject}")]
    public static partial void LogSendingEmail(ILogger logger, string recipient, string subject);

    [LoggerMessage(LogLevel.Information, "Email sent successfully to {recipient}")]
    public static partial void LogEmailSentSuccessfully(ILogger logger, string recipient);

    // responseBody is the email provider's raw error payload, logged for diagnosis. It can echo
    // back the address that was rejected, so callers must scrub it with
    // LogRedaction.MaskEmailsInText before passing it in.
    [LoggerMessage(LogLevel.Warning, "Email to {recipient} failed with status code {statusCode}: {responseBody}")]
    public static partial void LogEmailFailed(ILogger logger, string recipient, int statusCode, string responseBody);

    [LoggerMessage(LogLevel.Error, "Error sending email to {recipient}")]
    public static partial void LogEmailError(ILogger logger, Exception ex, string recipient);

    [LoggerMessage(LogLevel.Warning, "API key not configured. Email not sent.")]
    public static partial void LogApiKeyNotConfigured(ILogger logger);

    // The invitation token is a bearer credential: it grants access to the participant's test,
    // their results and their withdraw-consent action. Neither it nor the test URL that embeds
    // it may ever be logged.
    [LoggerMessage(LogLevel.Information, "Sending RefTest invitation for {refTestId} to {recipient}. Questions: {questions}, Time: {time} minutes")]
    public static partial void LogSendingRefTestInvitation(ILogger logger, Guid refTestId, string recipient, int questions, int time);

    [LoggerMessage(LogLevel.Information, "Sending RefTest results for {refTestId} to {recipient}")]
    public static partial void LogSendingRefTestResults(ILogger logger, Guid refTestId, string recipient);

    [LoggerMessage(LogLevel.Information, "Report email sent to {recipient}")]
    public static partial void LogReportEmailSent(ILogger logger, string recipient);

    // ========================================
    // Job Processing Operations
    // ========================================

    [LoggerMessage(LogLevel.Information, "Processing job {jobId} of type {jobType} (attempt {attempt}/{maxAttempts})")]
    public static partial void LogProcessingJob(ILogger logger, Guid jobId, JobType jobType, int attempt, int maxAttempts);

    [LoggerMessage(LogLevel.Information, "Successfully completed job {jobId} of type {jobType}")]
    public static partial void LogJobCompleted(ILogger logger, Guid jobId, JobType jobType);

    [LoggerMessage(LogLevel.Warning, "Job {jobId} of type {jobType} failed (attempt {attempt}/{maxAttempts}): {errorMessage}")]
    public static partial void LogJobFailed(ILogger logger, Guid jobId, JobType jobType, int attempt, int maxAttempts, string errorMessage);

    [LoggerMessage(LogLevel.Error, "Job {jobId} of type {jobType} failed permanently after {attempts} attempts")]
    public static partial void LogJobFailedPermanently(ILogger logger, Guid jobId, JobType jobType, int attempts);

    [LoggerMessage(LogLevel.Information, "Found {count} jobs to process")]
    public static partial void LogJobsFound(ILogger logger, int count);

    [LoggerMessage(LogLevel.Debug, "No jobs available for processing")]
    public static partial void LogNoJobsAvailable(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Enqueued {jobType} job {jobId}")]
    public static partial void LogJobEnqueued(ILogger logger, JobType jobType, Guid jobId);

    [LoggerMessage(LogLevel.Information, "Enqueued invitation email job {jobId} for RefTest {refTestId}")]
    public static partial void LogEnqueuedInvitationEmail(ILogger logger, Guid jobId, Guid refTestId);

    [LoggerMessage(LogLevel.Information, "Enqueued result email job {jobId} for RefTest {refTestId}")]
    public static partial void LogEnqueuedResultEmail(ILogger logger, Guid jobId, Guid refTestId);

    [LoggerMessage(LogLevel.Information, "Enqueued report email job {jobId} for {recipientCount} recipients")]
    public static partial void LogEnqueuedReportEmail(ILogger logger, Guid jobId, int recipientCount);

    [LoggerMessage(LogLevel.Debug, "Sending invitation email for RefTest {refTestId}")]
    public static partial void LogSendingInvitationEmail(ILogger logger, Guid refTestId);

    [LoggerMessage(LogLevel.Debug, "Sending result email for RefTest {refTestId}")]
    public static partial void LogSendingResultEmail(ILogger logger, Guid refTestId);

    [LoggerMessage(LogLevel.Debug, "Sending report email to {count} recipients")]
    public static partial void LogSendingReportEmail(ILogger logger, int count);

    [LoggerMessage(LogLevel.Information, "Approval notification sent to {approverCount} approver(s) for {refTestCount} RefTest(s)")]
    public static partial void LogApprovalNotificationSent(ILogger logger, int approverCount, int refTestCount);

    [LoggerMessage(LogLevel.Information, "Approval decision ({decision}) email sent to creator {recipient} for {refTestCount} RefTest(s)")]
    public static partial void LogApprovalDecisionEmailSent(ILogger logger, string decision, string recipient, int refTestCount);

    [LoggerMessage(LogLevel.Error, "Failed to deserialize job payload for job {jobId}")]
    public static partial void LogJobDeserializationError(ILogger logger, Exception ex, Guid jobId);

    [LoggerMessage(LogLevel.Error,
        "Failed to mask personal data in the error message for job {jobId} - a placeholder was stored instead")]
    public static partial void LogErrorMessageMaskingFailed(ILogger logger, Exception ex, Guid jobId);

    // ========================================
    // Report Service Operations
    // ========================================

    [LoggerMessage(LogLevel.Warning, "No recipient emails configured for reports")]
    public static partial void LogNoReportRecipients(ILogger logger);

    // ========================================
    // Logo Service Operations
    // ========================================

    [LoggerMessage(LogLevel.Warning, "Failed to download logo from {logoUrl}")]
    public static partial void LogLogoDownloadFailed(ILogger logger, Exception ex, string logoUrl);

    // ========================================
    // RefTest Expiration Service Operations
    // ========================================

    [LoggerMessage(LogLevel.Debug, "No potentially expired RefTests found")]
    public static partial void LogNoPotentiallyExpiredTests(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Checking {count} potentially expired RefTests")]
    public static partial void LogCheckingExpiredTests(ILogger logger, int count);

    [LoggerMessage(LogLevel.Debug, "RefTest {refTestId} is expired: {isExpired}, Status: {status}")]
    public static partial void LogRefTestExpirationCheck(ILogger logger, Guid refTestId, bool isExpired, RefTestStatus status);

    [LoggerMessage(LogLevel.Information, "Auto-completed expired RefTest {refTestId}")]
    public static partial void LogAutoCompleted(ILogger logger, Guid refTestId);

    [LoggerMessage(LogLevel.Error, "Failed to auto-complete expired RefTest {refTestId}")]
    public static partial void LogAutoCompleteFailed(ILogger logger, Exception ex, Guid refTestId);

    [LoggerMessage(LogLevel.Information, "Expired RefTest {refTestId} in status {status}")]
    public static partial void LogExpired(ILogger logger, Guid refTestId, RefTestStatus status);

    [LoggerMessage(LogLevel.Information, "Processed {totalCount} expired RefTests: {expiredCount} expired, {completedCount} auto-completed")]
    public static partial void LogProcessingSummary(ILogger logger, int totalCount, int expiredCount, int completedCount);

    [LoggerMessage(LogLevel.Information, "Enqueued {count} RefTest expiration jobs")]
    public static partial void LogEnqueuedExpirationJobs(ILogger logger, int count);

    [LoggerMessage(LogLevel.Debug, "Enqueued {action} job for RefTest {refTestId}")]
    public static partial void LogEnqueuedExpirationJob(ILogger logger, RefTestExpirationAction action, Guid refTestId);

    [LoggerMessage(LogLevel.Information, "Canceled {count} pending job(s) for RefTest {refTestId}")]
    public static partial void LogCanceledCountPendingJobsForRefTestRefTestId(ILogger logger, int count, Guid refTestId);

    [LoggerMessage(LogLevel.Information, "Canceled {count} pending result email job(s) for RefTest {refTestId}")]
    public static partial void LogCanceledCountPendingResultEmailJobsForRefTestRefTestId(ILogger logger, int count, Guid refTestId);
    
    // ========================================
    // Database Operations
    // ========================================

    [LoggerMessage(LogLevel.Debug, "Executing query: {queryName}")]
    public static partial void LogQueryExecuting(ILogger logger, string queryName);

    [LoggerMessage(LogLevel.Warning, "Query {queryName} returned no results")]
    public static partial void LogQueryNoResults(ILogger logger, string queryName);

    [LoggerMessage(LogLevel.Error, "Database error in {operation}")]
    public static partial void LogDatabaseError(ILogger logger, Exception ex, string operation);

    // ========================================
    // PDF Generation Operations
    // ========================================

    [LoggerMessage(LogLevel.Information, "Generating PDF for {documentType} - {identifier}")]
    public static partial void LogGeneratingPdf(ILogger logger, string documentType, string identifier);

    [LoggerMessage(LogLevel.Information, "PDF generated successfully for {documentType} - {identifier} ({sizeKb} KB)")]
    public static partial void LogPdfGenerated(ILogger logger, string documentType, string identifier, int sizeKb);

    [LoggerMessage(LogLevel.Error, "Error generating PDF for {documentType} - {identifier}")]
    public static partial void LogPdfGenerationError(ILogger logger, Exception ex, string documentType, string identifier);

    // ========================================
    // Cleanup Operations
    // ========================================

    [LoggerMessage(LogLevel.Information, "Running cleanup for {resourceType} - removing items older than {retentionDays} days")]
    public static partial void LogCleanupStarting(ILogger logger, string resourceType, int retentionDays);

    [LoggerMessage(LogLevel.Information, "Cleanup completed for {resourceType} - removed {count} items")]
    public static partial void LogCleanupCompleted(ILogger logger, string resourceType, int count);

    [LoggerMessage(LogLevel.Error, "Error during cleanup of {resourceType}")]
    public static partial void LogCleanupError(ILogger logger, Exception ex, string resourceType);

    [LoggerMessage(LogLevel.Information,
        "Cleanup for {resourceType} hit its per-run limit after {count} items - the remainder is processed on the next run")]
    public static partial void LogCleanupBatchLimitReached(ILogger logger, string resourceType, int count);

    // ========================================
    // External API Operations
    // ========================================

    [LoggerMessage(LogLevel.Information, "Calling external API: {apiName} - {endpoint}")]
    public static partial void LogExternalApiCall(ILogger logger, string apiName, string endpoint);

    [LoggerMessage(LogLevel.Information, "External API call successful: {apiName} ({durationMs}ms)")]
    public static partial void LogExternalApiSuccess(ILogger logger, string apiName, long durationMs);

    [LoggerMessage(LogLevel.Warning, "External API call failed: {apiName} - Status: {statusCode}")]
    public static partial void LogExternalApiFailure(ILogger logger, string apiName, int statusCode);

    [LoggerMessage(LogLevel.Error, "Error calling external API: {apiName}")]
    public static partial void LogExternalApiError(ILogger logger, Exception ex, string apiName);

    // ========================================
    // Validation Operations
    // ========================================

    [LoggerMessage(LogLevel.Warning, "Validation failed for {operation}: {validationErrors}")]
    public static partial void LogValidationFailed(ILogger logger, string operation, string validationErrors);

    [LoggerMessage(LogLevel.Debug, "Validation passed for {operation}")]
    public static partial void LogValidationPassed(ILogger logger, string operation);

    // ========================================
    // Performance Monitoring
    // ========================================

    [LoggerMessage(LogLevel.Information, "Operation {operationName} completed in {durationMs}ms")]
    public static partial void LogOperationDuration(ILogger logger, string operationName, long durationMs);

    [LoggerMessage(LogLevel.Warning, "Operation {operationName} took longer than expected: {durationMs}ms (threshold: {thresholdMs}ms)")]
    public static partial void LogSlowOperation(ILogger logger, string operationName, long durationMs, long thresholdMs);
}
