using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Logging;

/// <summary>
/// Centralized source-generated logger messages for common service operations.
/// Reduces boilerplate code across services while maintaining zero-allocation logging.
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

    [LoggerMessage(LogLevel.Information, "Sending email to {email} with subject: {subject}")]
    public static partial void LogSendingEmail(ILogger logger, string email, string subject);

    [LoggerMessage(LogLevel.Information, "Email sent successfully to {email}")]
    public static partial void LogEmailSentSuccessfully(ILogger logger, string email);

    [LoggerMessage(LogLevel.Warning, "Email to {email} failed with status code {statusCode}: {responseBody}")]
    public static partial void LogEmailFailed(ILogger logger, string email, int statusCode, string responseBody);

    [LoggerMessage(LogLevel.Error, "Error sending email to {email}")]
    public static partial void LogEmailError(ILogger logger, Exception ex, string email);

    [LoggerMessage(LogLevel.Warning, "API key not configured. Email not sent.")]
    public static partial void LogApiKeyNotConfigured(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Sending RefTest invitation to {email}. Token: {token}, Questions: {questions}, Time: {time} minutes. URL: {url}")]
    public static partial void LogSendingRefTestInvitation(ILogger logger, string email, string token, int questions, int time, string url);

    [LoggerMessage(LogLevel.Information, "Sending RefTest results to {email}. QuestionScore: {questionScore}/{totalQuestions}, AnswerScore: {answerScore}/{answerTotal} ({percentage:F1}%)")]
    public static partial void LogSendingRefTestResults(ILogger logger, string email, int questionScore, int totalQuestions, int answerScore, int answerTotal, double percentage);

    [LoggerMessage(LogLevel.Information, "Report email sent to {email}")]
    public static partial void LogReportEmailSent(ILogger logger, string email);

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

    [LoggerMessage(LogLevel.Information, "Enqueued invitation email job {jobId} for {email}")]
    public static partial void LogEnqueuedInvitationEmail(ILogger logger, Guid jobId, string email);

    [LoggerMessage(LogLevel.Information, "Enqueued result email job {jobId} for {email}")]
    public static partial void LogEnqueuedResultEmail(ILogger logger, Guid jobId, string email);

    [LoggerMessage(LogLevel.Information, "Enqueued report email job {jobId} for {recipientCount} recipients")]
    public static partial void LogEnqueuedReportEmail(ILogger logger, Guid jobId, int recipientCount);

    [LoggerMessage(LogLevel.Debug, "Sending invitation email to {email}")]
    public static partial void LogSendingInvitationEmail(ILogger logger, string email);

    [LoggerMessage(LogLevel.Debug, "Sending result email to {email}")]
    public static partial void LogSendingResultEmail(ILogger logger, string email);

    [LoggerMessage(LogLevel.Debug, "Sending report email to {count} recipients")]
    public static partial void LogSendingReportEmail(ILogger logger, int count);

    [LoggerMessage(LogLevel.Error, "Failed to deserialize job payload for job {jobId}")]
    public static partial void LogJobDeserializationError(ILogger logger, Exception ex, Guid jobId);

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

    [LoggerMessage(LogLevel.Information, "Auto-completed expired RefTest {refTestId} for {email}")]
    public static partial void LogAutoCompleted(ILogger logger, Guid refTestId, string email);

    [LoggerMessage(LogLevel.Error, "Failed to auto-complete expired RefTest {refTestId} for {email}")]
    public static partial void LogAutoCompleteFailed(ILogger logger, Exception ex, Guid refTestId, string email);

    [LoggerMessage(LogLevel.Information, "Expired RefTest {refTestId} in status {status} for {email}")]
    public static partial void LogExpired(ILogger logger, Guid refTestId, RefTestStatus status, string email);

    [LoggerMessage(LogLevel.Information, "Processed {totalCount} expired RefTests: {expiredCount} expired, {completedCount} auto-completed")]
    public static partial void LogProcessingSummary(ILogger logger, int totalCount, int expiredCount, int completedCount);

    [LoggerMessage(LogLevel.Information, "Enqueued {count} RefTest expiration jobs")]
    public static partial void LogEnqueuedExpirationJobs(ILogger logger, int count);

    [LoggerMessage(LogLevel.Debug, "Enqueued {action} job for RefTest {refTestId}")]
    public static partial void LogEnqueuedExpirationJob(ILogger logger, RefTestExpirationAction action, Guid refTestId);

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
