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
using Handball.Belgium.RefTestManagement.Domain.Security;
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

/// <summary>Cleanup of expired challenges and completed batches, and scheduled reconciliation.</summary>
public sealed partial class PrivacyWithdrawalPipelineTests
{
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
}
