using System.Reflection;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices;
using Handball.Belgium.RefTestManagement.Infrastructure.Jobs;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Lifecycle;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.Security;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>Challenges, participant-token and bulk confirmation, and challenge email delivery.</summary>
public sealed partial class PrivacyWithdrawalPipelineTests
{
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
                NewSessionTokenService(),
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
                    new EfJobQueueStore(context, NullLogger<EfJobQueueStore>.Instance),
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
}
