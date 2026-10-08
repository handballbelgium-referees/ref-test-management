using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Handball.Belgium.RefTestManagement.Domain.Privacy.Events;
using Handball.Belgium.RefTestManagement.Domain.Security;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;

/// <summary>
/// Builds and sends the verified participant's export from current RefTest and retained audit
/// data. The durable payload contains only the confirmed request ID; PDF bytes remain in memory.
/// </summary>
public sealed class PersonalDataExportDeliveryEmailJobHandler(
    RefTestManagementContext context,
    IPersonalDataExportPdfService pdfService,
    IEmailService emailService,
    BackgroundJobConfiguration jobConfiguration,
    ILogger<PersonalDataExportDeliveryEmailJobHandler> logger) : IJobHandler
{
    private const int AuditStreamBatchSize = 500;

    public async Task HandleAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = JobPayload.Deserialize<PersonalDataExportDeliveryEmailPayload>(job, logger);
        var request = await context.PersonalDataExportRequests
            .SingleOrDefaultAsync(candidate => candidate.Id == payload.RequestId, cancellationToken);

        // A duplicate job, an erased request, or an unverified request has nothing safe to send.
        if (request is null || request.VerifiedAt is null || string.IsNullOrEmpty(request.Email))
            return;

        var now = DateTime.UtcNow;
        var leaseDuration = TimeSpan.FromMinutes(Math.Max(1, jobConfiguration.LockDurationMinutes));
        if (!request.TryStartPersonalDataExportDelivery(now, leaseDuration, job.Attempts > 0))
            return;

        try
        {
            await context.SaveChangesWithRetryAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent delivery job or erasure won the request's optimistic-concurrency
            // claim. It owns the outcome; this duplicate must not send.
            return;
        }

        var recipientEmail = request.Email;
        IReadOnlyList<EmailAttachment> attachments;

        try
        {
            var normalizedRecipientEmail = PrivacyWithdrawalChallenge.NormalizeEmail(recipientEmail);
            var emailLookupKey = TokenService.HashBytes(normalizedRecipientEmail);
            var refTests = await LoadCurrentRefTestsAsync(
                normalizedRecipientEmail,
                emailLookupKey,
                cancellationToken);
            if (refTests.Count == 0)
            {
                await RecordFailureAsync(
                    request,
                    PersonalDataExportDeliveryFailureCode.NoCurrentRecords,
                    isTerminal: true,
                    cancellationToken);
                throw new JobPayloadException("No current RefTest records are available for this export.");
            }

            var auditEvents = await LoadRetainedAuditEventsAsync(
                refTests,
                recipientEmail,
                cancellationToken);
            var document = new PersonalDataExportDocumentData(
                recipientEmail,
                refTests,
                auditEvents);

            attachments = await pdfService.GenerateAttachmentsAsync(document);

            // Recheck both the request and every exported record after the email service has
            // prepared its provider payload. The query completes before the network call, so no
            // transaction or row lock spans I/O.
            // Keep the ID collection as a List for consistent translation across database providers.
            var exportedRefTestIds = refTests.Select(refTest => refTest.Id).ToList();
            var wasSent = await emailService.SendPersonalDataExportAsync(
                recipientEmail,
                attachments,
                async finalCheckCancellationToken =>
                {
                    var requestIsValid = await context.PersonalDataExportRequests
                        .AsNoTracking()
                        .AnyAsync(
                            candidate => candidate.Id == payload.RequestId
                                         && candidate.VerifiedAt != null
                                         && candidate.Email == recipientEmail,
                            finalCheckCancellationToken);
                    if (!requestIsValid)
                        return false;

                    await RefTestEmailLookupKeyBackfill.EnsureEmailLookupKeysBackfilledAsync(
                        context,
                        finalCheckCancellationToken);
                    var stillOwnedRecords = await PrivacyWithdrawalQueries
                        .MatchingRefTestsByEmailLookupKey(
                            context.RefTests.Where(refTest => exportedRefTestIds.Contains(refTest.Id)),
                            emailLookupKey)
                        .AsNoTracking()
                        .Select(refTest => new { refTest.Id, refTest.Email })
                        .ToListAsync(finalCheckCancellationToken);
                    return stillOwnedRecords.Count == exportedRefTestIds.Count
                           && stillOwnedRecords.All(refTest => string.Equals(
                               PrivacyWithdrawalChallenge.NormalizeEmail(refTest.Email),
                               normalizedRecipientEmail,
                               StringComparison.Ordinal));
                },
                cancellationToken);
            if (!wasSent)
                return;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Leave the request lease in place. The queue's expired lock lets a reclaimed job
            // take over after a worker is stopped mid-delivery.
            throw;
        }
        catch (PersonalDataExportSizeLimitException)
        {
            await RecordFailureAsync(
                request,
                PersonalDataExportDeliveryFailureCode.SizeLimitExceeded,
                isTerminal: true,
                cancellationToken);
            throw new JobPayloadException("The personal-data export exceeds the email provider's size limits.");
        }
        catch (JobPayloadException)
        {
            throw;
        }
        catch (Exception)
        {
            // Provider and rendering exceptions can contain message content. The job queue stores
            // exception messages, so only this fixed, non-sensitive text crosses the boundary.
            var isTerminal = request.DeliveryAttemptCount >= Math.Max(1, jobConfiguration.MaxAttempts);
            await RecordFailureAsync(
                request,
                PersonalDataExportDeliveryFailureCode.DeliveryFailed,
                isTerminal,
                cancellationToken);

            if (isTerminal)
                throw new JobPayloadException("Personal-data export delivery failed after all retry attempts.");

            throw new PersonalDataExportDeliveryException();
        }

        if (request.MarkPersonalDataExportDelivered(DateTime.UtcNow, attachments.Count))
            await context.SaveChangesWithRetryAsync(cancellationToken);
    }

    private async Task<List<PersonalDataExportRefTestData>> LoadCurrentRefTestsAsync(
        string normalizedRecipientEmail,
        byte[] emailLookupKey,
        CancellationToken cancellationToken)
    {
        await RefTestEmailLookupKeyBackfill.EnsureEmailLookupKeysBackfilledAsync(
            context,
            cancellationToken);
        var candidates = await PrivacyWithdrawalQueries.MatchingRefTestsByEmailLookupKey(
                context.RefTests,
                emailLookupKey)
            .AsNoTracking()
            .OrderBy(refTest => refTest.CreatedAt)
            .ThenBy(refTest => refTest.Id)
            // Creator identity is staff data; rejection text is free-form and may mention unrelated people.
            // Invitation tokens and anonymization metadata are intentionally not selected either.
            .Select(refTest => new
            {
                refTest.Id,
                refTest.FirstName,
                refTest.LastName,
                refTest.Email,
                refTest.NumberOfQuestions,
                refTest.MaxTimeInMinutes,
                refTest.QuestionIds,
                refTest.CreatedAt,
                refTest.StartedAt,
                refTest.CompletedAt,
                refTest.ExpiredAt,
                refTest.CurrentQuestionIndex,
                refTest.QuestionScore,
                refTest.AnswerScore,
                refTest.AnswerTotal,
                refTest.Percentage,
                refTest.Language,
                refTest.PrivacyNoticeVersion,
                refTest.PrivacyNoticeAcceptedAt,
                refTest.ScheduledAt
            })
            .ToListAsync(cancellationToken);

        // The indexed digest narrows database candidates; verify the full normalized address
        // in application code so a hypothetical digest collision cannot add unrelated data.
        return candidates
            .Where(refTest => string.Equals(
                PrivacyWithdrawalChallenge.NormalizeEmail(refTest.Email),
                normalizedRecipientEmail,
                StringComparison.Ordinal))
            .Select(refTest => new PersonalDataExportRefTestData(
                refTest.Id,
                refTest.FirstName,
                refTest.LastName,
                refTest.Email,
                refTest.NumberOfQuestions,
                refTest.MaxTimeInMinutes,
                refTest.CreatedAt,
                refTest.StartedAt,
                refTest.CompletedAt,
                refTest.ExpiredAt,
                refTest.QuestionScore,
                refTest.AnswerScore,
                refTest.AnswerTotal,
                refTest.Percentage,
                refTest.Language,
                refTest.PrivacyNoticeVersion,
                refTest.PrivacyNoticeAcceptedAt,
                refTest.ScheduledAt,
                refTest.CurrentQuestionIndex,
                refTest.QuestionIds.Count))
            .ToList();
    }

    private async Task<List<PersonalDataExportAuditEventData>> LoadRetainedAuditEventsAsync(
        IReadOnlyList<PersonalDataExportRefTestData> refTests,
        string recipientEmail,
        CancellationToken cancellationToken)
    {
        var streamIds = refTests.Select(refTest => refTest.Id.ToString()).ToArray();
        var retainedEvents = new List<AuditEvent>();

        // Keep each IN-list below common provider parameter limits without excluding any matched
        // stream. No archive predicate is applied: archived rows are still retained audit data.
        foreach (var streamIdBatch in streamIds.Chunk(AuditStreamBatchSize))
        {
            var batch = await context.AuditEvents
                .AsNoTracking()
                .Where(auditEvent => streamIdBatch.Contains(auditEvent.StreamId))
                .OrderBy(auditEvent => auditEvent.SeqId)
                .ToListAsync(cancellationToken);
            retainedEvents.AddRange(batch);
        }

        return PersonalDataExportAuditSanitizer.SanitizeHistory(retainedEvents, recipientEmail);
    }

    private async Task RecordFailureAsync(
        PersonalDataExportRequest request,
        PersonalDataExportDeliveryFailureCode failureCode,
        bool isTerminal,
        CancellationToken cancellationToken)
    {
        if (request.MarkPersonalDataExportDeliveryFailed(
                DateTime.UtcNow,
                failureCode,
                isTerminal))
            await context.SaveChangesWithRetryAsync(cancellationToken);
    }
}

/// <summary>A retryable export-email failure with no recipient or message data in its text.</summary>
public sealed class PersonalDataExportDeliveryException()
    : Exception("Personal-data export delivery failed.");
