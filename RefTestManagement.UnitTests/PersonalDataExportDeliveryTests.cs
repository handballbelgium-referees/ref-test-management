using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Handball.Belgium.RefTestManagement.Domain.Privacy.Events;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Infrastructure;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class PersonalDataExportDeliveryTests
{
    private const string ParticipantEmail = "ada@example.org";
    private static readonly string ChallengeKey = new('K', 43);
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DeliverySelectsAllCurrentStatusesAndRetainedEventsWithoutThirdPartyPii()
    {
        using var database = SqliteTestDatabase.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var request = CreateVerifiedRequest(ParticipantEmail, "fr");
        var expectedIds = new Dictionary<RefTestStatus, Guid>();
        var tokens = new List<string>();

        await using (var seed = database.CreateContext())
        {
            var title = RefTestTitle.Create("Season 2026");
            seed.RefTestTitles.Add(title);
            await seed.SaveChangesAsync(cancellationToken);

            foreach (var status in Enum.GetValues<RefTestStatus>())
            {
                var email = status is RefTestStatus.Pending or RefTestStatus.Completed
                    ? "Ada@Example.org"
                    : ParticipantEmail;
                var refTest = CreateRefTestForStatus(title.Id, status, email);
                seed.RefTests.Add(refTest);
                expectedIds.Add(status, refTest.Id);
                tokens.Add(refTest.GetIssuedToken());
            }

            var otherEmail = CreateRefTest(title.Id, "other@example.org", "Other", "Person");
            var anonymizedButStillLinked = CreateRefTest(title.Id, ParticipantEmail, "Erased", "Person");
            seed.RefTests.AddRange(otherEmail, anonymizedButStillLinked);
            seed.Entry(anonymizedButStillLinked)
                .Property(refTest => refTest.IsAnonymized)
                .CurrentValue = true;

            var completedId = expectedIds[RefTestStatus.Completed].ToString();
            var pendingId = expectedIds[RefTestStatus.Pending].ToString();
            seed.AuditEvents.AddRange(
                new AuditEvent
                {
                    StreamId = completedId,
                    Version = 1,
                    Type = "RefTestDetailsUpdated",
                    Timestamp = Now,
                    ActorName = "Staff Operator",
                    ActorEmail = "staff@example.org",
                    Data = """
                           {
                             "firstName": { "oldValue": "***", "newValue": "Ada" },
                             "email": { "oldValue": "ada@example.org", "newValue": "Ada@Example.org" },
                             "creatorName": { "newValue": "Staff Operator" },
                             "creatorEmail": { "newValue": "staff@example.org" },
                             "rejectionReason": { "newValue": "Staff Operator contacted staff@example.org" },
                             "invitationToken": { "newValue": "invitation-secret" },
                             "answerKey": { "newValue": "correct-answer-secret" },
                             "details": "Participant ada@example.org; unrelated staff@example.org"
                           }
                           """,
                    IsArchived = true,
                    RedactedAt = Now.AddDays(-1)
                },
                new AuditEvent
                {
                    StreamId = pendingId,
                    Version = 1,
                    Type = "RefTestStarted",
                    Timestamp = Now.AddMinutes(1),
                    ActorName = "***",
                    ActorEmail = "***",
                    Data = """{"firstName":"***","lastName":"***"}""",
                    IsArchived = false,
                    RedactedAt = Now.AddDays(-1)
                },
                new AuditEvent
                {
                    StreamId = pendingId,
                    Version = 2,
                    Type = "RefTestPrivacyNoticeAccepted",
                    Timestamp = Now.AddMinutes(2),
                    ActorName = "Ada Lovelace",
                    ActorEmail = ParticipantEmail,
                    Data = """{"email":"ada@example.org","noticeVersion":"v2"}"""
                },
                new AuditEvent
                {
                    StreamId = otherEmail.Id.ToString(),
                    Version = 1,
                    Type = "UnrelatedEvent",
                    Timestamp = Now,
                    ActorName = "Someone Else",
                    ActorEmail = "third-party@example.org",
                    Data = """{"email":"third-party@example.org"}"""
                });

            seed.PersonalDataExportRequests.Add(request);
            await seed.SaveChangesAsync(cancellationToken);
        }

        var pdfService = new RecordingPdfService();
        var emailService = new RecordingEmailService();
        using var loggerFactory = NullLoggerFactory.Instance;
        var staffContext = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, "Staff Operator"),
                    new Claim(ClaimTypes.Email, "staff@example.org")
                ], "test"))
            }
        };

        await using (var deliveryContext = database.CreateContext(
                         new AuditSaveChangesInterceptor(staffContext, new AuditLogOptions())))
        {
            var handler = CreateHandler(
                deliveryContext,
                pdfService,
                emailService,
                new BackgroundJobConfiguration(),
                loggerFactory);
            await handler.HandleAsync(DeliveryJob(request.Id), cancellationToken);
        }

        var document = Assert.IsType<PersonalDataExportDocumentData>(pdfService.Document);
        Assert.Equal(ParticipantEmail, document.RecipientEmail);
        Assert.Equal("fr", document.Locale);
        Assert.Equal(Enum.GetNames<RefTestStatus>().Length, document.RefTests.Count);
        Assert.Equal(expectedIds.Values.OrderBy(id => id), document.RefTests.Select(refTest => refTest.Id).OrderBy(id => id));
        Assert.DoesNotContain(document.RefTests, refTest => refTest.Email == "other@example.org");
        Assert.DoesNotContain(document.RefTests, refTest => refTest.FirstName == "Erased");

        var completed = Assert.Single(document.RefTests, refTest => refTest.CompletedAt is not null);
        Assert.Equal(12, completed.NumberOfQuestions);
        Assert.Equal(8, completed.QuestionScore);
        Assert.Equal(9, completed.AnswerScore);
        Assert.Equal(10, completed.AnswerTotal);
        Assert.Equal("v2", completed.PrivacyNoticeVersion);
        Assert.NotNull(completed.PrivacyNoticeAcceptedAt);

        Assert.Equal(3, document.AuditEvents.Count);
        var archivedEvent = Assert.Single(document.AuditEvents, auditEvent => auditEvent.IsArchived);
        Assert.Equal(Now.AddDays(-1), archivedEvent.RedactedAt);
        Assert.Equal(PersonalDataExportActorKind.Staff, archivedEvent.ActorKind);
        Assert.Equal(string.Empty, archivedEvent.ActorEmail);
        Assert.NotEqual("Staff Operator", archivedEvent.ActorName);
        using (var archivedData = JsonDocument.Parse(archivedEvent.Data!))
        {
            Assert.Equal("***", archivedData.RootElement.GetProperty("firstName")
                .GetProperty("oldValue").GetString());
            Assert.Equal("Ada", archivedData.RootElement.GetProperty("firstName")
                .GetProperty("newValue").GetString());
            Assert.False(archivedData.RootElement.TryGetProperty("creatorName", out _));
            Assert.False(archivedData.RootElement.TryGetProperty("creatorEmail", out _));
            Assert.False(archivedData.RootElement.TryGetProperty("rejectionReason", out _));
            Assert.False(archivedData.RootElement.TryGetProperty("invitationToken", out _));
            Assert.False(archivedData.RootElement.TryGetProperty("answerKey", out _));
            var details = archivedData.RootElement.GetProperty("details").GetString()!;
            Assert.Contains(ParticipantEmail, details, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("staff@example.org", details, StringComparison.OrdinalIgnoreCase);
        }

        var redactedEvent = Assert.Single(
            document.AuditEvents,
            auditEvent => auditEvent.ActorKind == PersonalDataExportActorKind.Redacted);
        Assert.Equal("***", redactedEvent.ActorName);
        Assert.Equal("***", redactedEvent.ActorEmail);
        var participantEvent = Assert.Single(
            document.AuditEvents,
            auditEvent => auditEvent.ActorKind == PersonalDataExportActorKind.Participant);
        Assert.Equal("Ada Lovelace", participantEvent.ActorName);
        Assert.Equal(ParticipantEmail, participantEvent.ActorEmail);

        var serializedDocument = JsonSerializer.Serialize(document);
        Assert.All(tokens, token => Assert.DoesNotContain(token, serializedDocument, StringComparison.Ordinal));
        Assert.DoesNotContain("Staff Operator", serializedDocument, StringComparison.Ordinal);
        Assert.DoesNotContain("staff@example.org", serializedDocument, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ParticipantEmail, Assert.Single(emailService.Recipients));

        await using var verification = database.CreateContext();
        var deliveredRequest = await verification.PersonalDataExportRequests
            .SingleAsync(candidate => candidate.Id == request.Id, cancellationToken);
        Assert.Equal(string.Empty, deliveredRequest.Email);
        Assert.Equal("en", deliveredRequest.Locale);
        Assert.Null(deliveredRequest.KeyHash);
        Assert.Null(deliveredRequest.ProtectedDeliveryKey);

        var deliveryAudit = await verification.AuditEvents
            .SingleAsync(auditEvent => auditEvent.StreamId == request.Id.ToString(), cancellationToken);
        Assert.Equal(PersonalDataExportDeliveredEvent.EventType, deliveryAudit.Type);
        Assert.Equal("Verified participant", deliveryAudit.ActorName);
        Assert.Equal(string.Empty, deliveryAudit.ActorEmail);
    }

    [Fact]
    public async Task RetryRecordsFailureThenDeliveryAndACompletedDuplicateDoesNotResend()
    {
        using var database = SqliteTestDatabase.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var request = CreateVerifiedRequest(ParticipantEmail, "nl");

        await using (var seed = database.CreateContext())
        {
            var title = RefTestTitle.Create("Retry Test");
            seed.RefTestTitles.Add(title);
            await seed.SaveChangesAsync(cancellationToken);
            var refTest = CreateRefTest(title.Id, ParticipantEmail, "Ada", "Lovelace");
            seed.RefTests.Add(refTest);
            seed.PersonalDataExportRequests.Add(request);
            await seed.SaveChangesAsync(cancellationToken);
        }

        var emailService = new RecordingEmailService
        {
            Failure = new InvalidOperationException($"Provider echoed {ParticipantEmail} and PDF bytes")
        };
        var pdfService = new RecordingPdfService();
        var staffContext = StaffHttpContextAccessor();
        var job = DeliveryJob(request.Id);

        await using (var firstAttempt = database.CreateContext(
                         new AuditSaveChangesInterceptor(staffContext, new AuditLogOptions())))
        {
            var failure = await Assert.ThrowsAsync<PersonalDataExportDeliveryException>(
                () => CreateHandler(
                        firstAttempt,
                        pdfService,
                        emailService,
                        new BackgroundJobConfiguration(),
                        NullLoggerFactory.Instance)
                    .HandleAsync(job, cancellationToken));
            Assert.DoesNotContain(ParticipantEmail, failure.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PDF bytes", failure.Message, StringComparison.Ordinal);
        }

        await using (var afterFailure = database.CreateContext())
        {
            var requestAfterFailure = await afterFailure.PersonalDataExportRequests
                .SingleAsync(candidate => candidate.Id == request.Id, cancellationToken);
            Assert.Equal(ParticipantEmail, requestAfterFailure.Email);
            Assert.Equal("nl", requestAfterFailure.Locale);
            Assert.Null(requestAfterFailure.LastDeliveryAttemptAt);
            Assert.Equal(1, requestAfterFailure.DeliveryAttemptCount);
        }

        emailService.Failure = null;
        job.MarkAsFailed("safe test failure", maxAttempts: 3);
        await using (var retryContext = database.CreateContext(
                         new AuditSaveChangesInterceptor(staffContext, new AuditLogOptions())))
        {
            await CreateHandler(
                    retryContext,
                    pdfService,
                    emailService,
                    new BackgroundJobConfiguration(),
                    NullLoggerFactory.Instance)
                .HandleAsync(job, cancellationToken);
        }

        await using (var duplicateContext = database.CreateContext())
        {
            await CreateHandler(
                    duplicateContext,
                    pdfService,
                    emailService,
                    new BackgroundJobConfiguration(),
                    NullLoggerFactory.Instance)
                .HandleAsync(job, cancellationToken);
        }

        Assert.Equal(2, emailService.Recipients.Count);
        Assert.All(emailService.Recipients, recipient => Assert.Equal(ParticipantEmail, recipient));
        await using var verification = database.CreateContext();
        var auditEvents = await verification.AuditEvents
            .Where(auditEvent => auditEvent.StreamId == request.Id.ToString())
            .OrderBy(auditEvent => auditEvent.Version)
            .ToListAsync(cancellationToken);
        Assert.Equal(
            [
                PersonalDataExportDeliveryFailedEvent.EventType,
                PersonalDataExportDeliveredEvent.EventType
            ],
            auditEvents.Select(auditEvent => auditEvent.Type));
        Assert.All(auditEvents, auditEvent =>
        {
            Assert.Equal("Verified participant", auditEvent.ActorName);
            Assert.Equal(string.Empty, auditEvent.ActorEmail);
            Assert.DoesNotContain(ParticipantEmail, auditEvent.Data, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task TerminalFailureIsAuditedAndClearsRecipientAndLocale()
    {
        using var database = SqliteTestDatabase.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        const string recipientEmail = "bea@example.org";
        var request = CreateVerifiedRequest(recipientEmail, "de");

        await using (var seed = database.CreateContext())
        {
            var title = RefTestTitle.Create("Failure Test");
            seed.RefTestTitles.Add(title);
            await seed.SaveChangesAsync(cancellationToken);
            var refTest = CreateRefTest(title.Id, recipientEmail, "Bea", "Example");
            seed.RefTests.Add(refTest);
            seed.PersonalDataExportRequests.Add(request);
            await seed.SaveChangesAsync(cancellationToken);
        }

        var emailService = new RecordingEmailService
        {
            Failure = new InvalidOperationException($"Provider error {recipientEmail}")
        };
        var pdfService = new RecordingPdfService();
        var job = DeliveryJob(request.Id);
        var staffContext = StaffHttpContextAccessor();
        var configuration = new BackgroundJobConfiguration { MaxAttempts = 2 };

        await using (var firstAttempt = database.CreateContext(
                         new AuditSaveChangesInterceptor(staffContext, new AuditLogOptions())))
        {
            await Assert.ThrowsAsync<PersonalDataExportDeliveryException>(
                () => CreateHandler(firstAttempt, pdfService, emailService, configuration, NullLoggerFactory.Instance)
                    .HandleAsync(job, cancellationToken));
        }

        job.MarkAsFailed("safe test failure", configuration.MaxAttempts);
        await using (var finalAttempt = database.CreateContext(
                         new AuditSaveChangesInterceptor(staffContext, new AuditLogOptions())))
        {
            await Assert.ThrowsAsync<JobPayloadException>(
                () => CreateHandler(finalAttempt, pdfService, emailService, configuration, NullLoggerFactory.Instance)
                    .HandleAsync(job, cancellationToken));
        }

        await using var verification = database.CreateContext();
        var failedRequest = await verification.PersonalDataExportRequests
            .SingleAsync(candidate => candidate.Id == request.Id, cancellationToken);
        Assert.Equal(string.Empty, failedRequest.Email);
        Assert.Equal("en", failedRequest.Locale);
        Assert.Null(failedRequest.KeyHash);
        Assert.Null(failedRequest.ProtectedDeliveryKey);

        var failedEvents = await verification.AuditEvents
            .Where(auditEvent => auditEvent.StreamId == request.Id.ToString())
            .OrderBy(auditEvent => auditEvent.Version)
            .ToListAsync(cancellationToken);
        Assert.Equal(2, failedEvents.Count);
        Assert.All(failedEvents, auditEvent =>
        {
            Assert.Equal(PersonalDataExportDeliveryFailedEvent.EventType, auditEvent.Type);
            Assert.Equal("Verified participant", auditEvent.ActorName);
            Assert.DoesNotContain(recipientEmail, auditEvent.Data, StringComparison.OrdinalIgnoreCase);
        });
        using var lastEvent = JsonDocument.Parse(failedEvents[^1].Data!);
        Assert.True(lastEvent.RootElement.GetProperty("terminal").GetBoolean());
    }

    [Fact]
    public async Task UnrecoverablePdfSizeFailureIsAuditedWithoutSendingOrTruncating()
    {
        using var database = SqliteTestDatabase.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        const string recipientEmail = "cora@example.org";
        var request = CreateVerifiedRequest(recipientEmail, "en");
        var emailService = new RecordingEmailService();
        var pdfService = new RecordingPdfService
        {
            Failure = new PersonalDataExportSizeLimitException()
        };

        await using (var seed = database.CreateContext())
        {
            var title = RefTestTitle.Create("Oversized Export");
            seed.RefTestTitles.Add(title);
            await seed.SaveChangesAsync(cancellationToken);
            seed.RefTests.Add(CreateRefTest(title.Id, recipientEmail, "Cora", "Example"));
            seed.PersonalDataExportRequests.Add(request);
            await seed.SaveChangesAsync(cancellationToken);
        }

        await using (var deliveryContext = database.CreateContext(
                         new AuditSaveChangesInterceptor(StaffHttpContextAccessor(), new AuditLogOptions())))
        {
            await Assert.ThrowsAsync<JobPayloadException>(
                () => CreateHandler(
                        deliveryContext,
                        pdfService,
                        emailService,
                        new BackgroundJobConfiguration(),
                        NullLoggerFactory.Instance)
                    .HandleAsync(DeliveryJob(request.Id), cancellationToken));
        }

        Assert.Empty(emailService.Recipients);
        await using var verification = database.CreateContext();
        var failedRequest = await verification.PersonalDataExportRequests
            .SingleAsync(candidate => candidate.Id == request.Id, cancellationToken);
        Assert.Equal(string.Empty, failedRequest.Email);
        var failure = await verification.AuditEvents
            .SingleAsync(auditEvent => auditEvent.StreamId == request.Id.ToString(), cancellationToken);
        using var failureData = JsonDocument.Parse(failure.Data!);
        Assert.Equal("SizeLimitExceeded", failureData.RootElement.GetProperty("failureCode").GetString());
        Assert.True(failureData.RootElement.GetProperty("terminal").GetBoolean());
    }

    [Fact]
    public void PdfPartsAreLocalizedValidAndNumberedInsteadOfTruncated()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var random = new Random(706);
        var eventData = new List<PersonalDataExportAuditEventData>();
        var streamId = Guid.NewGuid().ToString();

        for (var index = 0; index < 96; index++)
        {
            var message = string.Join(' ', Enumerable.Range(0, 14).Select(_ =>
                new string(Enumerable.Range(0, 48)
                    .Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[random.Next(36)])
                    .ToArray())));
            eventData.Add(new PersonalDataExportAuditEventData(
                streamId,
                index + 1,
                "RefTestDetailsUpdated",
                Now.AddMinutes(index),
                PersonalDataExportActorKind.System,
                string.Empty,
                string.Empty,
                JsonSerializer.Serialize(new { message }),
                IsArchived: index % 2 == 0,
                RedactedAt: index % 2 == 0 ? Now : null));
        }

        var refTest = new PersonalDataExportRefTestData(
            Guid.Parse(streamId),
            "Ada",
            "Lovelace",
            ParticipantEmail,
            NumberOfQuestions: 12,
            MaxTimeInMinutes: 30,
            CreatedAt: Now,
            StartedAt: Now.AddMinutes(1),
            CompletedAt: Now.AddMinutes(30),
            ExpiredAt: null,
            QuestionScore: 10,
            AnswerScore: 12,
            AnswerTotal: 12,
            Percentage: 83.33,
            Language: "fr",
            PrivacyNoticeVersion: "v2",
            PrivacyNoticeAcceptedAt: Now,
            ScheduledAt: null);
        var document = new PersonalDataExportDocumentData(
            ParticipantEmail,
            "fr",
            [refTest],
            eventData);

        var fullExport = new PersonalDataExportPdfService(new TranslationService())
            .GenerateAttachments(document);
        var oneEventExport = new PersonalDataExportPdfService(new TranslationService())
            .GenerateAttachments(document with { AuditEvents = [eventData[0]] });
        var testPartLimit = Assert.Single(oneEventExport).Content.Length * 3;
        Assert.True(Assert.Single(fullExport).Content.Length > testPartLimit);

        var service = new PersonalDataExportPdfService(new TranslationService(), testPartLimit);
        var attachments = service.GenerateAttachments(document);

        Assert.True(attachments.Count > 1);
        Assert.Equal(
            Enumerable.Range(1, attachments.Count)
                .Select(part => $"PersonalDataExport_Part_{part:D3}_of_{attachments.Count:D3}.pdf"),
            attachments.Select(attachment => attachment.FileName));
        Assert.All(attachments, attachment =>
        {
            Assert.True(attachment.Content.Length <= testPartLimit);
            Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(attachment.Content, 0, 5));
        });

        var english = new PersonalDataExportPdfService(new TranslationService())
            .GenerateAttachments(document with { Locale = "en" });
        var french = new PersonalDataExportPdfService(new TranslationService())
            .GenerateAttachments(document);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(Assert.Single(english).Content, 0, 5));
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(Assert.Single(french).Content, 0, 5));
        Assert.NotEqual(
            Convert.ToBase64String(Assert.Single(english).Content),
            Convert.ToBase64String(Assert.Single(french).Content));
    }

    private static PersonalDataExportRequest CreateVerifiedRequest(string email, string locale)
    {
        var request = PersonalDataExportRequest.Create(
            email,
            locale,
            ChallengeKey,
            $"protected:{ChallengeKey}",
            Now,
            Now.AddHours(24));
        Assert.True(request.TryConfirm(ChallengeKey, Now.AddMinutes(1)));
        return request;
    }

    private static RefTest CreateRefTestForStatus(Guid titleId, RefTestStatus status, string email)
    {
        var requiresApproval = status is RefTestStatus.PendingApproval or RefTestStatus.Rejected;
        var refTest = RefTest.Create(
            titleId,
            status == RefTestStatus.Completed ? "Ada" : $"Participant{(int)status}",
            "Lovelace",
            email,
            numberOfQuestions: 12,
            maxTimeInMinutes: 30,
            questionIds: ["question-1", "question-2"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: status == RefTestStatus.Completed,
            requiresApproval: requiresApproval,
            creatorName: "Staff Operator",
            creatorEmail: "staff@example.org");

        switch (status)
        {
            case RefTestStatus.Pending:
            case RefTestStatus.PendingApproval:
                break;
            case RefTestStatus.InProgress:
                refTest.AcceptPrivacyNotice("v2");
                refTest.Start("v2");
                refTest.SaveProgress(1, ["selected-answer"], "nl");
                break;
            case RefTestStatus.Completed:
                refTest.AcceptPrivacyNotice("v2");
                refTest.Start("v2");
                refTest.Complete(8, 9, 10, 80, ["answer-1"], ["question-2"], ["answer-2"], "fr");
                refTest.SendResults();
                break;
            case RefTestStatus.Expired:
                refTest.Expire();
                break;
            case RefTestStatus.Rejected:
                refTest.Reject("Staff Operator reviewed this participant record.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        return refTest;
    }

    private static RefTest CreateRefTest(Guid titleId, string email, string firstName, string lastName) =>
        RefTest.Create(
            titleId,
            firstName,
            lastName,
            email,
            numberOfQuestions: 10,
            maxTimeInMinutes: 30,
            questionIds: ["question-1"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: false);

    private static Job DeliveryJob(Guid requestId)
    {
        var payload = JsonSerializer.Serialize(
            new PersonalDataExportDeliveryEmailPayload(requestId),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return Job.Create(JobType.PersonalDataExportDeliveryEmail, payload);
    }

    private static PersonalDataExportDeliveryEmailJobHandler CreateHandler(
        RefTestManagementContext context,
        RecordingPdfService pdfService,
        RecordingEmailService emailService,
        BackgroundJobConfiguration configuration,
        ILoggerFactory loggerFactory) =>
        new(
            context,
            pdfService,
            emailService,
            configuration,
            loggerFactory.CreateLogger<PersonalDataExportDeliveryEmailJobHandler>());

    private static IHttpContextAccessor StaffHttpContextAccessor() => new HttpContextAccessor
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "Staff Operator"),
                new Claim(ClaimTypes.Email, "staff@example.org")
            ], "test"))
        }
    };

    private sealed class RecordingPdfService : IPersonalDataExportPdfService
    {
        public PersonalDataExportDocumentData? Document { get; private set; }
        public Exception? Failure { get; init; }

        public IReadOnlyList<EmailAttachment> GenerateAttachments(PersonalDataExportDocumentData document)
        {
            Document = document;
            if (Failure is not null)
                throw Failure;
            return [new EmailAttachment("PersonalDataExport.pdf", [0x25, 0x50, 0x44, 0x46])];
        }
    }

    private sealed class RecordingEmailService : IEmailService
    {
        public Exception? Failure { get; set; }
        public List<string> Recipients { get; } = [];
        public List<IReadOnlyList<EmailAttachment>> Attachments { get; } = [];

        public Task SendPersonalDataExportAsync(
            string recipientEmail,
            IReadOnlyList<EmailAttachment> attachments,
            CancellationToken cancellationToken)
        {
            Recipients.Add(recipientEmail);
            Attachments.Add(attachments);
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }

        public Task SendRefTestInvitationAsync(
            Guid refTestId,
            string name,
            string email,
            string token,
            int numberOfQuestions,
            int maxTimeInMinutes,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SendRefTestResultsAsync(
            Guid refTestId,
            string name,
            string email,
            int questionScore,
            int answerScore,
            int totalQuestions,
            int answerTotal,
            double percentage,
            List<string> selectedAnswerIds,
            List<string> wrongQuestionIds,
            List<string> wrongAnswerIds,
            List<Question> questionsWithCorrectAnswers,
            bool scheduleEmail,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SendReportEmailAsync(
            string recipientEmail,
            byte[] excelReport,
            byte[] pdfReport,
            string timestamp,
            int refTestCount,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SendApprovalNotificationAsync(
            string approverName,
            string approverEmail,
            string creatorName,
            string? titleValue,
            List<(string FullName, string Email, DateTime? ScheduledAt)> refTestItems,
            string baseUrl,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SendApprovalDecisionAsync(
            string creatorName,
            string creatorEmail,
            string approverName,
            bool isApproved,
            string? rejectionReason,
            string? titleValue,
            List<(string FullName, string Email, DateTime? ScheduledAt)> refTestItems,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SendPersonalDataExportVerificationAsync(
            string recipientEmail,
            string challengeKey,
            DateTime expiresAt,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
