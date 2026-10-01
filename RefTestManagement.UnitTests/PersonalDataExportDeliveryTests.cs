using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
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
    public void PdfPartsAreMultilingualValidAndNumberedInsteadOfTruncated()
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
        var fullExport = new PersonalDataExportPdfService(new TranslationService(), languages)
            .GenerateAttachments(document);
        var oneEventExport = new PersonalDataExportPdfService(new TranslationService(), languages)
            .GenerateAttachments(document with { AuditEvents = [eventData[0]] });
        var testPartLimit = Assert.Single(oneEventExport).Content.Length * 3;
        Assert.True(Assert.Single(fullExport).Content.Length > testPartLimit);

        var service = new PersonalDataExportPdfService(
            new TranslationService(),
            languages,
            testPartLimit);
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

        var english = new PersonalDataExportPdfService(
            new TranslationService(),
            new LanguageConfiguration { DefaultPhraseLanguage = "en", EnabledLanguages = ["en"] })
            .GenerateAttachments(document);
        var french = new PersonalDataExportPdfService(
            new TranslationService(),
            new LanguageConfiguration { DefaultPhraseLanguage = "en", EnabledLanguages = ["fr"] })
            .GenerateAttachments(document);
        var englishPdf = Assert.Single(english).Content;
        var frenchPdf = Assert.Single(french).Content;
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
