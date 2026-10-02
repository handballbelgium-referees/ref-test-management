using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Lifecycle;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
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

    private static PrivacyWithdrawalRequestService NewRequestService(
        RefTestManagementContext context,
        CapturingKeyProtection keyProtection,
        ILogger<PrivacyWithdrawalRequestService>? logger = null,
        PersonalDataExportConfiguration? configuration = null,
        BackgroundJobConfiguration? backgroundJobConfiguration = null) =>
        new(
            context,
            NewJobEnqueueService(context),
            keyProtection,
            configuration ?? new PersonalDataExportConfiguration(),
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
    public async Task RequestDoesNotQueueUnknownAddressesAndSuppressesRepeatedNormalizedRequests()
    {
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        await using var context = database.CreateContext();
        context.RefTests.Add(NewRefTest(titleId, ParticipantEmail));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var keyProtection = new CapturingKeyProtection();
        var service = NewRequestService(
            context,
            keyProtection,
            configuration: new PersonalDataExportConfiguration
            {
                PrivacyChallengeKeyLifetimeHours = 2
            });
        await service.RequestAsync("missing@example.org", TestContext.Current.CancellationToken);

        Assert.Empty(await context.PrivacyWithdrawalChallenges.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await context.Jobs.ToListAsync(TestContext.Current.CancellationToken));

        await service.RequestAsync("  ADA@EXAMPLE.ORG  ", TestContext.Current.CancellationToken);
        var challenge = await context.PrivacyWithdrawalChallenges.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ParticipantEmail, challenge.Email);
        Assert.Equal(TimeSpan.FromHours(2), challenge.ExpiresAt - challenge.CreatedAt);
        Assert.Single(keyProtection.ProtectedKeys);
        Assert.Single(await context.Jobs.ToListAsync(TestContext.Current.CancellationToken));

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
                new PersonalDataExportConfiguration(),
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
            loggerFactory.CreateLogger<PrivacyWithdrawalChallengeEmailJobHandler>());
        await completedHandler.HandleAsync(job, TestContext.Current.CancellationToken);
        Assert.Equal(2, emailProxy.InvocationCount);
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

        await using (var seed = database.CreateContext())
        {
            seed.PrivacyWithdrawalChallenges.AddRange(expired, active);
            seed.PrivacyWithdrawalBatches.AddRange(completedBatch, incompleteBatch);
            seed.PrivacyWithdrawalBatchTargets.AddRange(
                completedTargetForCompletedBatch,
                completedTargetForActiveBatch,
                pendingTargetForActiveBatch);
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
                1,
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
        var payload = JsonSerializer.Serialize(
            new PrivacyWithdrawalBatchPayload(batch.Id),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var job = Job.Create(JobType.PrivacyWithdrawalBatch, payload);
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

        Assert.Equal(JobStatus.Pending, trackedJob.Status);
        Assert.NotEqual(JobStatus.Cancelled, trackedJob.Status);
        Assert.DoesNotContain(ParticipantEmail, trackedJob.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ChallengeKey, trackedJob.ErrorMessage, StringComparison.Ordinal);

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
            Assert.NotNull(progress[refTests[1].Id].CompletedAt);
            Assert.NotNull(progress[refTests[2].Id].CompletedAt);
            Assert.Null(progress[refTests[2].Id].ErasureStartedAt);
        }

        trackedJob.MarkAsProcessing(TimeSpan.FromMinutes(5));
        await jobContext.SaveChangesWithRetryAsync(TestContext.Current.CancellationToken);
        await BackgroundJobService.ProcessJobAsync(
            trackedJob,
            processingScope.ServiceProvider,
            jobContext,
            NullLogger.Instance,
            maxAttempts: 3,
            TestContext.Current.CancellationToken);

        Assert.Equal(JobStatus.Completed, trackedJob.Status);
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

            var payloadJson = JsonDocument.Parse(trackedJob.Payload);
            Assert.Equal(
                ["batchId"],
                payloadJson.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
            Assert.DoesNotContain(refTests[0].Id.ToString(), trackedJob.Payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(refTests[1].Id.ToString(), trackedJob.Payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(refTests[2].Id.ToString(), trackedJob.Payload, StringComparison.OrdinalIgnoreCase);
        }

        var logs = string.Join(Environment.NewLine, loggerProvider.Messages);
        Assert.DoesNotContain(ParticipantEmail, logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ChallengeKey, logs, StringComparison.Ordinal);
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
            new PersonalDataExportConfiguration(),
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
    public void EligibilityAndChallengeCleanupQueriesTranslateOnEveryProvider(string provider)
    {
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
        var eligibleSql = PrivacyWithdrawalQueries.EligibleRefTests(context.RefTests)
            .Select(refTest => new { refTest.Id, refTest.Email })
            .ToQueryString();
        var cleanupSql = context.PrivacyWithdrawalChallenges
            .Where(PrivacyWithdrawalCleanupQueries.IsDueForChallengeCleanup(Now))
            .Select(challenge => challenge.Id)
            .ToQueryString();

        Assert.Contains("IsAnonymized", eligibleSql, StringComparison.Ordinal);
        Assert.Contains("Email", eligibleSql, StringComparison.Ordinal);
        var pendingTargetSql = context.PrivacyWithdrawalBatchTargets
            .Where(target => target.RefTestId == Guid.Empty && target.CompletedAt == null)
            .Select(target => target.Id)
            .ToQueryString();
        Assert.Contains("RefTestId", pendingTargetSql, StringComparison.Ordinal);
        Assert.Contains("CompletedAt", pendingTargetSql, StringComparison.Ordinal);
        Assert.Contains("ExpiresAt", cleanupSql, StringComparison.Ordinal);
        Assert.Contains("VerifiedAt", cleanupSql, StringComparison.Ordinal);
        Assert.Contains("ProtectedDeliveryKey", cleanupSql, StringComparison.Ordinal);
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
        var payload = JsonSerializer.Serialize(
            new PrivacyWithdrawalBatchPayload(batch.Id),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        context.Jobs.Add(Job.Create(JobType.PrivacyWithdrawalBatch, payload));
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
                ? Task.FromException(new InvalidOperationException("Batch enqueue failed."))
                : throw new NotSupportedException($"Unexpected {targetMethod?.Name} call.");
    }

    public class RecordingPrivacyWithdrawalEmailProxy : DispatchProxy
    {
        public Exception? Failure { get; set; }
        public int InvocationCount { get; private set; }
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
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
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
}
