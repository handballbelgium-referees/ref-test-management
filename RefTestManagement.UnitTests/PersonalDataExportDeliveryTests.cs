using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Handball.Belgium.RefTestManagement.Domain.Privacy.Events;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTests.Events;
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
        var request = CreateVerifiedRequest(ParticipantEmail);
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
                    Type = "RefTestCreated",
                    Timestamp = Now.AddMinutes(-2),
                    ActorName = "Staff Operator",
                    ActorEmail = "staff@example.org",
                    Data = """{"firstName":"Ada","lastName":"Lovelace","email":"ada@example.org"}"""
                },
                new AuditEvent
                {
                    StreamId = completedId,
                    Version = 2,
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
                    Type = "RefTestCreated",
                    Timestamp = Now.AddMinutes(-2),
                    ActorName = "Staff Operator",
                    ActorEmail = "staff@example.org",
                    Data = """{"firstName":"Ada","lastName":"Lovelace","email":"ada@example.org"}"""
                },
                new AuditEvent
                {
                    StreamId = pendingId,
                    Version = 2,
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
                    Version = 3,
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

        Assert.Equal(5, document.AuditEvents.Count);
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
        Assert.Null(deliveredRequest.KeyHash);
        Assert.Null(deliveredRequest.ProtectedDeliveryKey);

        var deliveryAudit = await verification.AuditEvents
            .SingleAsync(auditEvent => auditEvent.StreamId == request.Id.ToString(), cancellationToken);
        Assert.Equal(PersonalDataExportDeliveredEvent.EventType, deliveryAudit.Type);
        Assert.Equal("Verified participant", deliveryAudit.ActorName);
        Assert.Equal(string.Empty, deliveryAudit.ActorEmail);
    }

    [Fact]
    public async Task DeliveryExcludesPriorOwnersNameOnlyHistoryAfterEmailTransfer()
    {
        const string priorOwnerEmail = "alice@example.org";
        const string currentOwnerEmail = "bea@example.org";
        const string aliceOldFirstName = "AliceBefore";
        const string aliceNewFirstName = "Alicia";
        const string aliceLastName = "AliceSurname";
        using var database = SqliteTestDatabase.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var request = CreateVerifiedRequest(currentOwnerEmail);

        await using (var seed = database.CreateContext())
        {
            var title = RefTestTitle.Create("Season 2026");
            seed.RefTestTitles.Add(title);
            await seed.SaveChangesAsync(cancellationToken);

            var refTest = CreateRefTest(title.Id, currentOwnerEmail, "Bea", "Current");
            var streamId = refTest.Id.ToString();
            seed.RefTests.Add(refTest);
            seed.AuditEvents.AddRange(
                CreateRefTestCreatedAuditEvent(
                    1,
                    streamId,
                    aliceOldFirstName,
                    aliceLastName,
                    priorOwnerEmail),
                CreateAuditEvent(
                    2,
                    streamId,
                    "RefTestStarted",
                    new { firstName = aliceOldFirstName, lastName = aliceLastName, email = priorOwnerEmail },
                    $"{aliceOldFirstName} {aliceLastName}",
                    priorOwnerEmail),
                CreateNameOnlyDetailsUpdatedAuditEvent(
                    3,
                    streamId,
                    aliceOldFirstName,
                    aliceNewFirstName,
                    aliceLastName,
                    aliceLastName,
                    priorOwnerEmail),
                CreateDetailsUpdatedAuditEvent(
                    4,
                    streamId,
                    aliceNewFirstName,
                    "Bea",
                    aliceLastName,
                    "Current",
                    priorOwnerEmail,
                    currentOwnerEmail),
                CreateAuditEvent(
                    5,
                    streamId,
                    "RefTestStarted",
                    new { firstName = "Bea", lastName = "Current", email = currentOwnerEmail },
                    "Bea own activity",
                    currentOwnerEmail));

            seed.PersonalDataExportRequests.Add(request);
            await seed.SaveChangesAsync(cancellationToken);
        }

        var pdfService = new RecordingPdfService();
        var emailService = new RecordingEmailService();
        using var loggerFactory = NullLoggerFactory.Instance;

        await using (var deliveryContext = database.CreateContext(
                         new AuditSaveChangesInterceptor(StaffHttpContextAccessor(), new AuditLogOptions())))
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
        Assert.Equal(currentOwnerEmail, document.RecipientEmail);
        Assert.Equal(
            new[] { "RefTestDetailsUpdated", "RefTestStarted" },
            document.AuditEvents.Select(auditEvent => auditEvent.Type));

        var sanitizedTransfer = Assert.Single(document.AuditEvents, auditEvent => auditEvent.Version == 4);
        using (var transferData = JsonDocument.Parse(sanitizedTransfer.Data!))
        {
            Assert.Equal(
                "***",
                transferData.RootElement.GetProperty("firstName").GetProperty("old").GetString());
            Assert.Equal(
                "***",
                transferData.RootElement.GetProperty("lastName").GetProperty("old").GetString());
            Assert.Equal(
                "***",
                transferData.RootElement.GetProperty("email").GetProperty("old").GetString());
            Assert.Equal(
                currentOwnerEmail,
                transferData.RootElement.GetProperty("email").GetProperty("new").GetString());
        }

        var beaActivity = Assert.Single(document.AuditEvents, auditEvent => auditEvent.Version == 5);
        Assert.Equal(PersonalDataExportActorKind.Participant, beaActivity.ActorKind);
        Assert.Equal(currentOwnerEmail, beaActivity.ActorEmail);

        var serializedExport = JsonSerializer.Serialize(document);
        Assert.DoesNotContain(aliceOldFirstName, serializedExport, StringComparison.Ordinal);
        Assert.DoesNotContain(aliceNewFirstName, serializedExport, StringComparison.Ordinal);
        Assert.DoesNotContain(aliceLastName, serializedExport, StringComparison.Ordinal);
        Assert.DoesNotContain(priorOwnerEmail, serializedExport, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bea own activity", serializedExport, StringComparison.Ordinal);
        Assert.Equal(currentOwnerEmail, Assert.Single(emailService.Recipients));
    }

    [Fact]
    public void SanitizeHistoryIncludesNewOwnerTransitionWithoutPriorOwnerIdentity()
    {
        const string streamId = "ownership-transition";
        const string oldOwnerEmail = "alice@example.org";
        const string newOwnerEmail = "bea@example.org";
        var transition = CreateDetailsUpdatedAuditEvent(
            3,
            streamId,
            "Alice",
            "Bea",
            "Former",
            "Current",
            oldOwnerEmail,
            newOwnerEmail);
        var events = new[]
        {
            CreateAuditEvent(
                4,
                streamId,
                "RefTestStarted",
                new { firstName = "Bea", email = newOwnerEmail },
                "Bea Current",
                newOwnerEmail),
            transition,
            CreateAuditEvent(
                2,
                streamId,
                "RefTestStarted",
                new { firstName = "Alice", email = oldOwnerEmail },
                "Alice Former",
                oldOwnerEmail),
            CreateRefTestCreatedAuditEvent(1, streamId, "Alice", "Former", oldOwnerEmail)
        };
        var originalTransitionData = transition.Data;

        var sanitized = PersonalDataExportAuditSanitizer.SanitizeHistory(events, newOwnerEmail);

        Assert.Equal(
            new[] { "RefTestDetailsUpdated", "RefTestStarted" },
            sanitized.Select(auditEvent => auditEvent.Type));
        var sanitizedTransition = Assert.Single(
            sanitized,
            auditEvent => auditEvent.Type == "RefTestDetailsUpdated");
        using var transitionData = JsonDocument.Parse(sanitizedTransition.Data!);
        Assert.Equal("***", transitionData.RootElement.GetProperty("firstName").GetProperty("old").GetString());
        Assert.Equal("Bea", transitionData.RootElement.GetProperty("firstName").GetProperty("new").GetString());
        Assert.Equal("***", transitionData.RootElement.GetProperty("lastName").GetProperty("old").GetString());
        Assert.Equal("Current", transitionData.RootElement.GetProperty("lastName").GetProperty("new").GetString());
        Assert.Equal("***", transitionData.RootElement.GetProperty("email").GetProperty("old").GetString());
        Assert.Equal(newOwnerEmail, transitionData.RootElement.GetProperty("email").GetProperty("new").GetString());
        Assert.Equal(originalTransitionData, transition.Data);

        var serializedHistory = JsonSerializer.Serialize(sanitized);
        Assert.DoesNotContain("Alice", serializedHistory, StringComparison.Ordinal);
        Assert.DoesNotContain(oldOwnerEmail, serializedHistory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizeHistoryIncludesCurrentOwnerEventsAfterRedactedCreation()
    {
        const string streamId = "redacted-creation";
        const string ownerEmail = "ada@example.org";
        var redactedCreation = CreateRedactedAuditEvent(
            CreateRefTestCreatedAuditEvent(1, streamId, "Ada", "Lovelace", ownerEmail));
        var originalCreationData = redactedCreation.Data;
        using (var sourceData = JsonDocument.Parse(redactedCreation.Data!))
        {
            Assert.Equal("***", sourceData.RootElement.GetProperty("firstName").GetString());
            Assert.Equal("***", sourceData.RootElement.GetProperty("lastName").GetString());
            Assert.Equal("***", sourceData.RootElement.GetProperty("email").GetString());
        }

        var events = new[]
        {
            CreateAuditEvent(
                2,
                streamId,
                "RefTestStarted",
                new { firstName = "Ada", lastName = "Lovelace", email = ownerEmail },
                "Ada Lovelace",
                ownerEmail),
            redactedCreation
        };

        var sanitized = PersonalDataExportAuditSanitizer.SanitizeHistory(events, ownerEmail);

        Assert.Equal(
            new[] { "RefTestCreated", "RefTestStarted" },
            sanitized.Select(auditEvent => auditEvent.Type));
        var sanitizedCreation = Assert.Single(sanitized, auditEvent => auditEvent.Type == "RefTestCreated");
        Assert.Equal(PersonalDataExportActorKind.Redacted, sanitizedCreation.ActorKind);
        Assert.Equal("***", sanitizedCreation.ActorName);
        Assert.Equal("***", sanitizedCreation.ActorEmail);
        Assert.NotNull(sanitizedCreation.RedactedAt);
        using var sanitizedData = JsonDocument.Parse(sanitizedCreation.Data!);
        Assert.Equal("***", sanitizedData.RootElement.GetProperty("firstName").GetString());
        Assert.Equal("***", sanitizedData.RootElement.GetProperty("lastName").GetString());
        Assert.Equal("***", sanitizedData.RootElement.GetProperty("email").GetString());
        Assert.Equal(originalCreationData, redactedCreation.Data);
    }

    [Fact]
    public void SanitizeHistoryExcludesPriorOwnerRedactedCreationAcrossFullyRedactedReassignment()
    {
        const string streamId = "redacted-reassignment";
        const string oldOwnerEmail = "alice@example.org";
        const string currentOwnerEmail = "bea@example.org";
        var redactedCreation = CreateRedactedAuditEvent(
            CreateRefTestCreatedAuditEvent(1, streamId, "Alice", "Former", oldOwnerEmail));
        var transitionSource = CreateAuditEvent(
            3,
            streamId,
            "RefTestDetailsUpdated",
            new
            {
                firstName = new { old = "Alice", @new = "Bea" },
                lastName = new { old = "Former", @new = "Current" },
                email = new { old = oldOwnerEmail, @new = currentOwnerEmail },
                invitationToken = "retained-but-sensitive-token"
            },
            "Staff Operator",
            "staff@example.org");
        var redactedTransition = CreateRedactedAuditEvent(transitionSource);
        var originalCreationData = redactedCreation.Data;
        var originalTransitionData = redactedTransition.Data;
        using (var transitionSourceData = JsonDocument.Parse(redactedTransition.Data!))
        {
            Assert.Equal(
                "***",
                transitionSourceData.RootElement.GetProperty("firstName").GetProperty("old").GetString());
            Assert.Equal(
                "***",
                transitionSourceData.RootElement.GetProperty("firstName").GetProperty("new").GetString());
            Assert.Equal(
                "***",
                transitionSourceData.RootElement.GetProperty("email").GetProperty("old").GetString());
            Assert.Equal(
                "***",
                transitionSourceData.RootElement.GetProperty("email").GetProperty("new").GetString());
        }

        var events = new[]
        {
            CreateAuditEvent(
                4,
                streamId,
                "RefTestCompleted",
                new { firstName = "Bea", lastName = "Current", email = currentOwnerEmail },
                "Bea Current",
                currentOwnerEmail),
            redactedTransition,
            redactedCreation
        };

        var sanitized = PersonalDataExportAuditSanitizer.SanitizeHistory(events, currentOwnerEmail);

        Assert.Equal(
            new[] { "RefTestDetailsUpdated", "RefTestCompleted" },
            sanitized.Select(auditEvent => auditEvent.Type));
        Assert.DoesNotContain(sanitized, auditEvent => auditEvent.Type == "RefTestCreated");
        var sanitizedTransition = Assert.Single(
            sanitized,
            auditEvent => auditEvent.Type == "RefTestDetailsUpdated");
        Assert.Equal(PersonalDataExportActorKind.Redacted, sanitizedTransition.ActorKind);
        Assert.NotNull(sanitizedTransition.RedactedAt);
        using (var sanitizedTransitionData = JsonDocument.Parse(sanitizedTransition.Data!))
        {
            Assert.Equal(
                "***",
                sanitizedTransitionData.RootElement.GetProperty("firstName").GetProperty("old").GetString());
            Assert.Equal(
                "***",
                sanitizedTransitionData.RootElement.GetProperty("email").GetProperty("new").GetString());
            Assert.False(sanitizedTransitionData.RootElement.TryGetProperty("invitationToken", out _));
        }

        var serializedHistory = JsonSerializer.Serialize(sanitized);
        Assert.DoesNotContain("retained-but-sensitive-token", serializedHistory, StringComparison.Ordinal);
        Assert.DoesNotContain(oldOwnerEmail, serializedHistory, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Alice", serializedHistory, StringComparison.Ordinal);
        Assert.Contains(currentOwnerEmail, serializedHistory, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalCreationData, redactedCreation.Data);
        Assert.Equal(originalTransitionData, redactedTransition.Data);
    }

    [Fact]
    public void SanitizeHistoryDoesNotExposeOldOwnerActivityAfterRedactedCreationAndReadableTransfer()
    {
        const string streamId = "redacted-creation-readable-transfer";
        const string oldOwnerEmail = "alice@example.org";
        const string currentOwnerEmail = "bea@example.org";
        var redactedCreation = CreateRedactedAuditEvent(
            CreateRefTestCreatedAuditEvent(1, streamId, "Alice", "Former", oldOwnerEmail));
        var oldOwnerActivity = CreateAuditEvent(
            2,
            streamId,
            "RefTestStarted",
            new { firstName = "Alice", lastName = "Former", email = oldOwnerEmail },
            "Alice Former",
            oldOwnerEmail);
        var transition = CreateDetailsUpdatedAuditEvent(
            3,
            streamId,
            "Alice",
            "Bea",
            "Former",
            "Current",
            oldOwnerEmail,
            currentOwnerEmail);
        var events = new[]
        {
            CreateAuditEvent(
                4,
                streamId,
                "RefTestCompleted",
                new { firstName = "Bea", lastName = "Current", email = currentOwnerEmail },
                "Bea Current",
                currentOwnerEmail),
            transition,
            oldOwnerActivity,
            redactedCreation
        };
        var originalTransitionData = transition.Data;

        var sanitized = PersonalDataExportAuditSanitizer.SanitizeHistory(events, currentOwnerEmail);

        Assert.Equal(
            new[] { "RefTestDetailsUpdated", "RefTestCompleted" },
            sanitized.Select(auditEvent => auditEvent.Type));
        Assert.DoesNotContain(sanitized, auditEvent => auditEvent.Type == "RefTestStarted");
        Assert.DoesNotContain(sanitized, auditEvent => auditEvent.Type == "RefTestCreated");
        var sanitizedTransition = Assert.Single(
            sanitized,
            auditEvent => auditEvent.Type == "RefTestDetailsUpdated");
        using var sanitizedTransitionData = JsonDocument.Parse(sanitizedTransition.Data!);
        Assert.Equal(
            "***",
            sanitizedTransitionData.RootElement.GetProperty("firstName").GetProperty("old").GetString());
        Assert.Equal(
            "Bea",
            sanitizedTransitionData.RootElement.GetProperty("firstName").GetProperty("new").GetString());
        Assert.Equal(
            "***",
            sanitizedTransitionData.RootElement.GetProperty("email").GetProperty("old").GetString());
        Assert.Equal(
            currentOwnerEmail,
            sanitizedTransitionData.RootElement.GetProperty("email").GetProperty("new").GetString());

        var serializedHistory = JsonSerializer.Serialize(sanitized);
        Assert.DoesNotContain("Alice", serializedHistory, StringComparison.Ordinal);
        Assert.DoesNotContain(oldOwnerEmail, serializedHistory, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(currentOwnerEmail, serializedHistory, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalTransitionData, transition.Data);
    }

    [Fact]
    public void SanitizeHistoryExcludesEarlierOwnersAcrossMultipleEmailChanges()
    {
        const string streamId = "multi-hop-ownership";
        const string firstOwnerEmail = "alice@example.org";
        const string secondOwnerEmail = "bea@example.org";
        const string currentOwnerEmail = "cora@example.org";
        var events = new[]
        {
            CreateAuditEvent(
                6,
                streamId,
                "RefTestStarted",
                new { firstName = "Cora", email = currentOwnerEmail },
                "Cora Current",
                currentOwnerEmail),
            CreateDetailsUpdatedAuditEvent(
                5,
                streamId,
                "Bea",
                "Cora",
                "Former",
                "Current",
                secondOwnerEmail,
                currentOwnerEmail),
            CreateAuditEvent(
                4,
                streamId,
                "RefTestStarted",
                new { firstName = "Bea", email = secondOwnerEmail },
                "Bea Former",
                secondOwnerEmail),
            CreateDetailsUpdatedAuditEvent(
                3,
                streamId,
                "Alice",
                "Bea",
                "Former",
                "Former",
                firstOwnerEmail,
                secondOwnerEmail),
            CreateAuditEvent(
                2,
                streamId,
                "RefTestStarted",
                new { firstName = "Alice", email = firstOwnerEmail },
                "Alice Former",
                firstOwnerEmail),
            CreateRefTestCreatedAuditEvent(1, streamId, "Alice", "Former", firstOwnerEmail)
        };

        var sanitized = PersonalDataExportAuditSanitizer.SanitizeHistory(events, currentOwnerEmail);

        Assert.Equal(
            new[] { "RefTestDetailsUpdated", "RefTestStarted" },
            sanitized.Select(auditEvent => auditEvent.Type));
        var transition = Assert.Single(sanitized, auditEvent => auditEvent.Type == "RefTestDetailsUpdated");
        using var transitionData = JsonDocument.Parse(transition.Data!);
        Assert.Equal("***", transitionData.RootElement.GetProperty("firstName").GetProperty("old").GetString());
        Assert.Equal("Cora", transitionData.RootElement.GetProperty("firstName").GetProperty("new").GetString());

        var serializedHistory = JsonSerializer.Serialize(sanitized);
        Assert.DoesNotContain("Alice", serializedHistory, StringComparison.Ordinal);
        Assert.DoesNotContain("Bea", serializedHistory, StringComparison.Ordinal);
        Assert.DoesNotContain(firstOwnerEmail, serializedHistory, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secondOwnerEmail, serializedHistory, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(currentOwnerEmail, serializedHistory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizeHistoryPreservesHistoryAcrossCaseAndWhitespaceOnlyEmailUpdates()
    {
        const string streamId = "case-and-whitespace-only-ownership";
        const string ownerEmail = "ada@example.org";
        var caseAndWhitespaceOnlyUpdate = CreateDetailsUpdatedAuditEvent(
            3,
            streamId,
            "Ada",
            "Ada Byron",
            "Lovelace",
            "Lovelace",
            "Ada@Example.org",
            " ADA@example.ORG ");
        var events = new[]
        {
            CreateAuditEvent(
                4,
                streamId,
                "RefTestCompleted",
                new { firstName = "Ada Byron", email = ownerEmail },
                "Ada Byron",
                ownerEmail),
            caseAndWhitespaceOnlyUpdate,
            CreateAuditEvent(
                2,
                streamId,
                "RefTestStarted",
                new { firstName = "Ada", email = ownerEmail },
                "Ada Lovelace",
                ownerEmail),
            CreateRefTestCreatedAuditEvent(1, streamId, "Ada", "Lovelace", "Ada@Example.org")
        };

        var sanitized = PersonalDataExportAuditSanitizer.SanitizeHistory(events, " Ada@Example.org ");

        Assert.Equal(4, sanitized.Count);
        var sanitizedUpdate = Assert.Single(
            sanitized,
            auditEvent => auditEvent.Type == "RefTestDetailsUpdated");
        using var updateData = JsonDocument.Parse(sanitizedUpdate.Data!);
        Assert.Equal("Ada", updateData.RootElement.GetProperty("firstName").GetProperty("old").GetString());
        Assert.Equal("Ada Byron", updateData.RootElement.GetProperty("firstName").GetProperty("new").GetString());
        Assert.Equal(
            "Ada@Example.org",
            updateData.RootElement.GetProperty("email").GetProperty("old").GetString());
    }

    [Fact]
    public void SanitizeHistoryOmitsUnknownOwnershipUntilAValidCurrentOwnerTransition()
    {
        const string streamId = "unknown-ownership";
        const string priorOwnerEmail = "alice@example.org";
        const string currentOwnerEmail = "bea@example.org";
        var events = new[]
        {
            CreateAuditEvent(
                6,
                streamId,
                "RefTestStarted",
                new { firstName = "Bea", email = currentOwnerEmail },
                "Bea Current",
                currentOwnerEmail),
            CreateDetailsUpdatedAuditEvent(
                5,
                streamId,
                "Alice",
                "Bea",
                "Former",
                "Current",
                priorOwnerEmail,
                currentOwnerEmail),
            CreateAuditEvent(4, streamId, "RefTestStarted", new { email = currentOwnerEmail }),
            new AuditEvent
            {
                SeqId = 3,
                StreamId = streamId,
                Version = 3,
                Type = "RefTestDetailsUpdated",
                Data = "{ malformed",
                Timestamp = Now
            },
            CreateAuditEvent(
                2,
                streamId,
                "RefTestStarted",
                new { firstName = "Bea", email = currentOwnerEmail },
                "Bea Current",
                currentOwnerEmail),
            new AuditEvent
            {
                SeqId = 1,
                StreamId = streamId,
                Version = 1,
                Type = "RefTestCreated",
                Data = "{ malformed",
                Timestamp = Now
            }
        };

        var sanitized = PersonalDataExportAuditSanitizer.SanitizeHistory(events, currentOwnerEmail);

        Assert.Equal(
            new[] { "RefTestDetailsUpdated", "RefTestStarted" },
            sanitized.Select(auditEvent => auditEvent.Type));
        var transition = Assert.Single(sanitized, auditEvent => auditEvent.Type == "RefTestDetailsUpdated");
        using var transitionData = JsonDocument.Parse(transition.Data!);
        Assert.Equal("***", transitionData.RootElement.GetProperty("firstName").GetProperty("old").GetString());
        Assert.Equal("Bea", transitionData.RootElement.GetProperty("firstName").GetProperty("new").GetString());
    }

    [Theory]
    [InlineData("firstName")]
    [InlineData("lastName")]
    public void SanitizeHistoryPreservesHistoryAcrossNameOnlyUpdatesForCurrentOwner(string changedField)
    {
        const string streamId = "name-only-current-owner";
        const string ownerEmail = "ada@example.org";
        const string oldFirstName = "Ada";
        const string oldLastName = "Lovelace";
        var newFirstName = changedField == "firstName" ? "Augusta" : oldFirstName;
        var newLastName = changedField == "lastName" ? "Byron" : oldLastName;
        var nameUpdate = CreateNameOnlyDetailsUpdatedAuditEvent(
            3,
            streamId,
            oldFirstName,
            newFirstName,
            oldLastName,
            newLastName,
            ownerEmail);
        var events = new[]
        {
            CreateAuditEvent(
                4,
                streamId,
                "RefTestCompleted",
                new { firstName = newFirstName, lastName = newLastName, email = ownerEmail },
                $"{newFirstName} {newLastName}",
                ownerEmail),
            nameUpdate,
            CreateAuditEvent(
                2,
                streamId,
                "RefTestStarted",
                new { firstName = oldFirstName, lastName = oldLastName, email = ownerEmail },
                $"{oldFirstName} {oldLastName}",
                ownerEmail),
            CreateRefTestCreatedAuditEvent(1, streamId, oldFirstName, oldLastName, ownerEmail)
        };

        var sanitized = PersonalDataExportAuditSanitizer.SanitizeHistory(events, ownerEmail);

        Assert.Equal(
            new[] { "RefTestCreated", "RefTestStarted", "RefTestDetailsUpdated", "RefTestCompleted" },
            sanitized.Select(auditEvent => auditEvent.Type));
        var sanitizedNameUpdate = Assert.Single(
            sanitized,
            auditEvent => auditEvent.Version == 3);
        using var nameUpdateData = JsonDocument.Parse(sanitizedNameUpdate.Data!);
        Assert.Equal(changedField, Assert.Single(nameUpdateData.RootElement.EnumerateObject()).Name);
        var changedName = nameUpdateData.RootElement.GetProperty(changedField);
        var expectedOldName = changedField == "firstName" ? oldFirstName : oldLastName;
        var expectedNewName = changedField == "firstName" ? newFirstName : newLastName;
        Assert.Equal(expectedOldName, changedName.GetProperty("old").GetString());
        Assert.Equal(expectedNewName, changedName.GetProperty("new").GetString());
        Assert.Contains(expectedOldName, JsonSerializer.Serialize(sanitized), StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeHistoryPreservesBothNameOnlyChangesForCurrentOwner()
    {
        const string streamId = "both-name-only-changes-current-owner";
        const string ownerEmail = "ada@example.org";
        const string oldFirstName = "Ada";
        const string newFirstName = "Augusta";
        const string oldLastName = "Lovelace";
        const string newLastName = "Byron";
        var nameUpdate = CreateNameOnlyDetailsUpdatedAuditEvent(
            2,
            streamId,
            oldFirstName,
            newFirstName,
            oldLastName,
            newLastName,
            ownerEmail);
        var events = new[]
        {
            CreateAuditEvent(
                3,
                streamId,
                "RefTestCompleted",
                new { firstName = newFirstName, lastName = newLastName, email = ownerEmail },
                $"{newFirstName} {newLastName}",
                ownerEmail),
            nameUpdate,
            CreateAuditEvent(
                1,
                streamId,
                "RefTestStarted",
                new { firstName = oldFirstName, lastName = oldLastName, email = ownerEmail },
                $"{oldFirstName} {oldLastName}",
                ownerEmail)
        };

        var sanitized = PersonalDataExportAuditSanitizer.SanitizeHistory(events, ownerEmail);

        Assert.Equal(
            new[] { "RefTestStarted", "RefTestDetailsUpdated", "RefTestCompleted" },
            sanitized.Select(auditEvent => auditEvent.Type));
        var sanitizedNameUpdate = Assert.Single(sanitized, auditEvent => auditEvent.Version == 2);
        using var nameUpdateData = JsonDocument.Parse(sanitizedNameUpdate.Data!);
        Assert.Equal(
            new[] { "firstName", "lastName" },
            nameUpdateData.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            oldFirstName,
            nameUpdateData.RootElement.GetProperty("firstName").GetProperty("old").GetString());
        Assert.Equal(
            oldLastName,
            nameUpdateData.RootElement.GetProperty("lastName").GetProperty("old").GetString());

        var serializedExport = JsonSerializer.Serialize(sanitized);
        Assert.Contains(oldFirstName, serializedExport, StringComparison.Ordinal);
        Assert.Contains(oldLastName, serializedExport, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeHistoryPreservesNewOwnersNameOnlyHistoryAfterEmailTransferWithoutPriorOwnerData()
    {
        const string streamId = "transfer-followed-by-name-only-update";
        const string priorOwnerEmail = "alice@example.org";
        const string currentOwnerEmail = "bea@example.org";
        var nameUpdate = CreateNameOnlyDetailsUpdatedAuditEvent(
            4,
            streamId,
            "Bea",
            "Beatrice",
            "Current",
            "Current",
            currentOwnerEmail);
        var events = new[]
        {
            CreateAuditEvent(
                5,
                streamId,
                "RefTestCompleted",
                new { firstName = "Beatrice", lastName = "Current", email = currentOwnerEmail },
                "Beatrice Current",
                currentOwnerEmail),
            nameUpdate,
            CreateDetailsUpdatedAuditEvent(
                3,
                streamId,
                "Alice",
                "Bea",
                "Former",
                "Current",
                priorOwnerEmail,
                currentOwnerEmail),
            CreateAuditEvent(
                2,
                streamId,
                "RefTestStarted",
                new { firstName = "Alice", lastName = "Former", email = priorOwnerEmail },
                "Alice Former",
                priorOwnerEmail),
            CreateRefTestCreatedAuditEvent(1, streamId, "Alice", "Former", priorOwnerEmail)
        };

        var sanitized = PersonalDataExportAuditSanitizer.SanitizeHistory(events, currentOwnerEmail);

        Assert.Equal(
            new[] { "RefTestDetailsUpdated", "RefTestDetailsUpdated", "RefTestCompleted" },
            sanitized.Select(auditEvent => auditEvent.Type));
        var sanitizedNameUpdate = Assert.Single(sanitized, auditEvent => auditEvent.Version == 4);
        using (var nameUpdateData = JsonDocument.Parse(sanitizedNameUpdate.Data!))
        {
            var firstNameChange = nameUpdateData.RootElement.GetProperty("firstName");
            Assert.Equal("Bea", firstNameChange.GetProperty("old").GetString());
            Assert.Equal("Beatrice", firstNameChange.GetProperty("new").GetString());
            Assert.False(nameUpdateData.RootElement.TryGetProperty("email", out _));
        }

        var sanitizedTransfer = Assert.Single(sanitized, auditEvent => auditEvent.Version == 3);
        using (var transferData = JsonDocument.Parse(sanitizedTransfer.Data!))
        {
            Assert.Equal(
                "***",
                transferData.RootElement.GetProperty("firstName").GetProperty("old").GetString());
            Assert.Equal(
                "***",
                transferData.RootElement.GetProperty("lastName").GetProperty("old").GetString());
            Assert.Equal(
                "***",
                transferData.RootElement.GetProperty("email").GetProperty("old").GetString());
            Assert.Equal(
                currentOwnerEmail,
                transferData.RootElement.GetProperty("email").GetProperty("new").GetString());
        }

        var serializedHistory = JsonSerializer.Serialize(sanitized);
        Assert.DoesNotContain("Alice", serializedHistory, StringComparison.Ordinal);
        Assert.DoesNotContain("Former", serializedHistory, StringComparison.Ordinal);
        Assert.DoesNotContain(priorOwnerEmail, serializedHistory, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(currentOwnerEmail, serializedHistory, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("""{"firstName":{"old":"Ada","new":"Augusta"},"displayName":"Ada Augusta"}""")]
    [InlineData("""{"firstName":{"old":"Ada","new":42}}""")]
    [InlineData("""{"firstName":{"old":"Ada","new":"Augusta"},"lastName":{"old":"Lovelace","new":42}}""")]
    [InlineData("""{"firstName":{"old":"Ada","oldValue":"Alice","new":"Augusta"}}""")]
    [InlineData("""{"firstName":{"old":"Ada","new":"Augusta"},"email":{"old":"ada@example.org","new":42}}""")]
    [InlineData("""{"firstName":{"old":"Ada","new":"Augusta"},"email":{"old":"ada@example.org","new":"not-an-email"}}""")]
    [InlineData("""{"firstName":{"old":"***","new":"***"},"lastName":{"old":"***","new":"***"},"email":{"old":"***","new":"***"}}""")]
    [InlineData("""{"firstName":{"old":"Ada","new":"Augusta"},"middleName":{"old":"Byron","new":"King"}}""")]
    [InlineData("""{"firstName":{"old":"Ada","new":"Augusta"},"email":null}""")]
    [InlineData("""{"firstName":{"old":"Ada","new":"Augusta"},"email":"ada@example.org"}""")]
    [InlineData("""{"firstName":{"old":"Ada","new":"Augusta"},"email":{"old":42,"new":null}}""")]
    [InlineData("{ malformed")]
    public void SanitizeHistoryDoesNotInferOwnershipFromUnknownOrMalformedDetailsUpdates(
        string? invalidDetailsData)
    {
        const string streamId = "invalid-name-only-update";
        const string ownerEmail = "ada@example.org";
        var events = new[]
        {
            CreateAuditEvent(
                4,
                streamId,
                "RefTestCompleted",
                new { firstName = "Augusta", lastName = "Lovelace", email = ownerEmail },
                "Augusta Lovelace",
                ownerEmail),
            CreateAuditEventWithRawData(3, streamId, "RefTestDetailsUpdated", invalidDetailsData),
            CreateNameOnlyDetailsUpdatedAuditEvent(
                2,
                streamId,
                "Ada",
                "Augusta",
                "Lovelace",
                "Lovelace",
                ownerEmail),
            CreateAuditEvent(
                1,
                streamId,
                "RefTestStarted",
                new { firstName = "Ada", lastName = "Lovelace", email = ownerEmail },
                "Ada Lovelace",
                ownerEmail)
        };

        var sanitized = PersonalDataExportAuditSanitizer.SanitizeHistory(events, ownerEmail);

        Assert.Equal("RefTestCompleted", Assert.Single(sanitized).Type);
    }

    [Fact]
    public void SanitizeHistoryFailsClosedAfterRedactedNameOnlyUpdate()
    {
        const string streamId = "redacted-name-only-update";
        const string ownerEmail = "ada@example.org";
        var redactedNameUpdate = CreateRedactedAuditEvent(
            CreateNameOnlyDetailsUpdatedAuditEvent(
                3,
                streamId,
                "VerifiedNameBefore",
                "VerifiedNameAfter",
                "Lovelace",
                "Lovelace",
                ownerEmail));
        var events = new[]
        {
            CreateAuditEvent(
                4,
                streamId,
                "RefTestCompleted",
                new { firstName = "Latest", lastName = "Lovelace", email = ownerEmail },
                "Latest Lovelace",
                ownerEmail),
            redactedNameUpdate,
            CreateNameOnlyDetailsUpdatedAuditEvent(
                2,
                streamId,
                "EarlierName",
                "OlderName",
                "Lovelace",
                "Lovelace",
                ownerEmail),
            CreateAuditEvent(
                1,
                streamId,
                "RefTestStarted",
                new { firstName = "EarlierActivity", lastName = "Lovelace", email = ownerEmail },
                "EarlierActivity Lovelace",
                ownerEmail)
        };

        var sanitized = PersonalDataExportAuditSanitizer.SanitizeHistory(events, ownerEmail);

        Assert.Equal(
            new[] { "RefTestDetailsUpdated", "RefTestCompleted" },
            sanitized.Select(auditEvent => auditEvent.Type));
        var sanitizedRedactedUpdate = Assert.Single(
            sanitized,
            auditEvent => auditEvent.Version == 3);
        Assert.Equal(PersonalDataExportActorKind.Redacted, sanitizedRedactedUpdate.ActorKind);
        using (var redactedData = JsonDocument.Parse(sanitizedRedactedUpdate.Data!))
        {
            Assert.Equal(
                "***",
                redactedData.RootElement.GetProperty("firstName").GetProperty("old").GetString());
            Assert.Equal(
                "***",
                redactedData.RootElement.GetProperty("firstName").GetProperty("new").GetString());
        }

        var serializedHistory = JsonSerializer.Serialize(sanitized);
        Assert.DoesNotContain("VerifiedNameBefore", serializedHistory, StringComparison.Ordinal);
        Assert.DoesNotContain("VerifiedNameAfter", serializedHistory, StringComparison.Ordinal);
        Assert.DoesNotContain("EarlierName", serializedHistory, StringComparison.Ordinal);
        Assert.DoesNotContain("EarlierActivity", serializedHistory, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmailChangeDuringEmailPreparationPreventsStaleOwnerHandoff()
    {
        using var database = SqliteTestDatabase.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var request = CreateVerifiedRequest(ParticipantEmail);
        Guid refTestId;

        await using (var seed = database.CreateContext())
        {
            var title = RefTestTitle.Create("Email Change During Delivery");
            seed.RefTestTitles.Add(title);
            await seed.SaveChangesAsync(cancellationToken);

            var refTest = CreateRefTest(title.Id, ParticipantEmail, "Ada", "Lovelace");
            seed.RefTests.Add(refTest);
            seed.PersonalDataExportRequests.Add(request);
            await seed.SaveChangesAsync(cancellationToken);
            refTestId = refTest.Id;
        }

        var emailService = new RecordingEmailService
        {
            PrepareEmailAsync = async preparationCancellationToken =>
            {
                await using var updateContext = database.CreateContext();
                var refTest = await updateContext.RefTests.SingleAsync(
                    candidate => candidate.Id == refTestId,
                    preparationCancellationToken);
                refTest.UpdateBasicDetails("Ada", "Lovelace", "new-owner@example.org");
                await updateContext.SaveChangesAsync(preparationCancellationToken);
            }
        };

        await using (var deliveryContext = database.CreateContext())
        {
            await CreateHandler(
                    deliveryContext,
                    new RecordingPdfService(),
                    emailService,
                    new BackgroundJobConfiguration(),
                    NullLoggerFactory.Instance)
                .HandleAsync(DeliveryJob(request.Id), cancellationToken);
        }

        Assert.Equal(1, emailService.PreparationCount);
        Assert.Equal(1, emailService.FinalDeliverabilityCheckCount);
        Assert.Empty(emailService.Recipients);

        await using var verification = database.CreateContext();
        var persistedRequest = await verification.PersonalDataExportRequests
            .SingleAsync(candidate => candidate.Id == request.Id, cancellationToken);
        Assert.Equal(ParticipantEmail, persistedRequest.Email);
        Assert.False(await verification.AuditEvents.AnyAsync(
            auditEvent => auditEvent.StreamId == request.Id.ToString()
                          && auditEvent.Type == PersonalDataExportDeliveredEvent.EventType,
            cancellationToken));
    }

    [Fact]
    public async Task ErasureDuringEmailPreparationPreventsProviderHandoffAndDeliverySuccess()
    {
        using var database = SqliteTestDatabase.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var request = CreateVerifiedRequest(ParticipantEmail);
        Guid refTestId;

        await using (var seed = database.CreateContext())
        {
            var title = RefTestTitle.Create("Erasure During Delivery");
            seed.RefTestTitles.Add(title);
            await seed.SaveChangesAsync(cancellationToken);

            var refTest = CreateRefTest(title.Id, ParticipantEmail, "Ada", "Lovelace");
            seed.RefTests.Add(refTest);
            seed.PersonalDataExportRequests.Add(request);
            await seed.SaveChangesAsync(cancellationToken);
            refTestId = refTest.Id;
        }

        var staffContext = StaffHttpContextAccessor();
        var emailService = new RecordingEmailService
        {
            PrepareEmailAsync = async preparationCancellationToken =>
            {
                await using var erasureContext = database.CreateContext(
                    new AuditSaveChangesInterceptor(staffContext, new AuditLogOptions()));
                var refTest = await erasureContext.RefTests.SingleAsync(
                    candidate => candidate.Id == refTestId,
                    preparationCancellationToken);
                await new RefTestPrivacyErasureService(erasureContext).EraseAsync(
                    refTest,
                    ErasureInitiator.Operator,
                    preparationCancellationToken);
            }
        };

        await using (var deliveryContext = database.CreateContext(
                         new AuditSaveChangesInterceptor(staffContext, new AuditLogOptions())))
        {
            await CreateHandler(
                    deliveryContext,
                    new RecordingPdfService(),
                    emailService,
                    new BackgroundJobConfiguration(),
                    NullLoggerFactory.Instance)
                .HandleAsync(DeliveryJob(request.Id), cancellationToken);
        }

        Assert.Equal(1, emailService.PreparationCount);
        Assert.Equal(1, emailService.FinalDeliverabilityCheckCount);
        Assert.Single(emailService.Attachments);
        Assert.Empty(emailService.Recipients);

        await using var verification = database.CreateContext();
        var erasedRequest = await verification.PersonalDataExportRequests
            .SingleAsync(candidate => candidate.Id == request.Id, cancellationToken);
        Assert.Equal(string.Empty, erasedRequest.Email);
        Assert.Null(erasedRequest.LastDeliveryAttemptAt);
        Assert.Equal(0, erasedRequest.DeliveryAttemptCount);
        Assert.False(await verification.AuditEvents.AnyAsync(
            auditEvent => auditEvent.StreamId == request.Id.ToString()
                          && auditEvent.Type == PersonalDataExportDeliveredEvent.EventType,
            cancellationToken));
    }

    [Fact]
    public async Task RetryRecordsFailureThenDeliveryAndACompletedDuplicateDoesNotResend()
    {
        using var database = SqliteTestDatabase.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var request = CreateVerifiedRequest(ParticipantEmail);

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
    public async Task TerminalFailureIsAuditedAndClearsRecipient()
    {
        using var database = SqliteTestDatabase.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        const string recipientEmail = "bea@example.org";
        var request = CreateVerifiedRequest(recipientEmail);

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
        var request = CreateVerifiedRequest(recipientEmail);
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
    public void PdfAuditDetailsAreLocalizedAndPreserveEveryValue()
    {
        var translations = new TranslationService();
        var data = """
                   {"firstName":{"old":"Ada","new":"Grace"},"email":{"oldValue":"ada@example.org","newValue":"grace@example.org"},"startReason":"participant","unmapped":{"nested":{"value":42}},"emptyObject":{},"items":[1,{"retained":true}],"nothing":null,"emptyString":"","multiline":"line\nbreak"}
                   """;
        var locales = new[]
        {
            ("en", "First name", "Email address", "Previous value", "New value", "Start reason"),
            ("nl", "Voornaam", "E-mailadres", "Oude waarde", "Nieuwe waarde", "Startreden"),
            ("fr", "Prénom", "Adresse e-mail", "Valeur précédente", "Nouvelle valeur", "Motif du démarrage"),
            ("de", "Vorname", "E-Mail-Adresse", "Vorheriger Wert", "Neuer Wert", "Startgrund")
        };

        foreach (var (locale, firstName, email, oldValue, newValue, startReason) in locales)
        {
            var details = PersonalDataExportPdfService.FormatAuditDetails(
                data,
                translations.GetPdfPersonalDataExportTranslations(locale));

            Assert.Contains(($"{firstName} — {oldValue}", "Ada"), details);
            Assert.Contains(($"{firstName} — {newValue}", "Grace"), details);
            Assert.Contains(($"{email} — {oldValue}", "ada@example.org"), details);
            Assert.Contains(($"{email} — {newValue}", "grace@example.org"), details);
            Assert.Contains((startReason, "participant"), details);
            Assert.Contains(("unmapped — nested — value", "42"), details);
            Assert.Contains(("emptyObject", "{}"), details);
            Assert.Contains(("items", """[1,{"retained":true}]"""), details);
            Assert.Contains(("nothing", "null"), details);
            Assert.Contains(("emptyString", "\"\""), details);
            Assert.Contains(("multiline", JsonSerializer.Serialize("line\nbreak")), details);
        }
    }

    [Fact]
    public void PdfAuditDetailsUseSafeFallbackForNonObjectAndMalformedData()
    {
        var translations = new TranslationService().GetPdfPersonalDataExportTranslations("en");
        const string arrayData = """[{"unrecognized":"still visible"},2]""";
        const string malformedData = " {broken json \r\n";

        foreach (var data in new[]
                 {
                     arrayData,
                     "\"private scalar details\"",
                     "42",
                     "true",
                     "null",
                     malformedData
                 })
        {
            Assert.Equal(
                (null, translations["noDetails"]),
                Assert.Single(PersonalDataExportPdfService.FormatAuditDetails(data, translations)));
        }

        Assert.Equal(
            (null, translations["noDetails"]),
            Assert.Single(PersonalDataExportPdfService.FormatAuditDetails(null, translations)));
        Assert.Equal(
            (null, translations["noDetails"]),
            Assert.Single(PersonalDataExportPdfService.FormatAuditDetails("{}", translations)));
    }

    [Fact]
    public void PdfAuditDetailsRenderSanitizedObjectsAndSuppressMalformedOrScalarPayloads()
    {
        const string participantEmail = "ada@example.org";
        var translations = new TranslationService().GetPdfPersonalDataExportTranslations("en");
        var invalidPayloads = new[]
        {
            ("malformed", "{ malformed"),
            ("string", "\"private scalar details\""),
            ("number", "42"),
            ("boolean", "true"),
            ("null", "null")
        };
        var invalidEvents = invalidPayloads
            .Select((payload, index) => CreateAuditEventWithRawData(
                index + 1,
                $"invalid-{payload.Item1}",
                "RefTestCompleted",
                payload.Item2))
            .ToArray();

        var sanitizedInvalidEvents = PersonalDataExportAuditSanitizer.SanitizeHistory(
            invalidEvents,
            participantEmail);

        Assert.Equal(invalidPayloads.Length, sanitizedInvalidEvents.Count);
        Assert.All(sanitizedInvalidEvents, auditEvent =>
        {
            Assert.Null(auditEvent.Data);
            Assert.Equal(
                (null, translations["noDetails"]),
                Assert.Single(PersonalDataExportPdfService.FormatAuditDetails(auditEvent.Data, translations)));
        });

        const string validObjectData =
            """{"unmapped":{"nested":{"value":"retained value"}},"secret":"private secret"}""";
        var sanitizedObjectEvent = Assert.Single(PersonalDataExportAuditSanitizer.SanitizeHistory(
            [
                CreateAuditEventWithRawData(
                    99,
                    "sanitized-object",
                    "RefTestCompleted",
                    validObjectData)
            ],
            participantEmail));

        var sanitizedObjectData = Assert.IsType<string>(sanitizedObjectEvent.Data);
        Assert.DoesNotContain("private secret", sanitizedObjectData, StringComparison.Ordinal);
        var details = PersonalDataExportPdfService.FormatAuditDetails(sanitizedObjectData, translations);
        Assert.Contains(("unmapped — nested — value", "retained value"), details);
    }

    [Fact]
    public void OversizedSingleRefTestSplitsEventsIntoAContinuationWithoutRepeatingFields()
    {
        var streamId = Guid.NewGuid().ToString();
        var refTest = new PersonalDataExportRefTestData(
            Guid.Parse(streamId),
            "Ada",
            "Lovelace",
            ParticipantEmail,
            NumberOfQuestions: 12,
            MaxTimeInMinutes: 30,
            CreatedAt: Now,
            StartedAt: null,
            CompletedAt: null,
            ExpiredAt: null,
            QuestionScore: null,
            AnswerScore: null,
            AnswerTotal: null,
            Percentage: null,
            Language: "en",
            PrivacyNoticeVersion: "v2",
            PrivacyNoticeAcceptedAt: null,
            ScheduledAt: null);
        var auditEvents = Enumerable.Range(1, 4)
            .Select(version => new PersonalDataExportAuditEventData(
                streamId,
                version,
                "RefTestDetailsUpdated",
                Now.AddMinutes(version),
                PersonalDataExportActorKind.System,
                string.Empty,
                string.Empty,
                JsonSerializer.Serialize(new { version }),
                IsArchived: false,
                RedactedAt: null))
            .ToList();
        var section = new PersonalDataExportPdfService.PdfSection(
            refTest,
            auditEvents,
            IncludeStoredFields: true,
            IsContinuation: false);

        Assert.True(PersonalDataExportPdfService.TrySplit(
            [section],
            out var firstPart,
            out var continuation));

        var firstSection = Assert.Single(firstPart);
        Assert.Same(refTest, firstSection.RefTest);
        Assert.Equal(auditEvents.Take(2), firstSection.AuditEvents);
        Assert.True(firstSection.IncludeStoredFields);
        Assert.False(firstSection.IsContinuation);

        var continuationSection = Assert.Single(continuation);
        Assert.Same(refTest, continuationSection.RefTest);
        Assert.Equal(auditEvents.Skip(2), continuationSection.AuditEvents);
        Assert.False(continuationSection.IncludeStoredFields);
        Assert.True(continuationSection.IsContinuation);
    }

    [Fact]
    public async Task PersonalDataExportPdfGeneratesInEverySupportedLocale()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var streamId = Guid.NewGuid().ToString();
        var document = new PersonalDataExportDocumentData(
            ParticipantEmail,
            [
                new PersonalDataExportRefTestData(
                    Guid.Parse(streamId),
                    "Ada",
                    "Lovelace",
                    ParticipantEmail,
                    NumberOfQuestions: 12,
                    MaxTimeInMinutes: 30,
                    CreatedAt: Now,
                    StartedAt: Now.AddMinutes(1),
                    CompletedAt: null,
                    ExpiredAt: null,
                    QuestionScore: null,
                    AnswerScore: null,
                    AnswerTotal: null,
                    Percentage: null,
                    Language: "en",
                    PrivacyNoticeVersion: "v2",
                    PrivacyNoticeAcceptedAt: Now,
                    ScheduledAt: null)
            ],
            [
                new PersonalDataExportAuditEventData(
                    streamId,
                    1,
                    "RefTestCreated",
                    Now,
                    PersonalDataExportActorKind.System,
                    string.Empty,
                    string.Empty,
                    """{"firstName":"Ada","customField":"preserved"}""",
                    IsArchived: false,
                    RedactedAt: null)
            ]);
        var translations = new TranslationService();
        var logoService = new NullLogoService();

        foreach (var locale in new[] { "en", "nl", "fr", "de" })
        {
            var attachments = await new PersonalDataExportPdfService(
                    translations,
                    new LanguageConfiguration
                    {
                        DefaultPhraseLanguage = "en",
                        EnabledLanguages = [locale]
                    },
                    logoService)
                .GenerateAttachmentsAsync(document);

            var pdf = Assert.Single(attachments).Content;
            Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        }
    }

    [Fact]
    public async Task PdfPartsAreMultilingualValidAndNumberedInsteadOfTruncated()
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
            [refTest],
            eventData);

        var languages = LanguageConfiguration.CreateDefault();
        var logoService = new NullLogoService();
        var fullExport = await new PersonalDataExportPdfService(
                new TranslationService(),
                languages,
                logoService)
            .GenerateAttachmentsAsync(document);
        var oneEventExport = await new PersonalDataExportPdfService(
                new TranslationService(),
                languages,
                logoService)
            .GenerateAttachmentsAsync(document with { AuditEvents = [eventData[0]] });
        var testPartLimit = Assert.Single(oneEventExport).Content.Length * 3;
        Assert.True(Assert.Single(fullExport).Content.Length > testPartLimit);

        var service = new PersonalDataExportPdfService(
            new TranslationService(),
            languages,
            logoService,
            testPartLimit);
        var attachments = await service.GenerateAttachmentsAsync(document);

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

        var english = new PersonalDataExportPdfService(
            new TranslationService(),
            new LanguageConfiguration { DefaultPhraseLanguage = "en", EnabledLanguages = ["en"] },
            logoService)
            .GenerateAttachmentsAsync(document);
        var french = new PersonalDataExportPdfService(
            new TranslationService(),
            new LanguageConfiguration { DefaultPhraseLanguage = "en", EnabledLanguages = ["fr"] },
            logoService)
            .GenerateAttachmentsAsync(document);
        var englishAttachments = await english;
        var frenchAttachments = await french;
        var englishPdf = Assert.Single(englishAttachments).Content;
        var frenchPdf = Assert.Single(frenchAttachments).Content;
        var multilingualPdf = Assert.Single(fullExport).Content;
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(englishPdf, 0, 5));
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(frenchPdf, 0, 5));
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(multilingualPdf, 0, 5));
        Assert.True(multilingualPdf.Length > englishPdf.Length);
        Assert.True(multilingualPdf.Length > frenchPdf.Length);
        Assert.NotEqual(
            Convert.ToBase64String(englishPdf),
            Convert.ToBase64String(frenchPdf));
    }

    private static PersonalDataExportRequest CreateVerifiedRequest(string email)
    {
        var request = PersonalDataExportRequest.Create(
            email,
            ChallengeKey,
            $"protected:{ChallengeKey}",
            Now,
            Now.AddHours(24));
        Assert.True(request.TryConfirm(ChallengeKey, Now.AddMinutes(1)));
        return request;
    }

    private static AuditEvent CreateAuditEvent(
        long seqId,
        string streamId,
        string type,
        object? data,
        string actorName = "System",
        string actorEmail = "") =>
        new()
        {
            SeqId = seqId,
            StreamId = streamId,
            Version = seqId,
            Type = type,
            Timestamp = Now.AddSeconds(seqId),
            ActorName = actorName,
            ActorEmail = actorEmail,
            Data = data is null
                ? null
                : JsonSerializer.Serialize(
                    data,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
        };

    private static AuditEvent CreateAuditEventWithRawData(
        long seqId,
        string streamId,
        string type,
        string? data) =>
        new()
        {
            SeqId = seqId,
            StreamId = streamId,
            Version = seqId,
            Type = type,
            Timestamp = Now.AddSeconds(seqId),
            ActorName = "System",
            ActorEmail = string.Empty,
            Data = data
        };

    private static AuditEvent CreateRefTestCreatedAuditEvent(
        long seqId,
        string streamId,
        string firstName,
        string lastName,
        string email) =>
        CreateAuditEvent(
            seqId,
            streamId,
            "RefTestCreated",
            new { firstName, lastName, email },
            "Staff Operator",
            "staff@example.org");

    private static AuditEvent CreateRedactedAuditEvent(AuditEvent source) =>
        new()
        {
            Id = source.Id,
            SeqId = source.SeqId,
            StreamId = source.StreamId,
            Version = source.Version,
            Type = source.Type,
            Timestamp = source.Timestamp,
            ActorName = AuditPiiRedactor.RedactedValue,
            ActorEmail = AuditPiiRedactor.RedactedValue,
            Headers = source.Headers,
            Data = AuditPiiRedactor.RedactData(source.Data),
            IsArchived = true,
            RedactedAt = source.Timestamp.AddDays(91)
        };

    private static AuditEvent CreateDetailsUpdatedAuditEvent(
        long seqId,
        string streamId,
        string oldFirstName,
        string newFirstName,
        string oldLastName,
        string newLastName,
        string oldEmail,
        string newEmail) =>
        CreateAuditEvent(
            seqId,
            streamId,
            "RefTestDetailsUpdated",
            new
            {
                firstName = new { old = oldFirstName, @new = newFirstName },
                lastName = new { old = oldLastName, @new = newLastName },
                email = new { old = oldEmail, @new = newEmail }
            },
            "Staff Operator",
            "staff@example.org");

    private static AuditEvent CreateNameOnlyDetailsUpdatedAuditEvent(
        long seqId,
        string streamId,
        string oldFirstName,
        string newFirstName,
        string oldLastName,
        string newLastName,
        string ownerEmail) =>
        CreateAuditEvent(
            seqId,
            streamId,
            "RefTestDetailsUpdated",
            new RefTestDetailsUpdatedEvent(
                oldFirstName,
                newFirstName,
                oldLastName,
                newLastName,
                ownerEmail,
                ownerEmail).GetChanges(),
            "Staff Operator",
            "staff@example.org");

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

        public Task<IReadOnlyList<EmailAttachment>> GenerateAttachmentsAsync(
            PersonalDataExportDocumentData document)
        {
            Document = document;
            if (Failure is not null)
                throw Failure;
            return Task.FromResult<IReadOnlyList<EmailAttachment>>(
                [new EmailAttachment("PersonalDataExport.pdf", [0x25, 0x50, 0x44, 0x46])]);
        }
    }

    private sealed class NullLogoService : ILogoService
    {
        public Task<byte[]?> GetLogoBytesAsync() => Task.FromResult<byte[]?>(null);
        public Task<string> GetLogoAsBase64Async() => Task.FromResult(string.Empty);
    }

    private sealed class RecordingEmailService : IEmailService
    {
        public Exception? Failure { get; set; }
        public List<string> Recipients { get; } = [];
        public List<IReadOnlyList<EmailAttachment>> Attachments { get; } = [];
        public Func<CancellationToken, Task>? PrepareEmailAsync { get; set; }
        public int PreparationCount { get; private set; }
        public int FinalDeliverabilityCheckCount { get; private set; }

        public async Task<bool> SendPersonalDataExportAsync(
            string recipientEmail,
            IReadOnlyList<EmailAttachment> attachments,
            Func<CancellationToken, Task<bool>> finalDeliverabilityCheck,
            CancellationToken cancellationToken)
        {
            Attachments.Add(attachments);
            if (PrepareEmailAsync is not null)
            {
                PreparationCount++;
                await PrepareEmailAsync(cancellationToken);
            }

            FinalDeliverabilityCheckCount++;
            if (!await finalDeliverabilityCheck(cancellationToken))
                return false;

            Recipients.Add(recipientEmail);
            if (Failure is not null)
                throw Failure;

            return true;
        }

        public Task<bool> SendRefTestInvitationAsync(
            Guid refTestId,
            string name,
            string email,
            string token,
            int numberOfQuestions,
            int maxTimeInMinutes,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> SendRefTestResultsAsync(
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

        public Task<bool> SendPersonalDataExportVerificationAsync(
            string recipientEmail,
            string challengeKey,
            DateTime expiresAt,
            Func<CancellationToken, Task<bool>> finalDeliverabilityCheck,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> SendPrivacyWithdrawalVerificationAsync(
            string recipientEmail,
            string challengeKey,
            DateTime expiresAt,
            Func<CancellationToken, Task<bool>> finalDeliverabilityCheck,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
