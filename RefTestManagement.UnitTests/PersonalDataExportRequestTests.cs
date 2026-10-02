using System.Security.Claims;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Privacy;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Handball.Belgium.RefTestManagement.Domain.Privacy.Events;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class PersonalDataExportRequestTests
{
    private const string ParticipantEmail = "ada@example.org";
    private static readonly string ChallengeKey = new('A', 43);
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private static JobEnqueueService NewJobEnqueueService(RefTestManagementContext context) =>
        new(
            context,
            new RefTestInvitationTokenProtection(new EphemeralDataProtectionProvider()),
            NullLogger<JobEnqueueService>.Instance);

    private static PersonalDataExportRequest NewRequest(
        DateTime createdAt,
        DateTime expiresAt,
        string email = ParticipantEmail,
        string key = "challenge-key",
        string protectedKey = "protected-challenge-key") =>
        PersonalDataExportRequest.Create(email, key, protectedKey, createdAt, expiresAt);

    [Fact]
    public void ChallengeKeyIsHashedAndConfirmationConsumesTheChallengeOnce()
    {
        var key = ChallengeKey;
        var request = NewRequest(Now, Now.AddHours(24), key: key);
        var expectedHash = PersonalDataExportRequest.HashKey(key);
        var deliveredAt = Now.AddMinutes(1);
        var confirmedAt = Now.AddMinutes(2);

        Assert.Equal(64, expectedHash.Length);
        Assert.NotEqual(key, expectedHash);
        Assert.Equal(expectedHash, request.KeyHash);
        Assert.NotNull(request.ProtectedDeliveryKey);
        Assert.True(request.MarkChallengeEmailDelivered(deliveredAt));
        Assert.Null(request.ProtectedDeliveryKey);

        Assert.True(request.TryConfirm(key, confirmedAt));
        Assert.False(request.TryConfirm(key, confirmedAt.AddSeconds(1)));
        Assert.Equal(confirmedAt, request.VerifiedAt);
        Assert.Null(request.KeyHash);
        Assert.Null(request.ProtectedDeliveryKey);
        Assert.Null(request.ChallengeEmailSentAt);
        Assert.Equal(ParticipantEmail, request.Email);
        Assert.Equal(2, request.DomainEvents.Count);

        var verifiedEventData = JsonSerializer.Serialize(request.DomainEvents.Last().GetChanges());
        Assert.Contains("mailboxVerified", verifiedEventData, StringComparison.Ordinal);
        Assert.DoesNotContain(ParticipantEmail, verifiedEventData, StringComparison.Ordinal);
        Assert.DoesNotContain(key, verifiedEventData, StringComparison.Ordinal);
        Assert.DoesNotContain(expectedHash, verifiedEventData, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpiredChallengeCannotBeConfirmedAndCleanupClearsRecipientAndSecretState()
    {
        var expiration = Now.AddHours(24);
        var request = NewRequest(Now, expiration, key: ChallengeKey);

        Assert.False(request.TryConfirm(ChallengeKey, expiration));
        Assert.True(request.ClearExpiredChallenge(expiration));
        Assert.False(request.ClearExpiredChallenge(expiration.AddMinutes(1)));
        Assert.Equal(string.Empty, request.Email);
        Assert.Null(request.KeyHash);
        Assert.Null(request.ProtectedDeliveryKey);
        Assert.Empty(request.DomainEvents);
    }

    [Fact]
    public async Task CompetingConfirmationsHaveOnlyOneSuccessfulDatabaseWrite()
    {
        using var database = SqliteTestDatabase.Create();
        Guid requestId;
        var confirmedAt = DateTime.UtcNow.AddSeconds(1);

        await using (var seed = database.CreateContext())
        {
            var createdAt = confirmedAt.AddMinutes(-1);
            var request = NewRequest(createdAt, createdAt.AddHours(24), key: ChallengeKey);
            seed.PersonalDataExportRequests.Add(request);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
            requestId = request.Id;
        }

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var first = await firstContext.PersonalDataExportRequests.SingleAsync(
            request => request.Id == requestId, TestContext.Current.CancellationToken);
        var second = await secondContext.PersonalDataExportRequests.SingleAsync(
            request => request.Id == requestId, TestContext.Current.CancellationToken);

        Assert.True(first.TryConfirm(ChallengeKey, confirmedAt));
        Assert.True(second.TryConfirm(ChallengeKey, confirmedAt));
        await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));

        await using var verification = database.CreateContext();
        var persisted = await verification.PersonalDataExportRequests.SingleAsync(
            request => request.Id == requestId, TestContext.Current.CancellationToken);
        Assert.Equal(confirmedAt, persisted.VerifiedAt);
        Assert.Null(persisted.KeyHash);
    }

    [Fact]
    public async Task RequestMutationAcknowledgesKnownAndUnknownAddressesIdenticallyAndEnqueuesIdOnly()
    {
        using var database = SqliteTestDatabase.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = database.CreateContext();
        var title = RefTestTitle.Create("Season 2026");
        context.RefTestTitles.Add(title);
        await context.SaveChangesAsync(cancellationToken);
        context.RefTests.Add(RefTest.Create(
            title.Id,
            "Ada",
            "Lovelace",
            ParticipantEmail,
            numberOfQuestions: 10,
            maxTimeInMinutes: 30,
            questionIds: ["q1"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: false));
        await context.SaveChangesAsync(cancellationToken);

        var service = new PersonalDataExportRequestService(
            context,
            NewJobEnqueueService(context),
            new TestKeyProtection(),
            new PrivacyChallengeConfiguration(),
            NullLogger<PersonalDataExportRequestService>.Instance);
        using var limiter = new PrivacyChallengeRateLimiter(new PrivacyChallengeConfiguration
        {
            RequestRateLimitPermitLimit = 5
        });
        var contextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var clientIpResolver = new FixedClientIpResolver();

        var knownAddressResult = await PersonalDataExportMutations.RequestPersonalDataExportAsync(
            new PersonalDataExportRequestInput(ParticipantEmail),
            service,
            limiter,
            clientIpResolver,
            contextAccessor,
            cancellationToken);
        var unknownAddressResult = await PersonalDataExportMutations.RequestPersonalDataExportAsync(
            new PersonalDataExportRequestInput("unknown@example.org"),
            service,
            limiter,
            clientIpResolver,
            contextAccessor,
            cancellationToken);

        Assert.Equal(knownAddressResult, unknownAddressResult);
        Assert.True(knownAddressResult.Acknowledged);
        var request = await context.PersonalDataExportRequests.SingleAsync(cancellationToken);
        Assert.Equal(64, request.KeyHash!.Length);
        Assert.NotNull(request.ProtectedDeliveryKey);

        var job = await context.Jobs.SingleAsync(cancellationToken);
        Assert.Equal(JobType.PersonalDataExportChallengeEmail, job.JobType);
        using var jobPayload = JsonDocument.Parse(job.Payload);
        Assert.Equal(["requestId"], jobPayload.RootElement.EnumerateObject()
            .Select(property => property.Name).ToArray());
        Assert.Equal(request.Id, jobPayload.RootElement.GetProperty("requestId").GetGuid());
        Assert.DoesNotContain(ParticipantEmail, job.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(request.KeyHash, job.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmationAtomicallyEnqueuesOnlyTheVerifiedRequestIdForDelivery()
    {
        using var database = SqliteTestDatabase.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var createdAt = DateTime.UtcNow;
        var request = NewRequest(
            createdAt,
            createdAt.AddHours(24),
            key: ChallengeKey,
            protectedKey: $"protected:{ChallengeKey}");

        await using (var seed = database.CreateContext())
        {
            seed.PersonalDataExportRequests.Add(request);
            await seed.SaveChangesAsync(cancellationToken);
        }

        await using var context = database.CreateContext();
        var service = new PersonalDataExportRequestService(
            context,
            NewJobEnqueueService(context),
            new TestKeyProtection(),
            new PrivacyChallengeConfiguration(),
            NullLogger<PersonalDataExportRequestService>.Instance);

        Assert.True(await service.ConfirmAsync(ChallengeKey, cancellationToken));

        var persistedRequest = await context.PersonalDataExportRequests.SingleAsync(cancellationToken);
        Assert.NotNull(persistedRequest.VerifiedAt);
        Assert.Equal(ParticipantEmail, persistedRequest.Email);

        var job = await context.Jobs.SingleAsync(cancellationToken);
        Assert.Equal(JobType.PersonalDataExportDeliveryEmail, job.JobType);
        using var payload = JsonDocument.Parse(job.Payload);
        Assert.Equal(["requestId"], payload.RootElement.EnumerateObject()
            .Select(property => property.Name).ToArray());
        Assert.Equal(request.Id, payload.RootElement.GetProperty("requestId").GetGuid());
        Assert.DoesNotContain(ParticipantEmail, job.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ChallengeKey, job.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestAndConfirmationRateLimitsAreIndependentAndPartitionedByClientAddress()
    {
        using var limiter = new PrivacyChallengeRateLimiter(new PrivacyChallengeConfiguration
        {
            RequestRateLimitPermitLimit = 1,
            ConfirmationRateLimitPermitLimit = 1
        });

        Assert.True(limiter.TryAcquireRequest("192.0.2.10"));
        Assert.False(limiter.TryAcquireRequest("192.0.2.10"));
        Assert.True(limiter.TryAcquireConfirmation("192.0.2.10"));
        Assert.True(limiter.TryAcquireRequest("192.0.2.11"));
    }

    [Fact]
    public async Task CleanupQueryAndServiceClearOnlyExpiredUnverifiedChallengeData()
    {
        using var database = SqliteTestDatabase.Create();
        var expired = NewRequest(Now.AddHours(-25), Now.AddHours(-1), key: "expired-key");
        var active = NewRequest(Now.AddHours(-1), Now.AddHours(23), email: "active@example.org", key: "active-key");
        var verified = NewRequest(Now.AddHours(-2), Now.AddHours(22), email: "verified@example.org", key: "verified-key");
        Assert.True(verified.TryConfirm("verified-key", Now.AddMinutes(-1)));

        await using (var seed = database.CreateContext())
        {
            seed.PersonalDataExportRequests.AddRange(expired, active, verified);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var dueIds = await context.PersonalDataExportRequests
                .Where(PersonalDataExportRequestCleanupQueries.IsDueForCleanup(Now))
                .Select(request => request.Id)
                .ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal([expired.Id], dueIds);

            var cleared = await PersonalDataExportRequestCleanupService.ClearExpiredChallengesAsync(
                context, Now, TestContext.Current.CancellationToken);
            Assert.Equal(1, cleared);
        }

        await using var verification = database.CreateContext();
        var clearedRequest = await verification.PersonalDataExportRequests.SingleAsync(
            request => request.Id == expired.Id, TestContext.Current.CancellationToken);
        var preservedActiveRequest = await verification.PersonalDataExportRequests.SingleAsync(
            request => request.Id == active.Id, TestContext.Current.CancellationToken);
        var preservedVerifiedRequest = await verification.PersonalDataExportRequests.SingleAsync(
            request => request.Id == verified.Id, TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, clearedRequest.Email);
        Assert.Null(clearedRequest.KeyHash);
        Assert.Null(clearedRequest.ProtectedDeliveryKey);
        Assert.Equal("active@example.org", preservedActiveRequest.Email);
        Assert.NotNull(preservedActiveRequest.KeyHash);
        Assert.Equal("verified@example.org", preservedVerifiedRequest.Email);
        Assert.Null(preservedVerifiedRequest.KeyHash);
    }

    [Fact]
    public async Task ChallengeEmailRetriesWithProtectedStateAndClearsItAfterAcceptance()
    {
        using var database = SqliteTestDatabase.Create();
        Guid requestId;

        await using (var seed = database.CreateContext())
        {
            var createdAt = DateTime.UtcNow;
            var request = NewRequest(
                createdAt,
                createdAt.AddHours(24),
                key: ChallengeKey,
                protectedKey: $"protected:{ChallengeKey}");
            seed.PersonalDataExportRequests.Add(request);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
            requestId = request.Id;
        }

        var emailService = new StubEmailService
        {
            Failure = new InvalidOperationException($"Provider echoed {ParticipantEmail} {ChallengeKey}")
        };
        var job = ChallengeJob(requestId);

        await using (var handlerContext = database.CreateContext())
        {
            var handler = new PersonalDataExportChallengeEmailJobHandler(
                handlerContext,
                emailService,
                new TestKeyProtection(),
                NullLogger<PersonalDataExportChallengeEmailJobHandler>.Instance);
            var failure = await Assert.ThrowsAsync<PersonalDataExportEmailDeliveryException>(
                () => handler.HandleAsync(job, TestContext.Current.CancellationToken));
            Assert.DoesNotContain(ParticipantEmail, failure.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ChallengeKey, failure.Message, StringComparison.Ordinal);
        }

        await using (var verification = database.CreateContext())
        {
            var pending = await verification.PersonalDataExportRequests.SingleAsync(
                request => request.Id == requestId, TestContext.Current.CancellationToken);
            Assert.NotNull(pending.ProtectedDeliveryKey);
            Assert.NotNull(pending.KeyHash);
            Assert.Null(pending.ChallengeEmailSentAt);
            Assert.Equal(1, pending.DeliveryAttemptCount);
        }

        emailService.Failure = null;
        await using (var retryContext = database.CreateContext())
        {
            var retryHandler = new PersonalDataExportChallengeEmailJobHandler(
                retryContext,
                emailService,
                new TestKeyProtection(),
                NullLogger<PersonalDataExportChallengeEmailJobHandler>.Instance);
            await retryHandler.HandleAsync(ChallengeJob(requestId), TestContext.Current.CancellationToken);
        }

        await using var finalVerification = database.CreateContext();
        var delivered = await finalVerification.PersonalDataExportRequests.SingleAsync(
            request => request.Id == requestId, TestContext.Current.CancellationToken);
        Assert.Equal(ParticipantEmail, emailService.RecipientEmail);
        Assert.Equal(ChallengeKey, emailService.ChallengeKey);
        Assert.Null(delivered.ProtectedDeliveryKey);
        Assert.NotNull(delivered.KeyHash);
        Assert.NotNull(delivered.ChallengeEmailSentAt);

        var payloadText = job.Payload;
        Assert.DoesNotContain(ChallengeKey, payloadText, StringComparison.Ordinal);
        Assert.DoesNotContain(ParticipantEmail, payloadText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerificationEventUsesExplicitParticipantActorWithoutChallengePii()
    {
        using var database = SqliteTestDatabase.Create();
        var request = NewRequest(Now, Now.AddHours(24), key: ChallengeKey);
        await using (var seed = database.CreateContext())
        {
            seed.PersonalDataExportRequests.Add(request);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var accessor = new HttpContextAccessor
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

        await using (var context = database.CreateContext(
                         new AuditSaveChangesInterceptor(accessor, new AuditLogOptions())))
        {
            var tracked = await context.PersonalDataExportRequests.SingleAsync(
                candidate => candidate.Id == request.Id, TestContext.Current.CancellationToken);
            Assert.True(tracked.TryConfirm(ChallengeKey, Now));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verification = database.CreateContext();
        var audit = await verification.AuditEvents.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(PersonalDataExportRequestVerifiedEvent.EventType, audit.Type);
        Assert.Equal("Verified participant", audit.ActorName);
        Assert.Equal(string.Empty, audit.ActorEmail);
        Assert.DoesNotContain(ParticipantEmail, audit.Data, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ChallengeKey, audit.Data, StringComparison.Ordinal);
        Assert.DoesNotContain(request.ProtectedDeliveryKey!, audit.Data, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ErasingTheLastSourceRecordClearsItsExportRequestAndCancelsBothIdOnlyJobs()
    {
        using var database = SqliteTestDatabase.Create();
        Guid refTestId;
        Guid requestId;

        await using (var seed = database.CreateContext())
        {
            var title = RefTestTitle.Create("Season 2026");
            seed.RefTestTitles.Add(title);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);

            var refTest = RefTest.Create(
                title.Id,
                "Ada",
                "Lovelace",
                ParticipantEmail,
                numberOfQuestions: 10,
                maxTimeInMinutes: 30,
                questionIds: ["q1"],
                sendInvitationAutomatically: false,
                sendResultsAutomatically: false);
            var createdAt = DateTime.UtcNow;
            var request = NewRequest(
                createdAt,
                createdAt.AddHours(24),
                key: ChallengeKey,
                protectedKey: $"protected:{ChallengeKey}");
            seed.RefTests.Add(refTest);
            seed.PersonalDataExportRequests.Add(request);
            seed.Jobs.AddRange(ChallengeJob(request.Id), DeliveryJob(request.Id));
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
            refTestId = refTest.Id;
            requestId = request.Id;
        }

        await using (var erasureContext = database.CreateContext())
        {
            var refTest = await erasureContext.RefTests.SingleAsync(
                candidate => candidate.Id == refTestId, TestContext.Current.CancellationToken);
            await new RefTestPrivacyErasureService(erasureContext).EraseAsync(
                refTest,
                ErasureInitiator.Operator,
                TestContext.Current.CancellationToken);
        }

        await using var verification = database.CreateContext();
        var erasedRequest = await verification.PersonalDataExportRequests.SingleAsync(
            candidate => candidate.Id == requestId, TestContext.Current.CancellationToken);
        var canceledJobs = await verification.Jobs.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, erasedRequest.Email);
        Assert.Null(erasedRequest.KeyHash);
        Assert.Null(erasedRequest.ProtectedDeliveryKey);
        Assert.Equal(2, canceledJobs.Count);
        Assert.All(canceledJobs, canceledJob =>
        {
            Assert.Equal(JobStatus.Cancelled, canceledJob.Status);
            Assert.Equal(string.Empty, canceledJob.Payload);
        });
    }

    [Fact]
    public async Task ErasingOneOfSeveralSourceRecordsClearsItsExportRequestAndCancelsBothIdOnlyJobs()
    {
        using var database = SqliteTestDatabase.Create();
        Guid erasedRefTestId;
        Guid remainingRefTestId;
        Guid requestId;

        await using (var seed = database.CreateContext())
        {
            var title = RefTestTitle.Create("Season 2026");
            seed.RefTestTitles.Add(title);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);

            var erasedRefTest = RefTest.Create(
                title.Id,
                "Ada",
                "Lovelace",
                ParticipantEmail,
                numberOfQuestions: 10,
                maxTimeInMinutes: 30,
                questionIds: ["q1"],
                sendInvitationAutomatically: false,
                sendResultsAutomatically: false);
            var remainingRefTest = RefTest.Create(
                title.Id,
                "Grace",
                "Hopper",
                ParticipantEmail,
                numberOfQuestions: 10,
                maxTimeInMinutes: 30,
                questionIds: ["q2"],
                sendInvitationAutomatically: false,
                sendResultsAutomatically: false);
            var createdAt = DateTime.UtcNow;
            var request = NewRequest(
                createdAt,
                createdAt.AddHours(24),
                key: ChallengeKey,
                protectedKey: $"protected:{ChallengeKey}");
            seed.RefTests.AddRange(erasedRefTest, remainingRefTest);
            seed.PersonalDataExportRequests.Add(request);
            seed.Jobs.AddRange(ChallengeJob(request.Id), DeliveryJob(request.Id));
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
            erasedRefTestId = erasedRefTest.Id;
            remainingRefTestId = remainingRefTest.Id;
            requestId = request.Id;
        }

        await using (var erasureContext = database.CreateContext())
        {
            var refTest = await erasureContext.RefTests.SingleAsync(
                candidate => candidate.Id == erasedRefTestId, TestContext.Current.CancellationToken);
            await new RefTestPrivacyErasureService(erasureContext).EraseAsync(
                refTest,
                ErasureInitiator.Operator,
                TestContext.Current.CancellationToken);
        }

        await using var verification = database.CreateContext();
        var erasedRequest = await verification.PersonalDataExportRequests.SingleAsync(
            candidate => candidate.Id == requestId, TestContext.Current.CancellationToken);
        var remainingRefTestRow = await verification.RefTests.SingleAsync(
            candidate => candidate.Id == remainingRefTestId, TestContext.Current.CancellationToken);
        var canceledJobs = await verification.Jobs.ToListAsync(TestContext.Current.CancellationToken);

        Assert.False(remainingRefTestRow.IsAnonymized);
        Assert.Equal(string.Empty, erasedRequest.Email);
        Assert.Null(erasedRequest.KeyHash);
        Assert.Null(erasedRequest.ProtectedDeliveryKey);
        Assert.Equal(2, canceledJobs.Count);
        Assert.All(canceledJobs, canceledJob =>
        {
            Assert.Equal(JobStatus.Cancelled, canceledJob.Status);
            Assert.Equal(string.Empty, canceledJob.Payload);
        });
    }

    private static Job ChallengeJob(Guid requestId)
    {
        var payload = JsonSerializer.Serialize(
            new PersonalDataExportChallengeEmailPayload(requestId),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return Job.Create(JobType.PersonalDataExportChallengeEmail, payload);
    }

    private static Job DeliveryJob(Guid requestId)
    {
        var payload = JsonSerializer.Serialize(
            new PersonalDataExportDeliveryEmailPayload(requestId),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return Job.Create(JobType.PersonalDataExportDeliveryEmail, payload);
    }

    private sealed class TestKeyProtection : IPersonalDataExportKeyProtection
    {
        public string Protect(string key) => $"protected:{key}";

        public string Unprotect(string protectedKey) =>
            protectedKey.StartsWith("protected:", StringComparison.Ordinal)
                ? protectedKey["protected:".Length..]
                : throw new InvalidOperationException("Test protection state is invalid.");
    }

    private sealed class FixedClientIpResolver : IClientIpResolver
    {
        public string Resolve(HttpContext context) => "192.0.2.10";
    }

    private sealed class StubEmailService : IEmailService
    {
        public Exception? Failure { get; set; }
        public string? RecipientEmail { get; private set; }
        public string? ChallengeKey { get; private set; }
        public string? ExportRecipientEmail { get; private set; }
        public IReadOnlyList<EmailAttachment>? ExportAttachments { get; private set; }

        public Task SendPersonalDataExportVerificationAsync(
            string recipientEmail,
            string challengeKey,
            DateTime expiresAt,
            CancellationToken cancellationToken)
        {
            RecipientEmail = recipientEmail;
            ChallengeKey = challengeKey;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }

        public Task SendPrivacyWithdrawalVerificationAsync(
            string recipientEmail,
            string challengeKey,
            DateTime expiresAt,
            CancellationToken cancellationToken)
        {
            RecipientEmail = recipientEmail;
            ChallengeKey = challengeKey;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }

        public async Task<bool> SendPersonalDataExportAsync(
            string recipientEmail,
            IReadOnlyList<EmailAttachment> attachments,
            Func<CancellationToken, Task<bool>> finalDeliverabilityCheck,
            CancellationToken cancellationToken)
        {
            if (!await finalDeliverabilityCheck(cancellationToken))
                return false;

            ExportRecipientEmail = recipientEmail;
            ExportAttachments = attachments;
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
    }
}
