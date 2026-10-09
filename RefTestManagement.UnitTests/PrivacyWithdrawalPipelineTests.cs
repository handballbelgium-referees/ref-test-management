using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Lifecycle;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.Security;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Persistence;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
using Handball.Belgium.RefTestManagement.Infrastructure.Security;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class PrivacyWithdrawalPipelineTests
{
    private const string ParticipantEmail = "ada@example.org";
    private static readonly string ChallengeKey = new('W', 43);
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private static JobEnqueueService NewJobEnqueueService(
        RefTestManagementContext context,
        ILogger<JobEnqueueService>? logger = null) =>
        new(
            context,
            new RefTestInvitationTokenProtection(new EphemeralDataProtectionProvider()),
            logger ?? NullLogger<JobEnqueueService>.Instance);

    private static Job NewBatchJob(PrivacyWithdrawalBatch batch)
    {
        var payload = JsonSerializer.Serialize(
            new PrivacyWithdrawalBatchPayload(batch.Id),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var job = Job.Create(
            JobType.PrivacyWithdrawalBatch,
            payload,
            privacyWithdrawalBatchId: batch.Id);
        batch.MarkJobEnqueued(job.Id);
        return job;
    }

    private static PrivacyWithdrawalBatchTarget ExhaustedTarget(Guid batchId)
    {
        var target = PrivacyWithdrawalBatchTarget.Create(batchId, Guid.NewGuid());
        var attemptAt = Now;
        for (var attempt = 1; attempt <= PrivacyWithdrawalBatchTarget.MaximumAttempts; attempt++)
        {
            target.TryStartAttempt(attemptAt);
            var failedAt = attemptAt.AddSeconds(1);
            target.RecordProcessingFailure(failedAt);
            if (attempt < PrivacyWithdrawalBatchTarget.MaximumAttempts)
                attemptAt = target.NextAttemptAt!.Value;
        }

        return target;
    }

    private static IRefTestSessionTokenService NewSessionTokenService() =>
        new RefTestSessionTokenService(new EphemeralDataProtectionProvider(), TimeProvider.System);

    private static PrivacyWithdrawalRequestService NewRequestService(
        RefTestManagementContext context,
        CapturingKeyProtection keyProtection,
        ILogger<PrivacyWithdrawalRequestService>? logger = null,
        PrivacyChallengeConfiguration? configuration = null,
        BackgroundJobConfiguration? backgroundJobConfiguration = null,
        IRefTestSessionTokenService? sessionTokenService = null) =>
        new(
            context,
            NewJobEnqueueService(context),
            keyProtection,
            new RefTestRepository(context, sessionTokenService ?? NewSessionTokenService()),
            configuration ?? new PrivacyChallengeConfiguration(),
            backgroundJobConfiguration ?? new BackgroundJobConfiguration(),
            logger ?? NullLogger<PrivacyWithdrawalRequestService>.Instance);

    private static RefTest NewRefTest(
        Guid titleId,
        string email,
        bool requiresApproval = false) =>
        RefTest.Create(
            titleId,
            "Ada",
            "Lovelace",
            email,
            numberOfQuestions: 10,
            maxTimeInMinutes: 30,
            questionIds: ["q1", "q2"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: false,
            requiresApproval: requiresApproval);

    private static async Task<Guid> SeedTitleAsync(SqliteTestDatabase database)
    {
        await using var context = database.CreateContext();
        var title = RefTestTitle.Create("Season 2026");
        context.RefTestTitles.Add(title);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return title.Id;
    }

    private sealed class ConcurrentSqliteTestDatabase : IDisposable
    {
        private readonly string _connectionString =
            $"Data Source=withdrawal-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        private readonly SqliteConnection _anchorConnection;
        private readonly DbContextOptions<RefTestManagementContext> _options;

        public ConcurrentSqliteTestDatabase()
        {
            _anchorConnection = new SqliteConnection(_connectionString);
            _anchorConnection.Open();
            _options = new DbContextOptionsBuilder<RefTestManagementContext>()
                .UseSqlite(_connectionString)
                .Options;

            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public RefTestManagementContext CreateContext() => new(_options);

        public void Dispose() => _anchorConnection.Dispose();
    }

    [Fact]
    public void ChallengeLifecycleHashesKeysAndClearsRecipientAndDeliveryState()
    {
        var expiresAt = Now.AddHours(24);
        var challenge = PrivacyWithdrawalChallenge.Create(
            ParticipantEmail,
            PrivacyWithdrawalChallenge.HashNormalizedEmail(
                PrivacyWithdrawalChallenge.NormalizeEmail(ParticipantEmail)),
            ChallengeKey,
            $"protected:{ChallengeKey}",
            Now,
            expiresAt,
            matchingRefTestCount: 3);

        Assert.True(challenge.IsPendingAt(Now));
        Assert.Equal(PrivacyWithdrawalChallenge.HashKey(ChallengeKey), challenge.KeyHash);
        Assert.NotEqual(ChallengeKey, challenge.KeyHash);
        Assert.True(challenge.CanDeliverAt(Now));
        Assert.True(challenge.MarkChallengeEmailDelivered(Now.AddMinutes(1)));
        Assert.Null(challenge.ProtectedDeliveryKey);
        Assert.True(challenge.IsPendingAt(Now.AddMinutes(1)));
        Assert.True(challenge.TryConfirm(ChallengeKey, Now.AddMinutes(2)));
        Assert.False(challenge.TryConfirm(ChallengeKey, Now.AddMinutes(3)));
        Assert.Equal(Now.AddMinutes(2), challenge.VerifiedAt);
        Assert.Equal(string.Empty, challenge.Email);
        Assert.Null(challenge.KeyHash);
        Assert.Null(challenge.ProtectedDeliveryKey);
        Assert.NotEqual(
            PrivacyWithdrawalChallenge.HashNormalizedEmail(
                PrivacyWithdrawalChallenge.NormalizeEmail(ParticipantEmail)),
            challenge.NormalizedEmailHash);

        var auditData = JsonSerializer.Serialize(
            challenge.DomainEvents.Select(domainEvent => domainEvent.GetChanges()));
        Assert.Contains("matchingRefTestCount", auditData, StringComparison.Ordinal);
        Assert.DoesNotContain(ParticipantEmail, auditData, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ChallengeKey, auditData, StringComparison.Ordinal);
        Assert.DoesNotContain($"protected:{ChallengeKey}", auditData, StringComparison.Ordinal);

        var expired = PrivacyWithdrawalChallenge.Create(
            ParticipantEmail,
            PrivacyWithdrawalChallenge.HashNormalizedEmail(
                PrivacyWithdrawalChallenge.NormalizeEmail(ParticipantEmail)),
            ChallengeKey,
            $"protected:{ChallengeKey}",
            Now,
            expiresAt,
            matchingRefTestCount: 1);
        Assert.False(expired.ClearExpiredChallenge(expiresAt.AddTicks(-1)));
        Assert.True(expired.ClearExpiredChallenge(expiresAt));
        Assert.Equal(string.Empty, expired.Email);
        Assert.Null(expired.KeyHash);
        Assert.Null(expired.ProtectedDeliveryKey);
    }

    [Fact]
    public void WithdrawalTargetBackoffIsDurableAndStopsAtItsAttemptLimit()
    {
        var target = PrivacyWithdrawalBatchTarget.Create(Guid.NewGuid(), Guid.NewGuid());
        var attemptAt = Now;

        for (var attempt = 1; attempt <= PrivacyWithdrawalBatchTarget.MaximumAttempts; attempt++)
        {
            Assert.True(target.TryStartAttempt(attemptAt));
            Assert.Equal(attempt, target.AttemptCount);
            var failedAt = attemptAt.AddSeconds(1);
            Assert.True(target.RecordProcessingFailure(failedAt));
            Assert.Equal(PrivacyWithdrawalTargetFailureCode.ProcessingFailed, target.FailureCode);

            if (attempt == PrivacyWithdrawalBatchTarget.MaximumAttempts)
            {
                Assert.Equal(failedAt, target.RetryExhaustedAt);
                Assert.Null(target.NextAttemptAt);
            }
            else
            {
                Assert.True(target.NextAttemptAt > failedAt);
                attemptAt = target.NextAttemptAt!.Value;
            }
        }

        Assert.False(target.TryStartAttempt(Now.AddYears(1)));
        Assert.False(target.RecordProcessingFailure(Now.AddYears(1)));
        Assert.Equal(PrivacyWithdrawalBatchTarget.MaximumAttempts, target.AttemptCount);
        Assert.NotNull(target.RetryExhaustedAt);
    }

    [Fact]
    public void BatchCompletionAuditSeparatesCompletedAndExhaustedTargets()
    {
        var batch = PrivacyWithdrawalBatch.Create(Now, targetCount: 3);

        Assert.True(batch.MarkCompleted(Now.AddMinutes(1), exhaustedTargetCount: 1));

        var completionEvent = batch.DomainEvents.Single(
            domainEvent => domainEvent.ActionName == "PrivacyWithdrawalBatchCompleted");
        var changes = JsonSerializer.Serialize(completionEvent.GetChanges());
        Assert.Contains("\"completedTargetCount\":2", changes, StringComparison.Ordinal);
        Assert.Contains("\"exhaustedTargetCount\":1", changes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestDoesNotQueueUnknownAddressesAndSuppressesRepeatedNormalizedRequests()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        await using var context = database.CreateContext();
        var legacyRefTest = NewRefTest(titleId, ParticipantEmail);
        context.RefTests.Add(legacyRefTest);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.Entry(legacyRefTest).Property(refTest => refTest.EmailLookupKey).CurrentValue = null;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var keyProtection = new CapturingKeyProtection();
        var service = NewRequestService(
            context,
            keyProtection,
            configuration: new PrivacyChallengeConfiguration
            {
                PrivacyChallengeKeyLifetimeHours = 2
            });
        await service.RequestAsync("missing@example.org", TestContext.Current.CancellationToken);

        Assert.Equal(
            TokenService.HashBytes(PrivacyWithdrawalChallenge.NormalizeEmail(ParticipantEmail)),
            legacyRefTest.EmailLookupKey);
        Assert.Empty(await context.PrivacyWithdrawalChallenges.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await context.Jobs.ToListAsync(TestContext.Current.CancellationToken));

        await service.RequestAsync("  ADA@EXAMPLE.ORG  ", TestContext.Current.CancellationToken);
        var challenge = await context.PrivacyWithdrawalChallenges.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ParticipantEmail, challenge.Email);
        Assert.Equal(TimeSpan.FromHours(2), challenge.ExpiresAt - challenge.CreatedAt);
        Assert.Single(keyProtection.ProtectedKeys);
        Assert.Single(await context.Jobs.ToListAsync(TestContext.Current.CancellationToken));

        // An active challenge suppresses a duplicate request even before delivery succeeds.
        await service.RequestAsync("Ada@Example.Org", TestContext.Current.CancellationToken);
        Assert.Single(await context.PrivacyWithdrawalChallenges.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await context.Jobs.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(keyProtection.ProtectedKeys);

        // A successfully delivered email is still a pending, unexpired challenge.
        Assert.True(challenge.MarkChallengeEmailDelivered(DateTime.UtcNow));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await service.RequestAsync("Ada@Example.Org", TestContext.Current.CancellationToken);

        Assert.Single(await context.PrivacyWithdrawalChallenges.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await context.Jobs.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(keyProtection.ProtectedKeys);

        var emailJob = await context.Jobs.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(JobType.PrivacyWithdrawalChallengeEmail, emailJob.JobType);
        using var emailPayload = JsonDocument.Parse(emailJob.Payload);
        Assert.Equal(
            ["challengeId"],
            emailPayload.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(challenge.Id, emailPayload.RootElement.GetProperty("challengeId").GetGuid());
        Assert.DoesNotContain(ParticipantEmail, emailJob.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(keyProtection.ProtectedKeys[0], emailJob.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public void RefTestEmailChangesKeepTheNormalizedLookupKeyInSyncAndErasureClearsIt()
    {
        var refTest = NewRefTest(Guid.NewGuid(), "before@example.org");

        refTest.UpdateBasicDetails("Ada", "Lovelace", " New@Example.org ");

        Assert.Equal(
            TokenService.HashBytes(
                PrivacyWithdrawalChallenge.NormalizeEmail(" New@Example.org ")),
            refTest.EmailLookupKey);

        refTest.Anonymize();
        Assert.Null(refTest.EmailLookupKey);

        // Repeated erasure also clears any stale derived key without restoring a match.
        refTest.Anonymize();
        Assert.Null(refTest.EmailLookupKey);
    }

    [Fact]
    public async Task ParticipantTokenQueuesOnlyItsRefTestAndReplayDoesNotSendEmailOrDuplicateWork()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var participant = NewRefTest(titleId, ParticipantEmail);
        var otherParticipant = NewRefTest(titleId, "grace@example.org");
        var participantToken = participant.GetIssuedToken();

        await using (var seed = database.CreateContext())
        {
            seed.RefTests.AddRange(participant, otherParticipant);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var keyProtection = new CapturingKeyProtection();
        await using var context = database.CreateContext();
        var service = NewRequestService(context, keyProtection);
        Assert.True(await RefTestLifecycleMutations.WithdrawConsentAsync(
            participantToken,
            service,
            TestContext.Current.CancellationToken));
        Assert.True(await RefTestLifecycleMutations.WithdrawConsentAsync(
            participantToken,
            service,
            TestContext.Current.CancellationToken));

        await using var verification = database.CreateContext();
        var batch = await verification.PrivacyWithdrawalBatches
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, batch.TargetCount);
        var target = await verification.PrivacyWithdrawalBatchTargets
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(participant.Id, target.RefTestId);

        var job = await verification.Jobs.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(JobType.PrivacyWithdrawalBatch, job.JobType);
        using var payload = JsonDocument.Parse(job.Payload);
        Assert.Equal(["batchId"], payload.RootElement.EnumerateObject()
            .Select(property => property.Name).ToArray());
        Assert.Equal(batch.Id, payload.RootElement.GetProperty("batchId").GetGuid());
        Assert.DoesNotContain(participantToken, job.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(ParticipantEmail, job.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await verification.PrivacyWithdrawalChallenges
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(keyProtection.ProtectedKeys);
        Assert.False((await verification.RefTests
            .SingleAsync(refTest => refTest.Id == participant.Id, TestContext.Current.CancellationToken))
            .IsAnonymized);
        Assert.False((await verification.RefTests
            .SingleAsync(refTest => refTest.Id == otherParticipant.Id, TestContext.Current.CancellationToken))
            .IsAnonymized);
    }

    [Fact]
    public async Task ParticipantSessionTokenQueuesOnlyItsRefTest()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var participant = NewRefTest(titleId, ParticipantEmail);
        participant.AcceptPrivacyNotice("v1");
        var sessionTokenService = NewSessionTokenService();
        var sessionToken = sessionTokenService.Create(participant);

        await using (var seed = database.CreateContext())
        {
            seed.RefTests.Add(participant);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var service = NewRequestService(
                context,
                new CapturingKeyProtection(),
                sessionTokenService: sessionTokenService);

            Assert.True(await RefTestLifecycleMutations.WithdrawConsentAsync(
                sessionToken,
                service,
                TestContext.Current.CancellationToken));
        }

        await using var verification = database.CreateContext();
        var target = await verification.PrivacyWithdrawalBatchTargets
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(participant.Id, target.RefTestId);
    }

    [Fact]
    public async Task InvalidParticipantTokenCannotQueueAWithdrawal()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var participant = NewRefTest(titleId, ParticipantEmail);
        var token = participant.GetIssuedToken();
        var invalidToken = $"{(token[0] == '0' ? '1' : '0')}{token[1..]}";

        await using (var seed = database.CreateContext())
        {
            seed.RefTests.Add(participant);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateContext();
        var service = NewRequestService(context, new CapturingKeyProtection());
        await Assert.ThrowsAsync<RefTestNotFoundException>(() =>
            RefTestLifecycleMutations.WithdrawConsentAsync(
                invalidToken,
                service,
                TestContext.Current.CancellationToken));

        await using var verification = database.CreateContext();
        Assert.Empty(await verification.PrivacyWithdrawalBatches
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await verification.PrivacyWithdrawalBatchTargets
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await verification.Jobs.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentParticipantTokenClaimsCommitOnlyOneBatchAndJob()
    {
        using var database = new ConcurrentSqliteTestDatabase();
        RefTest participant;
        string participantToken;

        await using (var seed = database.CreateContext())
        {
            var title = RefTestTitle.Create("Season 2026");
            participant = NewRefTest(title.Id, ParticipantEmail);
            participantToken = participant.GetIssuedToken();
            seed.RefTestTitles.Add(title);
            seed.RefTests.Add(participant);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var ready = new CountdownEvent(2);
        using var start = new ManualResetEventSlim();
        Task<bool> SubmitAsync() => Task.Run(async () =>
        {
            ready.Signal();
            start.Wait(TestContext.Current.CancellationToken);

            await using var requestContext = database.CreateContext();
            var service = NewRequestService(requestContext, new CapturingKeyProtection());
            return await RefTestLifecycleMutations.WithdrawConsentAsync(
                participantToken,
                service,
                TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);

        var first = SubmitAsync();
        var second = SubmitAsync();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        start.Set();
        Assert.All(await Task.WhenAll(first, second), result => Assert.True(result));

        await using var verification = database.CreateContext();
        Assert.Single(await verification.PrivacyWithdrawalBatches
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await verification.PrivacyWithdrawalBatchTargets
            .ToListAsync(TestContext.Current.CancellationToken));
        var job = await verification.Jobs.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(JobType.PrivacyWithdrawalBatch, job.JobType);
    }

    [Fact]
    public async Task BulkConfirmationDoesNotQueueAnAlreadyClaimedParticipantTargetAgain()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var now = DateTime.UtcNow;
        var refTest = NewRefTest(titleId, ParticipantEmail);
        var challenge = PrivacyWithdrawalChallenge.Create(
            ParticipantEmail,
            PrivacyWithdrawalChallenge.HashNormalizedEmail(
                PrivacyWithdrawalChallenge.NormalizeEmail(ParticipantEmail)),
            ChallengeKey,
            $"protected:{ChallengeKey}",
            now,
            now.AddHours(24),
            matchingRefTestCount: 1);

        await using var context = database.CreateContext();
        context.RefTests.Add(refTest);
        context.PrivacyWithdrawalChallenges.Add(challenge);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = NewRequestService(context, new CapturingKeyProtection());

        Assert.True(await service.RequestForParticipantAsync(
            refTest.GetIssuedToken(),
            TestContext.Current.CancellationToken));
        Assert.True(await service.ConfirmAsync(ChallengeKey, TestContext.Current.CancellationToken));

        await using var verification = database.CreateContext();
        Assert.Single(await verification.PrivacyWithdrawalBatches
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await verification.PrivacyWithdrawalBatchTargets
            .ToListAsync(TestContext.Current.CancellationToken));
        var job = await verification.Jobs.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(JobType.PrivacyWithdrawalBatch, job.JobType);
        var consumedChallenge = await verification.PrivacyWithdrawalChallenges
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(consumedChallenge.VerifiedAt);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("missing")]
    [InlineData("exhausted")]
    public async Task ParticipantTokenRecoversAnIncompleteBatchWhenItsJobIsNotProcessable(string jobState)
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var participant = NewRefTest(titleId, ParticipantEmail);
        var participantToken = participant.GetIssuedToken();

        await using (var seed = database.CreateContext())
        {
            seed.RefTests.Add(participant);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Guid originalBatchId;
        Guid originalJobId;
        await using (var context = database.CreateContext())
        {
            var service = NewRequestService(context, new CapturingKeyProtection());
            Assert.True(await service.RequestForParticipantAsync(
                participantToken,
                TestContext.Current.CancellationToken));

            originalBatchId = await context.PrivacyWithdrawalBatches
                .Select(batch => batch.Id)
                .SingleAsync(TestContext.Current.CancellationToken);
            var originalJob = await context.Jobs.SingleAsync(TestContext.Current.CancellationToken);
            originalJobId = originalJob.Id;
            if (jobState == "missing")
                context.Jobs.Remove(originalJob);
            else if (jobState == "failed")
                originalJob.MarkAsFailed("Simulated terminal failure", maxAttempts: 1);
            else
            {
                var maxAttempts = new BackgroundJobConfiguration().MaxAttempts;
                originalJob.MarkAsProcessing(TimeSpan.FromMinutes(-10));
                for (var attempt = 0; attempt < maxAttempts; attempt++)
                    originalJob.MarkAsProcessing(TimeSpan.FromMinutes(-10));
                Assert.Equal(maxAttempts, originalJob.Attempts);
            }

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.True(await service.RequestForParticipantAsync(
                participantToken,
                TestContext.Current.CancellationToken));
        }

        await using var verification = database.CreateContext();
        var batch = await verification.PrivacyWithdrawalBatches
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(originalBatchId, batch.Id);
        Assert.Null(batch.CompletedAt);
        var target = await verification.PrivacyWithdrawalBatchTargets
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(originalBatchId, target.BatchId);
        Assert.Equal(participant.Id, target.RefTestId);

        var jobs = await verification.Jobs.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(jobState == "missing" ? 1 : 2, jobs.Count);
        var replacementJob = Assert.Single(jobs, job => job.Status == JobStatus.Pending);
        Assert.Equal(JobType.PrivacyWithdrawalBatch, replacementJob.JobType);
        using (var payload = JsonDocument.Parse(replacementJob.Payload))
            Assert.Equal(originalBatchId, payload.RootElement.GetProperty("batchId").GetGuid());

        if (jobState != "missing")
        {
            var failedJob = Assert.Single(jobs, job => job.Id == originalJobId);
            Assert.Equal(JobStatus.Failed, failedJob.Status);
            Assert.NotNull(failedJob.CompletedAt);
            Assert.NotEqual(failedJob.Id, replacementJob.Id);
        }
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("missing")]
    [InlineData("exhausted")]
    public async Task BulkConfirmationRecoversAnIncompleteBatchWhenItsJobIsNotProcessable(string jobState)
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var now = DateTime.UtcNow;
        var refTest = NewRefTest(titleId, ParticipantEmail);
        var challenge = PrivacyWithdrawalChallenge.Create(
            ParticipantEmail,
            PrivacyWithdrawalChallenge.HashNormalizedEmail(
                PrivacyWithdrawalChallenge.NormalizeEmail(ParticipantEmail)),
            ChallengeKey,
            $"protected:{ChallengeKey}",
            now,
            now.AddHours(24),
            matchingRefTestCount: 1);

        Guid originalBatchId;
        Guid originalJobId;
        await using (var seed = database.CreateContext())
        {
            seed.RefTests.Add(refTest);
            seed.PrivacyWithdrawalChallenges.Add(challenge);
            StageConfirmedBatch(seed, refTest.Id, now);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);

            originalBatchId = await seed.PrivacyWithdrawalBatches
                .Select(batch => batch.Id)
                .SingleAsync(TestContext.Current.CancellationToken);
            var originalJob = await seed.Jobs.SingleAsync(TestContext.Current.CancellationToken);
            originalJobId = originalJob.Id;
            if (jobState == "missing")
                seed.Jobs.Remove(originalJob);
            else if (jobState == "failed")
                originalJob.MarkAsFailed("Simulated terminal failure", maxAttempts: 1);
            else
            {
                var maxAttempts = new BackgroundJobConfiguration().MaxAttempts;
                originalJob.MarkAsProcessing(TimeSpan.FromMinutes(-10));
                for (var attempt = 0; attempt < maxAttempts; attempt++)
                    originalJob.MarkAsProcessing(TimeSpan.FromMinutes(-10));
                Assert.Equal(maxAttempts, originalJob.Attempts);
            }

            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var service = NewRequestService(context, new CapturingKeyProtection());
            Assert.True(await service.ConfirmAsync(
                ChallengeKey,
                TestContext.Current.CancellationToken));
        }

        await using var verification = database.CreateContext();
        var batch = await verification.PrivacyWithdrawalBatches
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(originalBatchId, batch.Id);
        Assert.Null(batch.CompletedAt);
        var target = await verification.PrivacyWithdrawalBatchTargets
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(originalBatchId, target.BatchId);
        Assert.Equal(refTest.Id, target.RefTestId);
        var consumedChallenge = await verification.PrivacyWithdrawalChallenges
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(consumedChallenge.VerifiedAt);

        var jobs = await verification.Jobs.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(jobState == "missing" ? 1 : 2, jobs.Count);
        var replacementJob = Assert.Single(jobs, job => job.Status == JobStatus.Pending);
        using (var payload = JsonDocument.Parse(replacementJob.Payload))
            Assert.Equal(originalBatchId, payload.RootElement.GetProperty("batchId").GetGuid());

        if (jobState != "missing")
        {
            var failedJob = Assert.Single(jobs, job => job.Id == originalJobId);
            Assert.Equal(JobStatus.Failed, failedJob.Status);
            Assert.NotNull(failedJob.CompletedAt);
            Assert.NotEqual(failedJob.Id, replacementJob.Id);
        }
    }

    [Fact]
    public async Task LiveProcessingBatchJobAtAttemptLimitSuppressesTokenAndBulkDuplicates()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var now = DateTime.UtcNow;
        var refTest = NewRefTest(titleId, ParticipantEmail);
        var participantToken = refTest.GetIssuedToken();
        var challenge = PrivacyWithdrawalChallenge.Create(
            ParticipantEmail,
            PrivacyWithdrawalChallenge.HashNormalizedEmail(
                PrivacyWithdrawalChallenge.NormalizeEmail(ParticipantEmail)),
            ChallengeKey,
            $"protected:{ChallengeKey}",
            now,
            now.AddHours(24),
            matchingRefTestCount: 1);

        await using (var seed = database.CreateContext())
        {
            seed.RefTests.Add(refTest);
            seed.PrivacyWithdrawalChallenges.Add(challenge);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var service = NewRequestService(context, new CapturingKeyProtection());
            Assert.True(await service.RequestForParticipantAsync(
                participantToken,
                TestContext.Current.CancellationToken));

            var job = await context.Jobs.SingleAsync(TestContext.Current.CancellationToken);
            var maxAttempts = new BackgroundJobConfiguration().MaxAttempts;
            job.MarkAsProcessing(TimeSpan.FromMinutes(10));
            for (var attempt = 0; attempt < maxAttempts; attempt++)
                job.MarkAsProcessing(TimeSpan.FromMinutes(10));
            Assert.Equal(maxAttempts, job.Attempts);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.True(await service.RequestForParticipantAsync(
                participantToken,
                TestContext.Current.CancellationToken));
            Assert.True(await service.ConfirmAsync(
                ChallengeKey,
                TestContext.Current.CancellationToken));
        }

        await using var verification = database.CreateContext();
        Assert.Single(await verification.PrivacyWithdrawalBatches
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await verification.PrivacyWithdrawalBatchTargets
            .ToListAsync(TestContext.Current.CancellationToken));
        var liveJob = await verification.Jobs.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Processing, liveJob.Status);
        Assert.Equal(new BackgroundJobConfiguration().MaxAttempts, liveJob.Attempts);
    }

    [Fact]
    public async Task ParticipantMutationDoesNotAcknowledgeWhenDurableEnqueueFails()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var participant = NewRefTest(titleId, ParticipantEmail);
        var participantToken = participant.GetIssuedToken();

        await using (var seed = database.CreateContext())
        {
            seed.RefTests.Add(participant);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var failingEnqueuer = DispatchProxy.Create<
                IJobEnqueueService,
                FailingWithdrawalBatchEnqueueProxy>();
            var service = new PrivacyWithdrawalRequestService(
                context,
                failingEnqueuer,
                new CapturingKeyProtection(),
                new RefTestRepository(context, NewSessionTokenService()),
                new PrivacyChallengeConfiguration(),
                new BackgroundJobConfiguration(),
                NullLogger<PrivacyWithdrawalRequestService>.Instance);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                RefTestLifecycleMutations.WithdrawConsentAsync(
                    participantToken,
                    service,
                    TestContext.Current.CancellationToken));
        }

        await using var verification = database.CreateContext();
        Assert.Empty(await verification.PrivacyWithdrawalBatches
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await verification.PrivacyWithdrawalBatchTargets
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await verification.Jobs.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConfirmationSnapshotsEveryStatusAndIncludesRecordsAddedAfterTheRequest()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var refTests = CreateAllStatusRefTests(titleId);
        var anonymized = NewRefTest(titleId, ParticipantEmail);
        anonymized.Anonymize();
        refTests.Add(anonymized);

        await using var context = database.CreateContext();
        context.RefTests.AddRange(refTests);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var keyProtection = new CapturingKeyProtection();
        var service = NewRequestService(context, keyProtection);
        await service.RequestAsync("  ADA@example.org  ", TestContext.Current.CancellationToken);

        var addedAfterRequest = NewRefTest(titleId, "Ada@Example.org");
        context.RefTests.Add(addedAfterRequest);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(await service.ConfirmAsync(
            keyProtection.ProtectedKeys.Single(),
            TestContext.Current.CancellationToken));
        Assert.False(await service.ConfirmAsync(
            keyProtection.ProtectedKeys.Single(),
            TestContext.Current.CancellationToken));

        var batch = await context.PrivacyWithdrawalBatches.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(7, batch.TargetCount);
        var targetIds = await context.PrivacyWithdrawalBatchTargets
            .Where(target => target.BatchId == batch.Id)
            .Select(target => target.RefTestId)
            .ToListAsync(TestContext.Current.CancellationToken);
        var expectedIds = refTests.Where(refTest => !refTest.IsAnonymized)
            .Select(refTest => refTest.Id)
            .Append(addedAfterRequest.Id)
            .Order()
            .ToArray();
        Assert.Equal(expectedIds, targetIds.Order().ToArray());
        Assert.Equal(
            Enum.GetValues<RefTestStatus>().Order(),
            refTests.Where(refTest => !refTest.IsAnonymized).Select(refTest => refTest.Status).Distinct().Order());

        var batchJobs = await context.Jobs
            .Where(job => job.JobType == JobType.PrivacyWithdrawalBatch)
            .ToListAsync(TestContext.Current.CancellationToken);
        var batchJob = Assert.Single(batchJobs);
        using var payload = JsonDocument.Parse(batchJob.Payload);
        Assert.Equal(["batchId"], payload.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(batch.Id, payload.RootElement.GetProperty("batchId").GetGuid());
        Assert.DoesNotContain(ParticipantEmail, batchJob.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(addedAfterRequest.Id.ToString(), batchJob.Payload, StringComparison.OrdinalIgnoreCase);

        var consumedChallenge = await context.PrivacyWithdrawalChallenges
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(consumedChallenge.VerifiedAt);
        Assert.Equal(string.Empty, consumedChallenge.Email);
        Assert.Null(consumedChallenge.KeyHash);
        Assert.Null(consumedChallenge.ProtectedDeliveryKey);
    }

    [Fact]
    public async Task MatchingTrimsWhitespaceAndUsesInvariantUnicodeCaseRules()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        await using var context = database.CreateContext();
        var unicodeRefTest = NewRefTest(titleId, "  Élodie@example.org  ");
        context.RefTests.Add(unicodeRefTest);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var keyProtection = new CapturingKeyProtection();
        var service = NewRequestService(context, keyProtection);
        await service.RequestAsync(" éLODIE@EXAMPLE.ORG ", TestContext.Current.CancellationToken);

        var challenge = await context.PrivacyWithdrawalChallenges.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Élodie@example.org", challenge.Email.Trim());
        Assert.True(await service.ConfirmAsync(
            keyProtection.ProtectedKeys.Single(),
            TestContext.Current.CancellationToken));

        var target = await context.PrivacyWithdrawalBatchTargets.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(unicodeRefTest.Id, target.RefTestId);
    }

    [Fact]
    public async Task InvalidAndExpiredChallengesCannotCreateWithdrawalJobs()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        await using var context = database.CreateContext();
        context.RefTests.Add(NewRefTest(titleId, ParticipantEmail));

        var expired = PrivacyWithdrawalChallenge.Create(
            ParticipantEmail,
            PrivacyWithdrawalChallenge.HashNormalizedEmail(
                PrivacyWithdrawalChallenge.NormalizeEmail(ParticipantEmail)),
            ChallengeKey,
            $"protected:{ChallengeKey}",
            DateTime.UtcNow.AddHours(-25),
            DateTime.UtcNow.AddHours(-1),
            matchingRefTestCount: 1);
        context.PrivacyWithdrawalChallenges.Add(expired);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = NewRequestService(context, new CapturingKeyProtection());
        Assert.False(await service.ConfirmAsync(new string('X', 43), TestContext.Current.CancellationToken));
        Assert.False(await service.ConfirmAsync(ChallengeKey, TestContext.Current.CancellationToken));
        Assert.Empty(await context.PrivacyWithdrawalBatches.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await context.Jobs.ToListAsync(TestContext.Current.CancellationToken));

        var cleared = await context.PrivacyWithdrawalChallenges.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, cleared.Email);
        Assert.Null(cleared.KeyHash);
        Assert.Null(cleared.ProtectedDeliveryKey);
    }

    [Fact]
    public async Task ChallengeEmailRetryKeepsProtectedKeyUntilDeliveryThenClearsIt()
    {
        using var database = SqliteTestDatabase.Create();
        var now = DateTime.UtcNow;
        var protectedKey = $"protected:{ChallengeKey}";
        var challenge = PrivacyWithdrawalChallenge.Create(
            ParticipantEmail,
            PrivacyWithdrawalChallenge.HashNormalizedEmail(
                PrivacyWithdrawalChallenge.NormalizeEmail(ParticipantEmail)),
            ChallengeKey,
            protectedKey,
            now,
            now.AddHours(2),
            matchingRefTestCount: 1);
        var payload = JsonSerializer.Serialize(
            new PrivacyWithdrawalChallengeEmailPayload(challenge.Id),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var job = Job.Create(JobType.PrivacyWithdrawalChallengeEmail, payload);
        await using (var seed = database.CreateContext())
        {
            seed.PrivacyWithdrawalChallenges.Add(challenge);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var emailService = DispatchProxy.Create<IEmailService, RecordingPrivacyWithdrawalEmailProxy>();
        var emailProxy = (RecordingPrivacyWithdrawalEmailProxy)(object)emailService;
        emailProxy.Failure = new InvalidOperationException(
            $"Provider echoed {ParticipantEmail} {ChallengeKey}.");
        using var loggerProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));

        await using (var failedAttempt = database.CreateContext())
        {
            var handler = new PrivacyWithdrawalChallengeEmailJobHandler(
                failedAttempt,
                emailService,
                new CapturingKeyProtection(),
                new BackgroundJobConfiguration(),
                loggerFactory.CreateLogger<PrivacyWithdrawalChallengeEmailJobHandler>());
            var failure = await Assert.ThrowsAsync<PrivacyWithdrawalEmailDeliveryException>(
                () => handler.HandleAsync(job, TestContext.Current.CancellationToken));
            Assert.DoesNotContain(ParticipantEmail, failure.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ChallengeKey, failure.Message, StringComparison.Ordinal);

            var pending = await failedAttempt.PrivacyWithdrawalChallenges
                .SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, pending.DeliveryAttemptCount);
            Assert.NotNull(pending.ProtectedDeliveryKey);
            Assert.Null(pending.ChallengeEmailSentAt);
        }

        emailProxy.Failure = null;
        await using (var successfulAttempt = database.CreateContext())
        {
            var handler = new PrivacyWithdrawalChallengeEmailJobHandler(
                successfulAttempt,
                emailService,
                new CapturingKeyProtection(),
                new BackgroundJobConfiguration(),
                loggerFactory.CreateLogger<PrivacyWithdrawalChallengeEmailJobHandler>());
            await handler.HandleAsync(job, TestContext.Current.CancellationToken);

            var delivered = await successfulAttempt.PrivacyWithdrawalChallenges
                .SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, delivered.DeliveryAttemptCount);
            Assert.NotNull(delivered.ChallengeEmailSentAt);
            Assert.Null(delivered.ProtectedDeliveryKey);
        }

        Assert.Equal(2, emailProxy.InvocationCount);
        Assert.Equal(ParticipantEmail, emailProxy.RecipientEmail);
        Assert.Equal(ChallengeKey, emailProxy.ChallengeKey);
        Assert.DoesNotContain(ParticipantEmail, string.Join(Environment.NewLine, loggerProvider.Messages),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ChallengeKey, string.Join(Environment.NewLine, loggerProvider.Messages),
            StringComparison.Ordinal);

        await using var noDuplicateDelivery = database.CreateContext();
        var completedHandler = new PrivacyWithdrawalChallengeEmailJobHandler(
            noDuplicateDelivery,
            emailService,
            new CapturingKeyProtection(),
            new BackgroundJobConfiguration(),
            loggerFactory.CreateLogger<PrivacyWithdrawalChallengeEmailJobHandler>());
        await completedHandler.HandleAsync(job, TestContext.Current.CancellationToken);
        Assert.Equal(2, emailProxy.InvocationCount);
    }

    [Fact]
    public async Task ErasureDuringWithdrawalEmailPreparationPreventsProviderHandoff()
    {
        using var database = SqliteTestDatabase.Create();
        var cancellationToken = TestContext.Current.CancellationToken;
        var titleId = await SeedTitleAsync(database);
        var refTest = NewRefTest(titleId, ParticipantEmail);
        var now = DateTime.UtcNow;
        var protectedKey = $"protected:{ChallengeKey}";
        var challenge = PrivacyWithdrawalChallenge.Create(
            ParticipantEmail,
            PrivacyWithdrawalChallenge.HashNormalizedEmail(
                PrivacyWithdrawalChallenge.NormalizeEmail(ParticipantEmail)),
            ChallengeKey,
            protectedKey,
            now,
            now.AddHours(2),
            matchingRefTestCount: 1);
        var payload = JsonSerializer.Serialize(
            new PrivacyWithdrawalChallengeEmailPayload(challenge.Id),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var job = Job.Create(JobType.PrivacyWithdrawalChallengeEmail, payload);

        await using (var seed = database.CreateContext())
        {
            seed.RefTests.Add(refTest);
            seed.PrivacyWithdrawalChallenges.Add(challenge);
            seed.Jobs.Add(job);
            await seed.SaveChangesAsync(cancellationToken);
        }

        var emailService = DispatchProxy.Create<IEmailService, RecordingPrivacyWithdrawalEmailProxy>();
        var emailProxy = (RecordingPrivacyWithdrawalEmailProxy)(object)emailService;
        emailProxy.BeforeFinalCheck = async () =>
        {
            await using var erasureContext = database.CreateContext();
            var current = await erasureContext.RefTests
                .SingleAsync(candidate => candidate.Id == refTest.Id, cancellationToken);
            await new RefTestPrivacyErasureService(erasureContext)
                .EraseAsync(current, ErasureInitiator.Operator, cancellationToken);
        };

        await using (var handlingContext = database.CreateContext())
        {
            var handler = new PrivacyWithdrawalChallengeEmailJobHandler(
                handlingContext,
                emailService,
                new CapturingKeyProtection(),
                new BackgroundJobConfiguration(),
                NullLogger<PrivacyWithdrawalChallengeEmailJobHandler>.Instance);
            await handler.HandleAsync(job, cancellationToken);
        }

        Assert.Equal(1, emailProxy.InvocationCount);
        Assert.Equal(0, emailProxy.ProviderSubmissionCount);
        await using var verification = database.CreateContext();
        var erasedRefTest = await verification.RefTests
            .SingleAsync(candidate => candidate.Id == refTest.Id, cancellationToken);
        var erasedChallenge = await verification.PrivacyWithdrawalChallenges
            .SingleAsync(candidate => candidate.Id == challenge.Id, cancellationToken);
        var cancelledJob = await verification.Jobs.SingleAsync(candidate => candidate.Id == job.Id, cancellationToken);
        Assert.True(erasedRefTest.IsAnonymized);
        Assert.Null(erasedRefTest.EmailLookupKey);
        Assert.Equal(string.Empty, erasedChallenge.Email);
        Assert.Null(erasedChallenge.ProtectedDeliveryKey);
        Assert.Equal(JobStatus.Cancelled, cancelledJob.Status);
        Assert.Empty(cancelledJob.Payload);
    }

    [Fact]
    public async Task FinalChallengeEmailFailureExpiresChallengeAndRepeatRequestQueuesFreshDelivery()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        await using (var seed = database.CreateContext())
        {
            seed.RefTests.Add(NewRefTest(titleId, ParticipantEmail));
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var jobConfiguration = new BackgroundJobConfiguration { MaxAttempts = 2 };
        var keyProtection = new CapturingKeyProtection();
        await using (var requestContext = database.CreateContext())
        {
            await NewRequestService(
                    requestContext,
                    keyProtection,
                    backgroundJobConfiguration: jobConfiguration)
                .RequestAsync(ParticipantEmail, TestContext.Current.CancellationToken);
        }

        Guid challengeId;
        Guid jobId;
        string initialKeyHash;
        string initialProtectedKey;
        string normalizedEmailHash;
        await using (var initialState = database.CreateContext())
        {
            var challenge = await initialState.PrivacyWithdrawalChallenges
                .SingleAsync(TestContext.Current.CancellationToken);
            var job = await initialState.Jobs.SingleAsync(TestContext.Current.CancellationToken);
            challengeId = challenge.Id;
            jobId = job.Id;
            initialKeyHash = challenge.KeyHash!;
            initialProtectedKey = challenge.ProtectedDeliveryKey!;
            normalizedEmailHash = challenge.NormalizedEmailHash;
        }

        var initialKey = keyProtection.ProtectedKeys.Single();
        var providerFailureMessage = $"Provider echoed {ParticipantEmail} {initialKey}.";
        var emailService = DispatchProxy.Create<IEmailService, RecordingPrivacyWithdrawalEmailProxy>();
        var emailProxy = (RecordingPrivacyWithdrawalEmailProxy)(object)emailService;
        emailProxy.Failure = new InvalidOperationException(providerFailureMessage);
        using var loggerProvider = new CapturingLoggerProvider();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        services.AddScoped<RefTestManagementContext>(_ => database.CreateContext());
        services.AddSingleton<IEmailService>(emailService);
        services.AddSingleton<IPersonalDataExportKeyProtection>(keyProtection);
        services.AddSingleton(jobConfiguration);
        services.AddKeyedScoped<IJobHandler, PrivacyWithdrawalChallengeEmailJobHandler>(
            JobType.PrivacyWithdrawalChallengeEmail);
        using var serviceProvider = services.BuildServiceProvider();
        var backgroundJobLogger = serviceProvider.GetRequiredService<ILogger<BackgroundJobService>>();

        for (var attempt = 1; attempt <= jobConfiguration.MaxAttempts; attempt++)
        {
            await using (var processingScope = serviceProvider.CreateAsyncScope())
            {
                var context = processingScope.ServiceProvider.GetRequiredService<RefTestManagementContext>();
                var job = await context.Jobs.SingleAsync(
                    candidate => candidate.Id == jobId,
                    TestContext.Current.CancellationToken);
                Assert.Equal(attempt - 1, job.Attempts);
                job.MarkAsProcessing(TimeSpan.FromMinutes(5));

                await BackgroundJobService.ProcessJobAsync(
                    job,
                    processingScope.ServiceProvider,
                    context,
                    backgroundJobLogger,
                    jobConfiguration.MaxAttempts,
                    TestContext.Current.CancellationToken);

                Assert.Equal(attempt, job.Attempts);
                Assert.Equal(
                    attempt == jobConfiguration.MaxAttempts ? JobStatus.Failed : JobStatus.Pending,
                    job.Status);
                Assert.Equal("Privacy-withdrawal verification email delivery failed.", job.ErrorMessage);
                Assert.DoesNotContain(ParticipantEmail, job.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(initialKey, job.ErrorMessage!, StringComparison.Ordinal);

                var challenge = await context.PrivacyWithdrawalChallenges
                    .SingleAsync(candidate => candidate.Id == challengeId, TestContext.Current.CancellationToken);
                Assert.Equal(attempt, challenge.DeliveryAttemptCount);
                if (attempt < jobConfiguration.MaxAttempts)
                {
                    Assert.True(challenge.IsPendingAt(DateTime.UtcNow));
                    Assert.NotNull(challenge.ProtectedDeliveryKey);
                }
                else
                {
                    Assert.False(challenge.IsPendingAt(DateTime.UtcNow));
                    Assert.True(challenge.ExpiresAt <= DateTime.UtcNow);
                }
            }

            if (attempt >= jobConfiguration.MaxAttempts)
                continue;

            await using var retryRequestContext = database.CreateContext();
            await NewRequestService(
                    retryRequestContext,
                    keyProtection,
                    backgroundJobConfiguration: jobConfiguration)
                .RequestAsync("Ada@Example.Org", TestContext.Current.CancellationToken);
            Assert.Single(await retryRequestContext.PrivacyWithdrawalChallenges
                .ToListAsync(TestContext.Current.CancellationToken));
            Assert.Single(await retryRequestContext.Jobs.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Single(keyProtection.ProtectedKeys);
        }

        Assert.Equal(jobConfiguration.MaxAttempts, emailProxy.InvocationCount);
        Assert.Equal(ParticipantEmail, emailProxy.RecipientEmail);
        Assert.Equal(initialKey, emailProxy.ChallengeKey);

        await using (var expiredState = database.CreateContext())
        {
            var expired = await expiredState.PrivacyWithdrawalChallenges
                .SingleAsync(candidate => candidate.Id == challengeId, TestContext.Current.CancellationToken);
            Assert.True(expired.ExpiresAt <= DateTime.UtcNow);
            Assert.Equal(jobConfiguration.MaxAttempts, expired.DeliveryAttemptCount);
            Assert.Equal(initialKeyHash, expired.KeyHash);
            Assert.Equal(initialProtectedKey, expired.ProtectedDeliveryKey);
            Assert.Equal(normalizedEmailHash, expired.NormalizedEmailHash);
        }

        await using (var renewalContext = database.CreateContext())
        {
            await NewRequestService(
                    renewalContext,
                    keyProtection,
                    backgroundJobConfiguration: jobConfiguration)
                .RequestAsync("ADA@EXAMPLE.ORG", TestContext.Current.CancellationToken);
            Assert.Equal(
                2,
                await renewalContext.Jobs.CountAsync(TestContext.Current.CancellationToken));
        }

        await using var verificationContext = database.CreateContext();
        var renewed = await verificationContext.PrivacyWithdrawalChallenges
            .SingleAsync(candidate => candidate.Id == challengeId, TestContext.Current.CancellationToken);
        Assert.Equal(normalizedEmailHash, renewed.NormalizedEmailHash);
        Assert.True(renewed.ExpiresAt > DateTime.UtcNow);
        Assert.Equal(0, renewed.DeliveryAttemptCount);
        Assert.Null(renewed.ChallengeEmailSentAt);
        Assert.NotEqual(initialKeyHash, renewed.KeyHash);
        Assert.Equal(PrivacyWithdrawalChallenge.HashKey(keyProtection.ProtectedKeys[1]), renewed.KeyHash);
        Assert.NotEqual(initialProtectedKey, renewed.ProtectedDeliveryKey);
        Assert.Equal(2, keyProtection.ProtectedKeys.Count);

        var jobs = await verificationContext.Jobs.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, jobs.Count);
        var failedJob = jobs.Single(candidate => candidate.Id == jobId);
        Assert.Equal(JobStatus.Failed, failedJob.Status);
        Assert.Equal(jobConfiguration.MaxAttempts, failedJob.Attempts);
        var freshJob = jobs.Single(candidate => candidate.Id != jobId);
        Assert.Equal(JobType.PrivacyWithdrawalChallengeEmail, freshJob.JobType);
        Assert.Equal(JobStatus.Pending, freshJob.Status);
        using var payload = JsonDocument.Parse(freshJob.Payload);
        Assert.Equal(challengeId, payload.RootElement.GetProperty("challengeId").GetGuid());

        var logs = string.Join(Environment.NewLine, loggerProvider.Messages);
        Assert.DoesNotContain(ParticipantEmail, logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(initialKey, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(providerFailureMessage, logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentChallengeConsumptionCannotCommitDuplicateBatchesOrJobs()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var refTest = NewRefTest(titleId, ParticipantEmail);
        var challenge = PrivacyWithdrawalChallenge.Create(
            ParticipantEmail,
            PrivacyWithdrawalChallenge.HashNormalizedEmail(
                PrivacyWithdrawalChallenge.NormalizeEmail(ParticipantEmail)),
            ChallengeKey,
            $"protected:{ChallengeKey}",
            Now,
            Now.AddHours(24),
            matchingRefTestCount: 1);

        await using (var seed = database.CreateContext())
        {
            seed.RefTests.Add(refTest);
            seed.PrivacyWithdrawalChallenges.Add(challenge);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var firstChallenge = await firstContext.PrivacyWithdrawalChallenges
            .SingleAsync(TestContext.Current.CancellationToken);
        var secondChallenge = await secondContext.PrivacyWithdrawalChallenges
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.True(firstChallenge.TryConfirm(ChallengeKey, Now.AddMinutes(1)));
        Assert.True(secondChallenge.TryConfirm(ChallengeKey, Now.AddMinutes(1)));
        StageConfirmedBatch(firstContext, refTest.Id, Now.AddMinutes(1));
        StageConfirmedBatch(secondContext, refTest.Id, Now.AddMinutes(1));

        await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));

        await using var verification = database.CreateContext();
        Assert.Single(await verification.PrivacyWithdrawalBatches.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await verification.PrivacyWithdrawalBatchTargets.ToListAsync(TestContext.Current.CancellationToken));
        var job = await verification.Jobs.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(JobType.PrivacyWithdrawalBatch, job.JobType);
    }

    [Fact]
    public async Task CleanupClearsExpiredChallengesAndOnlyDeletesTargetsOfCompletedBatches()
    {
        using var database = SqliteTestDatabase.Create();
        var expired = PrivacyWithdrawalChallenge.Create(
            ParticipantEmail,
            PrivacyWithdrawalChallenge.HashNormalizedEmail(
                PrivacyWithdrawalChallenge.NormalizeEmail(ParticipantEmail)),
            ChallengeKey,
            $"protected:{ChallengeKey}",
            Now.AddHours(-25),
            Now.AddHours(-1),
            matchingRefTestCount: 1);
        var active = PrivacyWithdrawalChallenge.Create(
            "grace@example.org",
            PrivacyWithdrawalChallenge.HashNormalizedEmail(
                PrivacyWithdrawalChallenge.NormalizeEmail("grace@example.org")),
            new string('G', 43),
            "protected:active",
            Now.AddHours(-1),
            Now.AddHours(23),
            matchingRefTestCount: 1);
        var completedBatch = PrivacyWithdrawalBatch.Create(Now, targetCount: 1);
        completedBatch.MarkCompleted(Now.AddMinutes(1));
        var incompleteBatch = PrivacyWithdrawalBatch.Create(Now, targetCount: 2);
        var completedTargetForCompletedBatch = PrivacyWithdrawalBatchTarget.Create(
            completedBatch.Id,
            Guid.NewGuid());
        completedTargetForCompletedBatch.MarkCompleted(Now.AddMinutes(1));
        var completedTargetForActiveBatch = PrivacyWithdrawalBatchTarget.Create(
            incompleteBatch.Id,
            Guid.NewGuid());
        completedTargetForActiveBatch.MarkCompleted(Now.AddMinutes(1));
        var pendingTargetForActiveBatch = PrivacyWithdrawalBatchTarget.Create(
            incompleteBatch.Id,
            Guid.NewGuid());
        var exhaustedBatchWithPendingJob = PrivacyWithdrawalBatch.Create(Now, targetCount: 1);
        exhaustedBatchWithPendingJob.MarkCompleted(Now.AddMinutes(1));
        var exhaustedBatchWithFailedJob = PrivacyWithdrawalBatch.Create(Now, targetCount: 1);
        exhaustedBatchWithFailedJob.MarkCompleted(Now.AddMinutes(1));
        var exhaustedBatchWithActiveReplacement = PrivacyWithdrawalBatch.Create(Now, targetCount: 1);
        exhaustedBatchWithActiveReplacement.MarkCompleted(Now.AddMinutes(1));
        var exhaustedTargetWithPendingJob = ExhaustedTarget(exhaustedBatchWithPendingJob.Id);
        var exhaustedTargetWithFailedJob = ExhaustedTarget(exhaustedBatchWithFailedJob.Id);
        var exhaustedTargetWithActiveReplacement = ExhaustedTarget(exhaustedBatchWithActiveReplacement.Id);
        var pendingBatchJob = NewBatchJob(exhaustedBatchWithPendingJob);
        var failedBatchJob = NewBatchJob(exhaustedBatchWithFailedJob);
        failedBatchJob.MarkAsPermanentlyFailed("One or more privacy-withdrawal targets exhausted their retries.");
        var historicalFailedJob = NewBatchJob(exhaustedBatchWithActiveReplacement);
        historicalFailedJob.MarkAsPermanentlyFailed("Earlier replacement failed.");
        var activeReplacementJob = NewBatchJob(exhaustedBatchWithActiveReplacement);
        activeReplacementJob.MarkAsProcessing(TimeSpan.FromMinutes(5));

        await using (var seed = database.CreateContext())
        {
            seed.PrivacyWithdrawalChallenges.AddRange(expired, active);
            seed.PrivacyWithdrawalBatches.AddRange(
                completedBatch,
                incompleteBatch,
                exhaustedBatchWithPendingJob,
                exhaustedBatchWithFailedJob,
                exhaustedBatchWithActiveReplacement);
            seed.PrivacyWithdrawalBatchTargets.AddRange(
                completedTargetForCompletedBatch,
                completedTargetForActiveBatch,
                pendingTargetForActiveBatch,
                exhaustedTargetWithPendingJob,
                exhaustedTargetWithFailedJob,
                exhaustedTargetWithActiveReplacement);
            seed.Jobs.AddRange(pendingBatchJob, failedBatchJob, historicalFailedJob, activeReplacementJob);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var cleanup = database.CreateContext())
        {
            Assert.Equal(
                1,
                await PrivacyWithdrawalCleanupService.ClearExpiredChallengesAsync(
                    cleanup,
                    Now,
                    TestContext.Current.CancellationToken));
            Assert.Equal(
                2,
                await PrivacyWithdrawalCleanupService.ClearCompletedBatchTargetsAsync(
                    cleanup,
                    TestContext.Current.CancellationToken));
        }

        await using var verification = database.CreateContext();
        var clearedExpired = await verification.PrivacyWithdrawalChallenges
            .SingleAsync(challenge => challenge.Id == expired.Id, TestContext.Current.CancellationToken);
        var preservedActive = await verification.PrivacyWithdrawalChallenges
            .SingleAsync(challenge => challenge.Id == active.Id, TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, clearedExpired.Email);
        Assert.Null(clearedExpired.KeyHash);
        Assert.Equal("grace@example.org", preservedActive.Email);
        Assert.NotNull(preservedActive.KeyHash);
        var remainingTargets = await verification.PrivacyWithdrawalBatchTargets
            .Select(target => target.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(completedTargetForCompletedBatch.Id, remainingTargets);
        Assert.Contains(completedTargetForActiveBatch.Id, remainingTargets);
        Assert.Contains(pendingTargetForActiveBatch.Id, remainingTargets);
        Assert.Contains(exhaustedTargetWithPendingJob.Id, remainingTargets);
        Assert.Contains(exhaustedTargetWithActiveReplacement.Id, remainingTargets);
        Assert.DoesNotContain(exhaustedTargetWithFailedJob.Id, remainingTargets);
    }

    [Fact]
    public async Task CleanupContinuesRetentionPurgesWhenReconciliationFails()
    {
        using var database = SqliteTestDatabase.Create();
        var expired = PrivacyWithdrawalChallenge.Create(
            ParticipantEmail,
            PrivacyWithdrawalChallenge.HashNormalizedEmail(
                PrivacyWithdrawalChallenge.NormalizeEmail(ParticipantEmail)),
            ChallengeKey,
            $"protected:{ChallengeKey}",
            Now.AddHours(-25),
            Now.AddHours(-1),
            matchingRefTestCount: 1);
        var completedBatch = PrivacyWithdrawalBatch.Create(Now, targetCount: 1);
        completedBatch.MarkCompleted(Now.AddMinutes(1));
        var completedTarget = PrivacyWithdrawalBatchTarget.Create(completedBatch.Id, Guid.NewGuid());
        completedTarget.MarkCompleted(Now.AddMinutes(1));

        await using (var seed = database.CreateContext())
        {
            seed.PrivacyWithdrawalChallenges.Add(expired);
            seed.PrivacyWithdrawalBatches.Add(completedBatch);
            seed.PrivacyWithdrawalBatchTargets.Add(completedTarget);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var loggerProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
        var requestService = new StubPrivacyWithdrawalRequestService(
            _ => Task.FromException<int>(
                new InvalidOperationException($"Provider failure for {ParticipantEmail}.")));

        await using (var cleanup = database.CreateContext())
        {
            await PrivacyWithdrawalCleanupService.RunCleanupCycleAsync(
                cleanup,
                requestService,
                Now,
                loggerFactory.CreateLogger<PrivacyWithdrawalCleanupService>(),
                TestContext.Current.CancellationToken);
        }

        await using var verification = database.CreateContext();
        var clearedChallenge = await verification.PrivacyWithdrawalChallenges
            .SingleAsync(challenge => challenge.Id == expired.Id, TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, clearedChallenge.Email);
        Assert.Null(clearedChallenge.KeyHash);
        Assert.Null(clearedChallenge.ProtectedDeliveryKey);
        Assert.False(await verification.PrivacyWithdrawalBatchTargets
            .AnyAsync(target => target.Id == completedTarget.Id, TestContext.Current.CancellationToken));

        var logs = string.Join(Environment.NewLine, loggerProvider.Messages);
        Assert.Contains("Privacy-withdrawal reconciliation failed.", logs, StringComparison.Ordinal);
        Assert.DoesNotContain(ParticipantEmail, logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Provider failure", logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CleanupDoesNotFlushStagedReconciliationChangesWhenReconciliationFails()
    {
        using var database = SqliteTestDatabase.Create();
        var expired = PrivacyWithdrawalChallenge.Create(
            ParticipantEmail,
            PrivacyWithdrawalChallenge.HashNormalizedEmail(
                PrivacyWithdrawalChallenge.NormalizeEmail(ParticipantEmail)),
            ChallengeKey,
            $"protected:{ChallengeKey}",
            Now.AddHours(-25),
            Now.AddHours(-1),
            matchingRefTestCount: 1);
        var completedBatch = PrivacyWithdrawalBatch.Create(Now, targetCount: 1);
        completedBatch.MarkCompleted(Now.AddMinutes(1));
        var completedTarget = PrivacyWithdrawalBatchTarget.Create(completedBatch.Id, Guid.NewGuid());
        completedTarget.MarkCompleted(Now.AddMinutes(1));
        var incompleteBatch = PrivacyWithdrawalBatch.Create(Now, targetCount: 1);
        var pendingTarget = PrivacyWithdrawalBatchTarget.Create(incompleteBatch.Id, Guid.NewGuid());
        pendingTarget.TryStartAttempt(Now);

        await using (var seed = database.CreateContext())
        {
            seed.PrivacyWithdrawalChallenges.Add(expired);
            seed.PrivacyWithdrawalBatches.AddRange(completedBatch, incompleteBatch);
            seed.PrivacyWithdrawalBatchTargets.AddRange(completedTarget, pendingTarget);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var loggerProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
        var stagedEntryCount = 0;

        // The hosted service resolves the request service and the retention queries from one
        // scoped context, so a failed reconciliation must not leave its uncommitted staging behind
        // for the retention saves to flush outside the transaction that validated it.
        await using (var cleanup = database.CreateContext())
        {
            var requestService = new StubPrivacyWithdrawalRequestService(async cancellationToken =>
            {
                var batch = await cleanup.PrivacyWithdrawalBatches
                    .SingleAsync(candidate => candidate.Id == incompleteBatch.Id, cancellationToken);
                var target = await cleanup.PrivacyWithdrawalBatchTargets
                    .SingleAsync(candidate => candidate.Id == pendingTarget.Id, cancellationToken);

                // Mirror a replacement-job recovery: a new job, the batch's latest-job pointer,
                // and a recorded target failure, all still unsaved when the failure occurs.
                cleanup.Jobs.Add(NewBatchJob(batch));
                target.RecordProcessingFailure(Now);
                stagedEntryCount = cleanup.ChangeTracker.Entries()
                    .Count(entry => entry.State is EntityState.Added or EntityState.Modified);

                // Stands in for a commit, deadlock, or concurrency failure after the rollback.
                throw new InvalidOperationException($"Commit failed for {ParticipantEmail}.");
            });

            await PrivacyWithdrawalCleanupService.RunCleanupCycleAsync(
                cleanup,
                requestService,
                Now,
                loggerFactory.CreateLogger<PrivacyWithdrawalCleanupService>(),
                TestContext.Current.CancellationToken);
        }

        Assert.Equal(3, stagedEntryCount);

        await using var verification = database.CreateContext();
        var clearedChallenge = await verification.PrivacyWithdrawalChallenges
            .SingleAsync(challenge => challenge.Id == expired.Id, TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, clearedChallenge.Email);
        Assert.Null(clearedChallenge.KeyHash);
        Assert.Null(clearedChallenge.ProtectedDeliveryKey);

        // Only the completed batch's target is purged; the failed reconciliation changed nothing.
        var remainingTarget = await verification.PrivacyWithdrawalBatchTargets
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(pendingTarget.Id, remainingTarget.Id);
        Assert.Equal(1, remainingTarget.AttemptCount);
        Assert.Null(remainingTarget.NextAttemptAt);
        Assert.Null(remainingTarget.FailureCode);
        Assert.Empty(await verification.Jobs.ToListAsync(TestContext.Current.CancellationToken));
        var persistedBatch = await verification.PrivacyWithdrawalBatches
            .SingleAsync(batch => batch.Id == incompleteBatch.Id, TestContext.Current.CancellationToken);
        Assert.Null(persistedBatch.LatestJobId);
        Assert.Null(persistedBatch.CompletedAt);

        var logs = string.Join(Environment.NewLine, loggerProvider.Messages);
        Assert.Contains("Privacy-withdrawal reconciliation failed.", logs, StringComparison.Ordinal);
        Assert.DoesNotContain(ParticipantEmail, logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Commit failed", logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CleanupCyclePropagatesShutdownCancellationDuringReconciliation()
    {
        using var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();
        using var loggerProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var requestService = new StubPrivacyWithdrawalRequestService(
            Task.FromCanceled<int>);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PrivacyWithdrawalCleanupService.RunCleanupCycleAsync(
                context,
                requestService,
                Now,
                loggerFactory.CreateLogger<PrivacyWithdrawalCleanupService>(),
                cancellation.Token));

        Assert.DoesNotContain(
            "Privacy-withdrawal reconciliation failed.",
            string.Join(Environment.NewLine, loggerProvider.Messages),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("terminal")]
    public async Task ScheduledReconciliationRecoversMissingOrTerminalJobsOnlyOnce(string jobState)
    {
        using var database = SqliteTestDatabase.Create();
        var batch = PrivacyWithdrawalBatch.Create(Now, targetCount: 1);
        var target = PrivacyWithdrawalBatchTarget.Create(batch.Id, Guid.NewGuid());
        var terminalJob = NewBatchJob(batch);
        if (jobState == "terminal")
            terminalJob.MarkAsFailed("Simulated terminal failure", maxAttempts: 1);

        await using (var seed = database.CreateContext())
        {
            seed.PrivacyWithdrawalBatches.Add(batch);
            seed.PrivacyWithdrawalBatchTargets.Add(target);
            if (jobState == "terminal")
                seed.Jobs.Add(terminalJob);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var service = NewRequestService(context, new CapturingKeyProtection());
            Assert.Equal(
                1,
                await service.ReconcileIncompleteBatchesAsync(TestContext.Current.CancellationToken));
            Assert.Equal(
                0,
                await service.ReconcileIncompleteBatchesAsync(TestContext.Current.CancellationToken));
        }

        await using var verification = database.CreateContext();
        var jobs = await verification.Jobs.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(jobState == "missing" ? 1 : 2, jobs.Count);
        var replacement = Assert.Single(jobs, job => job.Status == JobStatus.Pending);
        Assert.Equal(batch.Id, replacement.PrivacyWithdrawalBatchId);
        Assert.Equal(replacement.Id, (await verification.PrivacyWithdrawalBatches
            .SingleAsync(TestContext.Current.CancellationToken)).LatestJobId);
        Assert.Null((await verification.PrivacyWithdrawalBatchTargets
            .SingleAsync(TestContext.Current.CancellationToken)).CompletedAt);

        if (jobState == "terminal")
            Assert.Contains(jobs, job => job.Id == terminalJob.Id && job.Status == JobStatus.Failed);
    }

    [Fact]
    public async Task ScheduledReconciliationLeavesActiveLeasesAlone()
    {
        using var database = SqliteTestDatabase.Create();
        var batch = PrivacyWithdrawalBatch.Create(Now, targetCount: 1);
        var target = PrivacyWithdrawalBatchTarget.Create(batch.Id, Guid.NewGuid());
        var job = NewBatchJob(batch);
        job.MarkAsProcessing(TimeSpan.FromMinutes(10));
        for (var attempt = 0; attempt < new BackgroundJobConfiguration().MaxAttempts; attempt++)
            job.MarkAsProcessing(TimeSpan.FromMinutes(10));
        var originalLock = job.LockedUntil;
        var originalAttempts = job.Attempts;

        await using (var seed = database.CreateContext())
        {
            seed.PrivacyWithdrawalBatches.Add(batch);
            seed.PrivacyWithdrawalBatchTargets.Add(target);
            seed.Jobs.Add(job);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var service = NewRequestService(context, new CapturingKeyProtection());
            Assert.Equal(
                0,
                await service.ReconcileIncompleteBatchesAsync(TestContext.Current.CancellationToken));
        }

        await using var verification = database.CreateContext();
        var activeJob = await verification.Jobs.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Processing, activeJob.Status);
        Assert.Equal(originalAttempts, activeJob.Attempts);
        Assert.Equal(originalLock, activeJob.LockedUntil);
        Assert.Equal(batch.LatestJobId, activeJob.Id);
    }

    [Fact]
    public async Task ScheduledReconciliationIgnoresCompletedTargetsAndCompletedBatches()
    {
        using var database = SqliteTestDatabase.Create();
        var completedTargetBatch = PrivacyWithdrawalBatch.Create(Now, targetCount: 1);
        var completedTarget = PrivacyWithdrawalBatchTarget.Create(
            completedTargetBatch.Id,
            Guid.NewGuid());
        completedTarget.MarkCompleted(Now.AddMinutes(1));

        var completedBatch = PrivacyWithdrawalBatch.Create(Now, targetCount: 1);
        completedBatch.MarkCompleted(Now.AddMinutes(2));
        var pendingTargetInCompletedBatch = PrivacyWithdrawalBatchTarget.Create(
            completedBatch.Id,
            Guid.NewGuid());

        await using (var seed = database.CreateContext())
        {
            seed.PrivacyWithdrawalBatches.AddRange(completedTargetBatch, completedBatch);
            seed.PrivacyWithdrawalBatchTargets.AddRange(completedTarget, pendingTargetInCompletedBatch);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var service = NewRequestService(context, new CapturingKeyProtection());
            Assert.Equal(
                0,
                await service.ReconcileIncompleteBatchesAsync(TestContext.Current.CancellationToken));
        }

        await using var verification = database.CreateContext();
        Assert.Empty(await verification.Jobs.ToListAsync(TestContext.Current.CancellationToken));
        var completedTargetAfter = await verification.PrivacyWithdrawalBatchTargets
            .SingleAsync(candidate => candidate.Id == completedTarget.Id, TestContext.Current.CancellationToken);
        Assert.Equal(Now.AddMinutes(1), completedTargetAfter.CompletedAt);
        var completedTargetBatchAfter = await verification.PrivacyWithdrawalBatches
            .SingleAsync(candidate => candidate.Id == completedTargetBatch.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(completedTargetBatchAfter.CompletedAt);

        var completedBatchAfter = await verification.PrivacyWithdrawalBatches
            .SingleAsync(candidate => candidate.Id == completedBatch.Id, TestContext.Current.CancellationToken);
        var targetInCompletedBatchAfter = await verification.PrivacyWithdrawalBatchTargets
            .SingleAsync(
                candidate => candidate.Id == pendingTargetInCompletedBatch.Id,
                TestContext.Current.CancellationToken);
        Assert.Equal(Now.AddMinutes(2), completedBatchAfter.CompletedAt);
        Assert.Null(targetInCompletedBatchAfter.CompletedAt);
    }

    [Fact]
    public async Task ScheduledReconciliationCompletesBatchWithExhaustedTargetsAndPreservesEscalation()
    {
        using var database = SqliteTestDatabase.Create();
        var batch = PrivacyWithdrawalBatch.Create(Now, targetCount: 2);
        var completedTarget = PrivacyWithdrawalBatchTarget.Create(batch.Id, Guid.NewGuid());
        Assert.True(completedTarget.MarkCompleted(Now.AddMinutes(1)));
        var exhaustedTarget = ExhaustedTarget(batch.Id);
        var interruptedJob = NewBatchJob(batch);
        interruptedJob.MarkAsProcessing(TimeSpan.FromMinutes(5));
        const string escalationMessage = "One or more privacy-withdrawal targets exhausted their retries.";

        await using (var seed = database.CreateContext())
        {
            seed.PrivacyWithdrawalBatches.Add(batch);
            seed.PrivacyWithdrawalBatchTargets.AddRange(completedTarget, exhaustedTarget);
            seed.Jobs.Add(interruptedJob);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var auditInterceptor = new AuditSaveChangesInterceptor(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new AuditLogOptions());
        await using (var context = database.CreateContext(auditInterceptor))
        {
            var service = NewRequestService(context, new CapturingKeyProtection());

            Assert.Equal(
                0,
                await service.ReconcileIncompleteBatchesAsync(TestContext.Current.CancellationToken));

            var completionAudit = await context.AuditEvents
                .AsNoTracking()
                .SingleAsync(
                    auditEvent => auditEvent.Type == "PrivacyWithdrawalBatchCompleted",
                    TestContext.Current.CancellationToken);
            using var completionData = JsonDocument.Parse(completionAudit.Data!);
            Assert.Equal(
                ["completedTargetCount", "exhaustedTargetCount"],
                completionData.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
            Assert.Equal(1, completionData.RootElement.GetProperty("completedTargetCount").GetInt32());
            Assert.Equal(1, completionData.RootElement.GetProperty("exhaustedTargetCount").GetInt32());
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<RefTestManagementContext>(_ => database.CreateContext());
        services.AddKeyedScoped<IJobHandler, PrivacyWithdrawalBatchJobHandler>(
            JobType.PrivacyWithdrawalBatch);
        using var serviceProvider = services.BuildServiceProvider();
        using var processingScope = serviceProvider.CreateScope();
        var processingContext = processingScope.ServiceProvider.GetRequiredService<RefTestManagementContext>();
        var recoveredJob = await processingContext.Jobs.SingleAsync(
            candidate => candidate.Id == interruptedJob.Id,
            TestContext.Current.CancellationToken);
        await BackgroundJobService.ProcessJobAsync(
            recoveredJob,
            processingScope.ServiceProvider,
            processingContext,
            NullLogger.Instance,
            maxAttempts: 3,
            TestContext.Current.CancellationToken);

        await using var verification = database.CreateContext();
        var recoveredBatch = await verification.PrivacyWithdrawalBatches.SingleAsync(
            candidate => candidate.Id == batch.Id,
            TestContext.Current.CancellationToken);
        Assert.NotNull(recoveredBatch.CompletedAt);
        var retainedJob = await verification.Jobs.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Failed, retainedJob.Status);
        Assert.Equal(escalationMessage, retainedJob.ErrorMessage);
    }

    [Fact]
    public async Task ScheduledRecoveryRacesParticipantRequestWithoutDuplicateJobs()
    {
        using var database = new ConcurrentSqliteTestDatabase();
        RefTest participant;
        string participantToken;
        PrivacyWithdrawalBatch batch;

        await using (var seed = database.CreateContext())
        {
            var title = RefTestTitle.Create("Season 2026");
            participant = NewRefTest(title.Id, ParticipantEmail);
            participantToken = participant.GetIssuedToken();
            batch = PrivacyWithdrawalBatch.Create(Now, targetCount: 1);
            seed.RefTestTitles.Add(title);
            seed.RefTests.Add(participant);
            seed.PrivacyWithdrawalBatches.Add(batch);
            seed.PrivacyWithdrawalBatchTargets.Add(
                PrivacyWithdrawalBatchTarget.Create(batch.Id, participant.Id));
            var terminalJob = NewBatchJob(batch);
            terminalJob.MarkAsFailed("Simulated terminal failure", maxAttempts: 1);
            seed.Jobs.Add(terminalJob);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var ready = new CountdownEvent(2);
        using var start = new ManualResetEventSlim();
        Task<bool> RequestAsync() => Task.Run(async () =>
        {
            ready.Signal();
            start.Wait(TestContext.Current.CancellationToken);
            await using var context = database.CreateContext();
            return await NewRequestService(context, new CapturingKeyProtection())
                .RequestForParticipantAsync(participantToken, TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);
        Task<int> SweepAsync() => Task.Run(async () =>
        {
            ready.Signal();
            start.Wait(TestContext.Current.CancellationToken);
            await using var context = database.CreateContext();
            return await NewRequestService(context, new CapturingKeyProtection())
                .ReconcileIncompleteBatchesAsync(TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);

        var participantRequest = RequestAsync();
        var scheduledSweep = SweepAsync();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        start.Set();
        await Task.WhenAll(participantRequest, scheduledSweep);
        Assert.True(await participantRequest);

        await using var verification = database.CreateContext();
        var jobs = await verification.Jobs.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, jobs.Count);
        var replacement = Assert.Single(jobs, job => job.Status == JobStatus.Pending);
        Assert.Equal(batch.Id, replacement.PrivacyWithdrawalBatchId);
        Assert.Equal(replacement.Id, (await verification.PrivacyWithdrawalBatches
            .SingleAsync(TestContext.Current.CancellationToken)).LatestJobId);
    }

    [Fact]
    public async Task BatchWorkerContinuesAfterFailureRetriesDurablyAndPublishesOnlyAfterCommit()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var refTests = new[]
        {
            NewRefTest(titleId, ParticipantEmail),
            NewRefTest(titleId, "grace@example.org"),
            NewRefTest(titleId, "already-erased@example.org")
        };
        var batch = PrivacyWithdrawalBatch.Create(DateTime.UtcNow, refTests.Length);
        var targetRows = refTests
            .Select(refTest => PrivacyWithdrawalBatchTarget.Create(batch.Id, refTest.Id))
            .ToArray();
        var job = NewBatchJob(batch);
        var payload = job.Payload;
        job.MarkAsProcessing(TimeSpan.FromMinutes(5));
        var failureState = new WorkerFailureState(refTests[0].Id, ParticipantEmail, ChallengeKey);

        await using (var seed = database.CreateContext())
        {
            seed.RefTests.AddRange(refTests);
            seed.PrivacyWithdrawalBatches.Add(batch);
            seed.PrivacyWithdrawalBatchTargets.AddRange(targetRows);
            seed.Jobs.Add(job);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var concurrentErasure = database.CreateContext())
        {
            var alreadyErased = await concurrentErasure.RefTests.SingleAsync(
                refTest => refTest.Id == refTests[2].Id,
                TestContext.Current.CancellationToken);
            alreadyErased.Anonymize();
            await concurrentErasure.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var loggerProvider = new CapturingLoggerProvider();
        var subscription = DispatchProxy.Create<IRefTestSubscriptionService, RecordingSubscriptionProxy>();
        var subscriptionProxy = (RecordingSubscriptionProxy)(object)subscription;
        subscriptionProxy.OnAnonymized = async refTestId =>
        {
            await using var verification = database.CreateContext();
            var erased = await verification.RefTests.SingleAsync(
                candidate => candidate.Id == refTestId,
                TestContext.Current.CancellationToken);
            Assert.True(erased.IsAnonymized);
            failureState.PublishedRefTestIds.Add(refTestId);
        };

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        services.AddScoped<RefTestManagementContext>(_ => database.CreateContext());
        services.AddScoped<IRefTestPrivacyErasureService>(provider =>
            new FailOncePrivacyErasureService(
                provider.GetRequiredService<RefTestManagementContext>(),
                failureState));
        services.AddSingleton<IRefTestSubscriptionService>(subscription);
        services.AddKeyedScoped<IJobHandler, PrivacyWithdrawalBatchJobHandler>(
            JobType.PrivacyWithdrawalBatch);
        using var serviceProvider = services.BuildServiceProvider();
        using var processingScope = serviceProvider.CreateScope();

        await using var jobContext = database.CreateContext();
        var trackedJob = await jobContext.Jobs.SingleAsync(TestContext.Current.CancellationToken);
        await BackgroundJobService.ProcessJobAsync(
            trackedJob,
            processingScope.ServiceProvider,
            jobContext,
            NullLogger.Instance,
            maxAttempts: 3,
            TestContext.Current.CancellationToken);

        Assert.Equal(JobStatus.Completed, trackedJob.Status);
        Assert.NotEqual(JobStatus.Cancelled, trackedJob.Status);
        Assert.Null(trackedJob.ErrorMessage);

        await using (var firstAttemptVerification = database.CreateContext())
        {
            var firstRefTest = await firstAttemptVerification.RefTests.SingleAsync(
                refTest => refTest.Id == refTests[0].Id,
                TestContext.Current.CancellationToken);
            var secondRefTest = await firstAttemptVerification.RefTests.SingleAsync(
                refTest => refTest.Id == refTests[1].Id,
                TestContext.Current.CancellationToken);
            Assert.False(firstRefTest.IsAnonymized);
            Assert.True(secondRefTest.IsAnonymized);

            var progress = await firstAttemptVerification.PrivacyWithdrawalBatchTargets
                .ToDictionaryAsync(target => target.RefTestId, TestContext.Current.CancellationToken);
            Assert.Null(progress[refTests[0].Id].CompletedAt);
            Assert.NotNull(progress[refTests[0].Id].ErasureStartedAt);
            Assert.Equal(1, progress[refTests[0].Id].AttemptCount);
            Assert.Equal(
                PrivacyWithdrawalTargetFailureCode.ProcessingFailed,
                progress[refTests[0].Id].FailureCode);
            Assert.NotNull(progress[refTests[0].Id].NextAttemptAt);
            Assert.Null(progress[refTests[0].Id].RetryExhaustedAt);
            Assert.NotNull(progress[refTests[1].Id].CompletedAt);
            Assert.NotNull(progress[refTests[2].Id].CompletedAt);
            Assert.Null(progress[refTests[2].Id].ErasureStartedAt);
        }

        await using var retryJobContext = database.CreateContext();
        await retryJobContext.PrivacyWithdrawalBatchTargets
            .Where(target => target.RefTestId == refTests[0].Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(target => target.NextAttemptAt, DateTime.UtcNow.AddMinutes(-1))
                    .SetProperty(target => target.Version, target => target.Version + 1),
                TestContext.Current.CancellationToken);
        var retryJob = Job.Create(
            JobType.PrivacyWithdrawalBatch,
            payload,
            privacyWithdrawalBatchId: batch.Id);
        retryJob.MarkAsProcessing(TimeSpan.FromMinutes(5));
        retryJobContext.Jobs.Add(retryJob);
        await retryJobContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await BackgroundJobService.ProcessJobAsync(
            retryJob,
            processingScope.ServiceProvider,
            retryJobContext,
            NullLogger.Instance,
            maxAttempts: 3,
            TestContext.Current.CancellationToken);

        Assert.Equal(JobStatus.Completed, retryJob.Status);
        Assert.Equal(2, failureState.PublishedRefTestIds.Count);
        Assert.Equal(3, failureState.ErasureContextIds.Distinct().Count());

        await using (var finalVerification = database.CreateContext())
        {
            Assert.All(
                await finalVerification.RefTests.ToListAsync(TestContext.Current.CancellationToken),
                refTest => Assert.True(refTest.IsAnonymized));
            var completedBatch = await finalVerification.PrivacyWithdrawalBatches
                .SingleAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(completedBatch.CompletedAt);
            Assert.All(
                await finalVerification.PrivacyWithdrawalBatchTargets.ToListAsync(
                    TestContext.Current.CancellationToken),
                target => Assert.NotNull(target.CompletedAt));

            var payloadJson = JsonDocument.Parse(retryJob.Payload);
            Assert.Equal(
                ["batchId"],
                payloadJson.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
            Assert.DoesNotContain(refTests[0].Id.ToString(), retryJob.Payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(refTests[1].Id.ToString(), retryJob.Payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(refTests[2].Id.ToString(), retryJob.Payload, StringComparison.OrdinalIgnoreCase);
        }

        var logs = string.Join(Environment.NewLine, loggerProvider.Messages);
        Assert.DoesNotContain(ParticipantEmail, logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ChallengeKey, logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExhaustedWithdrawalTargetCompletesBatchAndEscalatesThroughTheFailedJob()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var refTest = NewRefTest(titleId, ParticipantEmail);
        var batch = PrivacyWithdrawalBatch.Create(DateTime.UtcNow, targetCount: 1);
        var target = PrivacyWithdrawalBatchTarget.Create(batch.Id, refTest.Id);
        var attemptAt = DateTime.UtcNow;
        for (var attempt = 1; attempt <= PrivacyWithdrawalBatchTarget.MaximumAttempts; attempt++)
        {
            Assert.True(target.TryStartAttempt(attemptAt));
            var failedAt = attemptAt.AddSeconds(1);
            Assert.True(target.RecordProcessingFailure(failedAt));
            if (attempt < PrivacyWithdrawalBatchTarget.MaximumAttempts)
                attemptAt = target.NextAttemptAt!.Value;
        }

        var job = NewBatchJob(batch);
        job.MarkAsProcessing(TimeSpan.FromMinutes(5));
        await using (var seed = database.CreateContext())
        {
            seed.RefTests.Add(refTest);
            seed.PrivacyWithdrawalBatches.Add(batch);
            seed.PrivacyWithdrawalBatchTargets.Add(target);
            seed.Jobs.Add(job);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<RefTestManagementContext>(_ => database.CreateContext());
        services.AddKeyedScoped<IJobHandler, PrivacyWithdrawalBatchJobHandler>(
            JobType.PrivacyWithdrawalBatch);
        using var serviceProvider = services.BuildServiceProvider();
        using var processingScope = serviceProvider.CreateScope();
        var context = processingScope.ServiceProvider.GetRequiredService<RefTestManagementContext>();
        var trackedJob = await context.Jobs.SingleAsync(
            candidate => candidate.Id == job.Id,
            TestContext.Current.CancellationToken);

        await BackgroundJobService.ProcessJobAsync(
            trackedJob,
            processingScope.ServiceProvider,
            context,
            NullLogger.Instance,
            maxAttempts: 3,
            TestContext.Current.CancellationToken);

        Assert.Equal(JobStatus.Failed, trackedJob.Status);
        Assert.Equal("One or more privacy-withdrawal targets exhausted their retries.", trackedJob.ErrorMessage);

        await using var verification = database.CreateContext();
        var completedBatch = await verification.PrivacyWithdrawalBatches.SingleAsync(
            candidate => candidate.Id == batch.Id,
            TestContext.Current.CancellationToken);
        var exhaustedTarget = await verification.PrivacyWithdrawalBatchTargets.SingleAsync(
            candidate => candidate.Id == target.Id,
            TestContext.Current.CancellationToken);
        Assert.NotNull(completedBatch.CompletedAt);
        Assert.NotNull(exhaustedTarget.RetryExhaustedAt);
        Assert.Equal(PrivacyWithdrawalTargetFailureCode.ProcessingFailed, exhaustedTarget.FailureCode);
    }

    [Fact]
    public async Task BatchWorkerReloadsARefTestEditedAfterTargetLoadBeforeErasure()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var refTest = NewRefTest(titleId, ParticipantEmail);
        var batch = PrivacyWithdrawalBatch.Create(DateTime.UtcNow, targetCount: 1);
        var target = PrivacyWithdrawalBatchTarget.Create(batch.Id, refTest.Id);

        await using (var seed = database.CreateContext())
        {
            seed.RefTests.Add(refTest);
            seed.PrivacyWithdrawalBatches.Add(batch);
            seed.PrivacyWithdrawalBatchTargets.Add(target);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var editState = new ConcurrentRefTestEditState(database);
        var subscription = DispatchProxy.Create<IRefTestSubscriptionService, RecordingSubscriptionProxy>();
        var subscriptionProxy = (RecordingSubscriptionProxy)(object)subscription;
        subscriptionProxy.OnAnonymized = async refTestId =>
        {
            await using var verification = database.CreateContext();
            var erased = await verification.RefTests.SingleAsync(
                candidate => candidate.Id == refTestId,
                TestContext.Current.CancellationToken);
            Assert.True(erased.IsAnonymized);
            editState.PublishedRefTestIds.Add(refTestId);
        };

        var services = new ServiceCollection();
        services.AddScoped<RefTestManagementContext>(_ => database.CreateContext());
        services.AddSingleton(editState);
        services.AddScoped<IRefTestPrivacyErasureService>(provider =>
            new ConcurrentEditingPrivacyErasureService(
                provider.GetRequiredService<RefTestManagementContext>(),
                editState));
        services.AddSingleton<IRefTestSubscriptionService>(subscription);
        using var serviceProvider = services.BuildServiceProvider();

        var payload = JsonSerializer.Serialize(
            new PrivacyWithdrawalBatchPayload(batch.Id),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var handler = new PrivacyWithdrawalBatchJobHandler(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PrivacyWithdrawalBatchJobHandler>.Instance);

        await handler.HandleAsync(
            Job.Create(JobType.PrivacyWithdrawalBatch, payload),
            TestContext.Current.CancellationToken);

        await using var verificationContext = database.CreateContext();
        var erasedRefTest = await verificationContext.RefTests
            .SingleAsync(candidate => candidate.Id == refTest.Id, TestContext.Current.CancellationToken);
        Assert.True(erasedRefTest.IsAnonymized);
        Assert.Equal(17, erasedRefTest.NumberOfQuestions);
        Assert.Equal(45, erasedRefTest.MaxTimeInMinutes);
        Assert.Single(editState.PublishedRefTestIds);
        var completedTarget = await verificationContext.PrivacyWithdrawalBatchTargets
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(completedTarget.CompletedAt);
    }

    [Fact]
    public async Task RefTestErasureUsesTheLatestConcurrentEditWithoutLosingTheUpdate()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var refTest = NewRefTest(titleId, ParticipantEmail);
        var batch = PrivacyWithdrawalBatch.Create(DateTime.UtcNow, targetCount: 1);
        var target = PrivacyWithdrawalBatchTarget.Create(batch.Id, refTest.Id);

        await using (var seed = database.CreateContext())
        {
            seed.RefTests.Add(refTest);
            seed.PrivacyWithdrawalBatches.Add(batch);
            seed.PrivacyWithdrawalBatchTargets.Add(target);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var editState = new ConcurrentRefTestEditState(database);
        var services = new ServiceCollection();
        services.AddScoped<RefTestManagementContext>(_ => database.CreateContext());
        services.AddSingleton(editState);
        services.AddScoped<IRefTestPrivacyErasureService>(provider =>
            new ConcurrentEditingPrivacyErasureService(
                provider.GetRequiredService<RefTestManagementContext>(),
                editState));
        services.AddSingleton<IRefTestSubscriptionService>(
            DispatchProxy.Create<IRefTestSubscriptionService, RecordingSubscriptionProxy>());
        using var serviceProvider = services.BuildServiceProvider();
        var handler = new PrivacyWithdrawalBatchJobHandler(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PrivacyWithdrawalBatchJobHandler>.Instance);
        var payload = JsonSerializer.Serialize(
            new PrivacyWithdrawalBatchPayload(batch.Id),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        await handler.HandleAsync(
            Job.Create(JobType.PrivacyWithdrawalBatch, payload),
            TestContext.Current.CancellationToken);

        await using var verification = database.CreateContext();
        var erased = await verification.RefTests.SingleAsync(
            candidate => candidate.Id == refTest.Id,
            TestContext.Current.CancellationToken);
        Assert.True(erased.IsAnonymized);
        Assert.Equal(17, erased.NumberOfQuestions);
        Assert.Equal(45, erased.MaxTimeInMinutes);
        var completedBatch = await verification.PrivacyWithdrawalBatches
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(completedBatch.CompletedAt);
    }

    [Fact]
    public async Task AuditAndOutboxStoreCountsAndIdentifiersWithoutMailboxOrKeyMaterial()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        var refTest = NewRefTest(titleId, ParticipantEmail);
        await using (var seed = database.CreateContext())
        {
            seed.RefTests.Add(refTest);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var loggerProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
        var interceptor = new AuditSaveChangesInterceptor(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new AuditLogOptions());
        await using var context = database.CreateContext(interceptor);
        var keyProtection = new CapturingKeyProtection();
        var service = new PrivacyWithdrawalRequestService(
            context,
            NewJobEnqueueService(context, loggerFactory.CreateLogger<JobEnqueueService>()),
            keyProtection,
            new RefTestRepository(context, NewSessionTokenService()),
            new PrivacyChallengeConfiguration(),
            new BackgroundJobConfiguration(),
            loggerFactory.CreateLogger<PrivacyWithdrawalRequestService>());

        await service.RequestAsync(ParticipantEmail, TestContext.Current.CancellationToken);
        var rawKey = keyProtection.ProtectedKeys.Single();
        var protectedKey = $"protected:{rawKey}";
        Assert.True(await service.ConfirmAsync(rawKey, TestContext.Current.CancellationToken));

        var auditEvents = await context.AuditEvents
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        var confirmationEvent = Assert.Single(
            auditEvents,
            auditEvent => auditEvent.Type == "PrivacyWithdrawalBatchConfirmed");
        Assert.Equal("Verified participant", confirmationEvent.ActorName);
        Assert.Equal(string.Empty, confirmationEvent.ActorEmail);
        using (var confirmationData = JsonDocument.Parse(confirmationEvent.Data!))
        {
            Assert.Equal(["targetCount"], confirmationData.RootElement.EnumerateObject()
                .Select(property => property.Name).ToArray());
            Assert.Equal(1, confirmationData.RootElement.GetProperty("targetCount").GetInt32());
        }

        foreach (var auditEvent in auditEvents)
        {
            Assert.DoesNotContain(ParticipantEmail, auditEvent.Data, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(rawKey, auditEvent.Data, StringComparison.Ordinal);
            Assert.DoesNotContain(protectedKey, auditEvent.Data, StringComparison.Ordinal);
            Assert.DoesNotContain(refTest.Id.ToString(), auditEvent.Data, StringComparison.OrdinalIgnoreCase);
        }

        var jobs = await context.Jobs.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, jobs.Count);
        foreach (var queuedJob in jobs)
        {
            Assert.DoesNotContain(ParticipantEmail, queuedJob.Payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(rawKey, queuedJob.Payload, StringComparison.Ordinal);
            Assert.DoesNotContain(protectedKey, queuedJob.Payload, StringComparison.Ordinal);
            Assert.DoesNotContain(refTest.Id.ToString(), queuedJob.Payload, StringComparison.OrdinalIgnoreCase);
        }

        var logs = string.Join(Environment.NewLine, loggerProvider.Messages);
        Assert.DoesNotContain(ParticipantEmail, logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(rawKey, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(protectedKey, logs, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MySQL")]
    [InlineData("SQLite")]
    public void EligibilityCleanupAndMultiIdRecoveryQueriesTranslateOnEveryProvider(string provider)
    {
        // SQLite's provider case does not stand in for MySQL collection translation.
        var builder = new DbContextOptionsBuilder<RefTestManagementContext>();
        switch (provider)
        {
            case "SqlServer":
                builder.UseSqlServer("Server=none;Database=none;Trusted_Connection=True;");
                break;
            case "PostgreSQL":
                builder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test");
                break;
            case "MySQL":
                builder.UseMySQL("Server=localhost;Database=test;User=test;Password=test;");
                break;
            case "SQLite":
                builder.UseSqlite("Data Source=:memory:");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, null);
        }

        using var context = new RefTestManagementContext(builder.Options);
        var refTestEntity = context.Model.FindEntityType(typeof(RefTest))!;
        var emailLookupKeyProperty = refTestEntity.FindProperty(nameof(RefTest.EmailLookupKey))!;
        Assert.Equal(typeof(byte[]), emailLookupKeyProperty.ClrType);
        Assert.Equal(32, emailLookupKeyProperty.GetMaxLength());
        Assert.True(emailLookupKeyProperty.IsNullable);
        Assert.Contains(
            refTestEntity.GetIndexes(),
            index => index.Properties.Contains(emailLookupKeyProperty));

        var lookupKey = TokenService.HashBytes(
            PrivacyWithdrawalChallenge.NormalizeEmail("participant@example.org"));
        var eligibleSql = PrivacyWithdrawalQueries.EligibleRefTests(context.RefTests)
            .Select(refTest => new { refTest.Id, refTest.Email })
            .ToQueryString();
        var matchingSql = PrivacyWithdrawalQueries.MatchingRefTestsByEmailLookupKey(
                context.RefTests,
                lookupKey)
            .Select(refTest => new { refTest.Id, refTest.Email })
            .ToQueryString();
        var backfillSql = PrivacyWithdrawalQueries.EligibleRefTestsMissingEmailLookupKey(context.RefTests)
            .Select(refTest => new { refTest.Id, refTest.Email, refTest.Version })
            .Take(250)
            .ToQueryString();
        var cleanupSql = context.PrivacyWithdrawalChallenges
            .Where(PrivacyWithdrawalCleanupQueries.IsDueForChallengeCleanup(Now))
            .Select(challenge => challenge.Id)
            .ToQueryString();
        var dueWithdrawalSql = context.PrivacyWithdrawalBatchTargets
            .Where(target => target.CompletedAt == null
                             && target.RetryExhaustedAt == null
                             && (target.AttemptCount >= PrivacyWithdrawalBatchTarget.MaximumAttempts
                                 || target.NextAttemptAt == null
                                 || target.NextAttemptAt <= Now)
                             && context.PrivacyWithdrawalBatches.Any(
                                 batch => batch.Id == target.BatchId && batch.CompletedAt == null))
            .Select(target => target.RefTestId)
            .Distinct()
            .Take(500)
            .ToQueryString();
        var requestedIds = new List<Guid> { Guid.Empty, Guid.NewGuid() };
        var requestedTargetsSql = context.PrivacyWithdrawalBatchTargets
            .Where(target => requestedIds.Contains(target.RefTestId)
                             && context.PrivacyWithdrawalBatches.Any(
                                 batch => batch.Id == target.BatchId && batch.CompletedAt == null))
            .Select(target => target.RefTestId)
            .ToQueryString();
        var batchIds = new List<Guid> { Guid.Empty, Guid.NewGuid() };
        var pendingTargetsSql = context.PrivacyWithdrawalBatchTargets
            .Where(target => batchIds.Contains(target.BatchId) && target.CompletedAt == null)
            .Select(target => target.Id)
            .ToQueryString();
        var batchJobSql = context.Jobs
            .Where(job => job.JobType == JobType.PrivacyWithdrawalBatch
                          && job.PrivacyWithdrawalBatchId != null
                          && batchIds.Contains(job.PrivacyWithdrawalBatchId.Value)
                          && (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing))
            .Select(job => job.Id)
            .ToQueryString();
        var incompleteBatchesSql = context.PrivacyWithdrawalBatches
            .Where(batch => batchIds.Contains(batch.Id) && batch.CompletedAt == null)
            .Select(batch => batch.Id)
            .ToQueryString();

        Assert.Contains("IsAnonymized", eligibleSql, StringComparison.Ordinal);
        Assert.Contains("Email", eligibleSql, StringComparison.Ordinal);
        Assert.Contains("EmailLookupKey", matchingSql, StringComparison.Ordinal);
        Assert.Contains("IsAnonymized", matchingSql, StringComparison.Ordinal);
        Assert.DoesNotContain("UPPER", matchingSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EmailLookupKey", backfillSql, StringComparison.Ordinal);
        Assert.Contains("IsAnonymized", backfillSql, StringComparison.Ordinal);
        var pendingTargetSql = context.PrivacyWithdrawalBatchTargets
            .Where(target => target.RefTestId == Guid.Empty && target.CompletedAt == null)
            .Select(target => target.Id)
            .ToQueryString();
        Assert.Contains("RefTestId", pendingTargetSql, StringComparison.Ordinal);
        Assert.Contains("CompletedAt", pendingTargetSql, StringComparison.Ordinal);
        Assert.Contains("ExpiresAt", cleanupSql, StringComparison.Ordinal);
        Assert.Contains("VerifiedAt", cleanupSql, StringComparison.Ordinal);
        Assert.Contains("ProtectedDeliveryKey", cleanupSql, StringComparison.Ordinal);
        Assert.Contains("RetryExhaustedAt", dueWithdrawalSql, StringComparison.Ordinal);
        Assert.Contains("NextAttemptAt", dueWithdrawalSql, StringComparison.Ordinal);
        Assert.Contains("RefTestId", requestedTargetsSql, StringComparison.Ordinal);
        Assert.Contains("BatchId", pendingTargetsSql, StringComparison.Ordinal);
        Assert.Contains("PrivacyWithdrawalBatchId", batchJobSql, StringComparison.Ordinal);
        Assert.Contains("CompletedAt", incompleteBatchesSql, StringComparison.Ordinal);
    }

    private static List<RefTest> CreateAllStatusRefTests(Guid titleId)
    {
        var refs = new List<RefTest>
        {
            NewRefTest(titleId, " ada@example.org "),
            NewRefTest(titleId, "ADA@EXAMPLE.ORG"),
            NewRefTest(titleId, "Ada@Example.org"),
            NewRefTest(titleId, "ada@example.org"),
            NewRefTest(titleId, "ada@example.org", requiresApproval: true),
            NewRefTest(titleId, "ada@example.org", requiresApproval: true)
        };

        refs[1].AcceptPrivacyNotice("1.0");
        refs[1].Start("1.0");
        refs[2].AcceptPrivacyNotice("1.0");
        refs[2].Start("1.0");
        refs[2].Complete(1, 1, 1, 100, [], [], []);
        refs[3].Expire();
        refs[5].Reject("Not eligible");

        Assert.Equal(
            Enum.GetValues<RefTestStatus>().Order(),
            refs.Select(refTest => refTest.Status).Distinct().Order());
        return refs;
    }

    private static void StageConfirmedBatch(RefTestManagementContext context, Guid refTestId, DateTime confirmedAt)
    {
        var batch = PrivacyWithdrawalBatch.Create(confirmedAt, targetCount: 1);
        context.PrivacyWithdrawalBatches.Add(batch);
        context.PrivacyWithdrawalBatchTargets.Add(
            PrivacyWithdrawalBatchTarget.Create(batch.Id, refTestId));
        context.Jobs.Add(NewBatchJob(batch));
    }

    public class RecordingSubscriptionProxy : DispatchProxy
    {
        public Func<Guid, Task>? OnAnonymized { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IRefTestSubscriptionService.PublishRefTestAnonymizedAsync))
                return OnAnonymized?.Invoke((Guid)args![0]!) ?? Task.CompletedTask;

            return targetMethod?.ReturnType == typeof(Task)
                ? Task.CompletedTask
                : throw new NotSupportedException($"Unexpected {targetMethod?.Name} call.");
        }
    }

    public class FailingWithdrawalBatchEnqueueProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IJobEnqueueService.EnqueuePrivacyWithdrawalBatchAsync)
                ? Task.FromException<Guid>(new InvalidOperationException("Batch enqueue failed."))
                : throw new NotSupportedException($"Unexpected {targetMethod?.Name} call.");
    }

    public class RecordingPrivacyWithdrawalEmailProxy : DispatchProxy
    {
        public Exception? Failure { get; set; }
        public Func<Task>? BeforeFinalCheck { get; set; }
        public int InvocationCount { get; private set; }
        public int ProviderSubmissionCount { get; private set; }
        public string? RecipientEmail { get; private set; }
        public string? ChallengeKey { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IEmailService.SendPrivacyWithdrawalVerificationAsync))
                return targetMethod?.ReturnType == typeof(Task)
                    ? Task.CompletedTask
                    : throw new NotSupportedException($"Unexpected {targetMethod?.Name} call.");

            InvocationCount++;
            RecipientEmail = (string)args![0]!;
            ChallengeKey = (string)args[1]!;
            return SubmitAsync(
                (Func<CancellationToken, Task<bool>>)args[3]!,
                (CancellationToken)args[4]!);
        }

        private async Task<bool> SubmitAsync(
            Func<CancellationToken, Task<bool>> finalDeliverabilityCheck,
            CancellationToken cancellationToken)
        {
            if (BeforeFinalCheck is not null)
                await BeforeFinalCheck();
            if (!await finalDeliverabilityCheck(cancellationToken))
                return false;

            ProviderSubmissionCount++;
            if (Failure is not null)
                throw Failure;
            return true;
        }
    }

    private sealed class WorkerFailureState(Guid failOnceId, string email, string key)
    {
        private readonly ConcurrentDictionary<Guid, byte> _failOnceIds = new()
        {
            [failOnceId] = 0
        };

        public ConcurrentBag<Guid> ErasureContextIds { get; } = [];
        public ConcurrentBag<Guid> PublishedRefTestIds { get; } = [];
        public string SensitiveFailure { get; } = $"provider details {email} {key}";

        public bool ShouldFailOnce(Guid refTestId) => _failOnceIds.TryRemove(refTestId, out _);
    }

    private sealed class ConcurrentRefTestEditState(SqliteTestDatabase database)
    {
        private int _hasEdited;

        public ConcurrentBag<Guid> PublishedRefTestIds { get; } = [];

        public async Task EditBeforeErasureAsync(Guid refTestId, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _hasEdited, 1) != 0)
                return;

            await using var concurrentContext = database.CreateContext();
            var current = await concurrentContext.RefTests.SingleAsync(
                candidate => candidate.Id == refTestId,
                cancellationToken);
            current.UpdateTestConfiguration(
                current.TitleId,
                numberOfQuestions: 17,
                maxTimeInMinutes: 45,
                questionIds: ["q1", "q2", "q3"]);
            await concurrentContext.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class ConcurrentEditingPrivacyErasureService(
        RefTestManagementContext context,
        ConcurrentRefTestEditState editState) : IRefTestPrivacyErasureService
    {
        public async Task EraseAsync(
            RefTest refTest,
            ErasureInitiator initiator,
            CancellationToken cancellationToken = default)
        {
            await editState.EditBeforeErasureAsync(refTest.Id, cancellationToken);
            await new RefTestPrivacyErasureService(context).EraseAsync(refTest, initiator, cancellationToken);
        }

        public Task<bool> EraseIfDueForRetentionAsync(
            Guid refTestId,
            DateTime cutoff,
            CancellationToken cancellationToken = default) =>
            new RefTestPrivacyErasureService(context).EraseIfDueForRetentionAsync(
                refTestId,
                cutoff,
                cancellationToken);

        public Task EraseAndDeleteAsync(
            RefTest refTest,
            ErasureInitiator initiator,
            CancellationToken cancellationToken = default) =>
            new RefTestPrivacyErasureService(context).EraseAndDeleteAsync(
                refTest,
                initiator,
                cancellationToken);
    }

    private sealed class FailOncePrivacyErasureService(
        RefTestManagementContext context,
        WorkerFailureState failureState) : IRefTestPrivacyErasureService
    {
        public async Task EraseAsync(
            RefTest refTest,
            ErasureInitiator initiator,
            CancellationToken cancellationToken = default)
        {
            failureState.ErasureContextIds.Add(context.ContextId.InstanceId);
            if (failureState.ShouldFailOnce(refTest.Id))
                throw new InvalidOperationException(failureState.SensitiveFailure);

            await new RefTestPrivacyErasureService(context).EraseAsync(refTest, initiator, cancellationToken);
        }

        public Task<bool> EraseIfDueForRetentionAsync(
            Guid refTestId,
            DateTime cutoff,
            CancellationToken cancellationToken = default) =>
            new RefTestPrivacyErasureService(context).EraseIfDueForRetentionAsync(
                refTestId,
                cutoff,
                cancellationToken);

        public Task EraseAndDeleteAsync(
            RefTest refTest,
            ErasureInitiator initiator,
            CancellationToken cancellationToken = default) =>
            new RefTestPrivacyErasureService(context).EraseAndDeleteAsync(
                refTest,
                initiator,
                cancellationToken);
    }

    private sealed class CapturingKeyProtection : IPersonalDataExportKeyProtection
    {
        public List<string> ProtectedKeys { get; } = [];

        public string Protect(string key)
        {
            ProtectedKeys.Add(key);
            return $"protected:{key}";
        }

        public string Unprotect(string protectedKey) =>
            protectedKey.StartsWith("protected:", StringComparison.Ordinal)
                ? protectedKey["protected:".Length..]
                : throw new InvalidOperationException("Test protection state is invalid.");
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                provider.Messages.Add(formatter(state, exception));
        }
    }

    private sealed class StubPrivacyWithdrawalRequestService(
        Func<CancellationToken, Task<int>> reconcile) : IPrivacyWithdrawalRequestService
    {
        public Task RequestAsync(string? email, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<bool> RequestForParticipantAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> ConfirmAsync(string? challengeKey, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<int> ReconcileIncompleteBatchesAsync(CancellationToken cancellationToken) =>
            reconcile(cancellationToken);
    }
}
