using System.Security.Claims;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.Graphql.Queries;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Handball.Belgium.RefTestManagement.Domain.Privacy.Events;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class PrivacyWithdrawalReviewTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ReviewQueryReturnsOnlyTerminalFailuresAndTheMinimalReviewFields()
    {
        using var database = SqliteTestDatabase.Create();
        var completedBatch = PrivacyWithdrawalBatch.Create(Now, targetCount: 3);
        var activeBatch = PrivacyWithdrawalBatch.Create(Now, targetCount: 1);
        var failedTarget = ExhaustedTarget(completedBatch.Id, Guid.NewGuid(), markAttemptLimitReached: true);
        var failedProcessingTarget = ExhaustedTarget(completedBatch.Id, Guid.NewGuid());
        var completedTarget = PrivacyWithdrawalBatchTarget.Create(completedBatch.Id, Guid.NewGuid());
        completedTarget.MarkCompleted(Now.AddMinutes(1));
        var failedTargetInActiveBatch = ExhaustedTarget(activeBatch.Id, Guid.NewGuid());
        completedBatch.MarkCompleted(Now.AddMinutes(1), exhaustedTargetCount: 2);

        await using (var seed = database.CreateContext())
        {
            seed.PrivacyWithdrawalBatches.AddRange(completedBatch, activeBatch);
            seed.PrivacyWithdrawalBatchTargets.AddRange(
                failedTarget,
                failedProcessingTarget,
                completedTarget,
                failedTargetInActiveBatch);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateContext();
        var results = await PrivacyWithdrawalReviewQueries
            .GetFailedPrivacyWithdrawalTargets(context)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.Contains(
            results,
            result => result.ActionToken == failedTarget.Id
                      && result.FailureCategory == PrivacyWithdrawalTargetFailureCode.AttemptLimitReached);
        Assert.Contains(
            results,
            result => result.ActionToken == failedProcessingTarget.Id
                      && result.FailureCategory == PrivacyWithdrawalTargetFailureCode.ProcessingFailed);
        Assert.Equal(
            ["ActionToken", "FailureCategory"],
            typeof(FailedPrivacyWithdrawalTarget)
                .GetProperties()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task AcknowledgementAuditsTheSanitizedCategoryBeforePurgingTheTarget()
    {
        using var database = SqliteTestDatabase.Create();
        var batch = PrivacyWithdrawalBatch.Create(Now, targetCount: 1);
        var refTestId = Guid.NewGuid();
        var target = ExhaustedTarget(batch.Id, refTestId, markAttemptLimitReached: true);
        batch.MarkCompleted(Now.AddMinutes(1), exhaustedTargetCount: 1);

        await using (var seed = database.CreateContext())
        {
            seed.PrivacyWithdrawalBatches.Add(batch);
            seed.PrivacyWithdrawalBatchTargets.Add(target);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext(CreateAuditInterceptor()))
        {
            var requestService = CreateRequestService(context);
            Assert.True(await requestService.AcknowledgeFailedTargetAsync(
                target.Id,
                TestContext.Current.CancellationToken));
        }

        await using var verification = database.CreateContext();
        Assert.False(await verification.PrivacyWithdrawalBatchTargets
            .AnyAsync(candidate => candidate.Id == target.Id, TestContext.Current.CancellationToken));

        var auditEvents = await verification.AuditEvents.ToListAsync(TestContext.Current.CancellationToken);
        var auditEvent = Assert.Single(auditEvents);
        Assert.Equal(
            PrivacyWithdrawalFailedTargetAcknowledgedEvent.EventType,
            auditEvent.Type);
        Assert.Equal(batch.Id.ToString(), auditEvent.StreamId);
        Assert.NotEqual(target.Id.ToString(), auditEvent.StreamId);
        Assert.Equal("Privacy operator", auditEvent.ActorName);
        Assert.Equal("privacy-operator@example.test", auditEvent.ActorEmail);

        using var changes = JsonDocument.Parse(auditEvent.Data!);
        Assert.Equal(
            "AttemptLimitReached",
            changes.RootElement.GetProperty("failureCategory").GetString());
        var failureChange = Assert.Single(changes.RootElement.EnumerateObject());
        Assert.Equal("failureCategory", failureChange.Name);
        var auditPayload =
            $"{auditEvent.Data} {auditEvent.StreamId} {auditEvent.ActorName} {auditEvent.ActorEmail} {auditEvent.Headers}";
        Assert.DoesNotContain(target.Id.ToString(), auditPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(refTestId.ToString(), auditPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("participant@example.test", auditPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Participant Name", auditPayload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AcknowledgementReturnsNotAvailableForMissingNonterminalOrCompletedTargets()
    {
        using var database = SqliteTestDatabase.Create();
        var activeBatch = PrivacyWithdrawalBatch.Create(Now, targetCount: 2);
        var pendingTarget = PrivacyWithdrawalBatchTarget.Create(activeBatch.Id, Guid.NewGuid());
        var exhaustedTargetInActiveBatch = ExhaustedTarget(activeBatch.Id, Guid.NewGuid());
        var completedBatch = PrivacyWithdrawalBatch.Create(Now, targetCount: 1);
        var completedTarget = PrivacyWithdrawalBatchTarget.Create(completedBatch.Id, Guid.NewGuid());
        completedTarget.MarkCompleted(Now.AddMinutes(1));
        completedBatch.MarkCompleted(Now.AddMinutes(2));

        await using (var seed = database.CreateContext())
        {
            seed.PrivacyWithdrawalBatches.AddRange(activeBatch, completedBatch);
            seed.PrivacyWithdrawalBatchTargets.AddRange(
                pendingTarget,
                exhaustedTargetInActiveBatch,
                completedTarget);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var requestService = CreateRequestService(context);
            Assert.False(await requestService.AcknowledgeFailedTargetAsync(
                Guid.NewGuid(),
                TestContext.Current.CancellationToken));
            Assert.False(await requestService.AcknowledgeFailedTargetAsync(
                pendingTarget.Id,
                TestContext.Current.CancellationToken));
            Assert.False(await requestService.AcknowledgeFailedTargetAsync(
                exhaustedTargetInActiveBatch.Id,
                TestContext.Current.CancellationToken));
            Assert.False(await requestService.AcknowledgeFailedTargetAsync(
                completedTarget.Id,
                TestContext.Current.CancellationToken));
        }

        await using var verification = database.CreateContext();
        Assert.Equal(
            3,
            await verification.PrivacyWithdrawalBatchTargets.CountAsync(
                TestContext.Current.CancellationToken));
        Assert.Empty(await verification.AuditEvents.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AcknowledgementRollsBackTheAuditEventWhenTargetPurgeFails()
    {
        using var database = SqliteTestDatabase.Create();
        var batch = PrivacyWithdrawalBatch.Create(Now, targetCount: 1);
        var target = ExhaustedTarget(batch.Id, Guid.NewGuid());
        batch.MarkCompleted(Now.AddMinutes(1), exhaustedTargetCount: 1);

        await using (var seed = database.CreateContext())
        {
            seed.PrivacyWithdrawalBatches.Add(batch);
            seed.PrivacyWithdrawalBatchTargets.Add(target);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext(
                         CreateAuditInterceptor(),
                         new FailOnSecondSaveInterceptor()))
        {
            var requestService = CreateRequestService(context);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                requestService.AcknowledgeFailedTargetAsync(
                    target.Id,
                    TestContext.Current.CancellationToken));
        }

        await using var verification = database.CreateContext();
        Assert.True(await verification.PrivacyWithdrawalBatchTargets
            .AnyAsync(candidate => candidate.Id == target.Id, TestContext.Current.CancellationToken));
        Assert.Empty(await verification.AuditEvents.ToListAsync(TestContext.Current.CancellationToken));
    }

    private static PrivacyWithdrawalBatchTarget ExhaustedTarget(
        Guid batchId,
        Guid refTestId,
        bool markAttemptLimitReached = false)
    {
        var target = PrivacyWithdrawalBatchTarget.Create(batchId, refTestId);
        var attemptAt = Now;
        for (var attempt = 1; attempt < PrivacyWithdrawalBatchTarget.MaximumAttempts; attempt++)
        {
            Assert.True(target.TryStartAttempt(attemptAt));
            Assert.True(target.RecordProcessingFailure(attemptAt.AddSeconds(1)));
            attemptAt = target.NextAttemptAt!.Value;
        }

        Assert.True(target.TryStartAttempt(attemptAt));
        Assert.True(markAttemptLimitReached
            ? target.MarkRetryLimitReached(attemptAt.AddSeconds(1))
            : target.RecordProcessingFailure(attemptAt.AddSeconds(1)));
        return target;
    }

    private static PrivacyWithdrawalRequestService CreateRequestService(
        RefTestManagement.Infrastructure.RefTestManagementContext context)
    {
        var protectionProvider = new EphemeralDataProtectionProvider();
        return new PrivacyWithdrawalRequestService(
            context,
            new JobEnqueueService(
                context,
                new RefTestInvitationTokenProtection(protectionProvider),
                NullLogger<JobEnqueueService>.Instance),
            new PersonalDataExportKeyProtection(protectionProvider),
            new RefTestSessionTokenService(protectionProvider, TimeProvider.System),
            new PrivacyChallengeConfiguration(),
            new BackgroundJobConfiguration(),
            NullLogger<PrivacyWithdrawalRequestService>.Instance);
    }

    private static AuditSaveChangesInterceptor CreateAuditInterceptor()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("name", "Privacy operator"),
                    new Claim("email", "privacy-operator@example.test")
                ],
                "test"))
        };
        return new AuditSaveChangesInterceptor(
            new HttpContextAccessor { HttpContext = httpContext },
            new AuditLogOptions());
    }

    private sealed class FailOnSecondSaveInterceptor : SaveChangesInterceptor
    {
        private int _saveCount;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _saveCount) == 2)
                return ValueTask.FromException<InterceptionResult<int>>(
                    new InvalidOperationException("Simulated target purge failure."));

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
