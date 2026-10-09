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

public sealed partial class PrivacyWithdrawalPipelineTests
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
            sessionTokenService ?? NewSessionTokenService(),
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
