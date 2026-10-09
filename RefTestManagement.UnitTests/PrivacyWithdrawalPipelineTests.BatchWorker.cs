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

/// <summary>The batch worker, erasure against concurrent edits, audit content and provider query translation.</summary>
public sealed partial class PrivacyWithdrawalPipelineTests
{
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
            NewSessionTokenService(),
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
}
