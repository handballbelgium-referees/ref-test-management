using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices;
using Handball.Belgium.RefTestManagement.Api.Graphql.Queries;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class RefTestExpirationEnqueueTests
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static RefTestInvitationTokenProtection TokenProtection() =>
        new(new EphemeralDataProtectionProvider());

    private static JobEnqueueService EnqueueService(RefTestManagementContext context) =>
        new(context, TokenProtection(), NullLogger<JobEnqueueService>.Instance);

    private static RefTestSessionTokenService SessionTokenService() =>
        new(new EphemeralDataProtectionProvider(), TimeProvider.System);

    private static RefTestExpirationPayload Payload(Guid? refTestId = null) =>
        new(refTestId ?? Guid.NewGuid(), RefTestExpirationAction.MarkAsExpired);

    [Fact]
    public async Task RepeatedEnqueuesCreateOneActiveJobPerRefTestAndAction()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();
        var service = EnqueueService(context);
        var refTestId = Guid.NewGuid();

        await service.EnqueueRefTestExpirationAsync(Payload(refTestId), cancellationToken: ct);
        await service.EnqueueRefTestExpirationAsync(Payload(refTestId), cancellationToken: ct);
        await service.EnqueueRefTestExpirationAsync(
            new RefTestExpirationPayload(refTestId, RefTestExpirationAction.AutoComplete),
            cancellationToken: ct);
        await service.EnqueueRefTestExpirationAsync(
            Payload(Guid.NewGuid()),
            cancellationToken: ct);

        Assert.Equal(
            3,
            await context.Jobs.CountAsync(
                job => job.JobType == JobType.RefTestExpiration
                       && (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing),
                ct));
    }

    [Fact]
    public async Task ConcurrentEnqueuesCreateOneActiveJob()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var payload = Payload();

        await Task.WhenAll(
            EnqueueService(firstContext).EnqueueRefTestExpirationAsync(payload, cancellationToken: ct),
            EnqueueService(secondContext).EnqueueRefTestExpirationAsync(payload, cancellationToken: ct));

        await using var observer = database.CreateContext();
        Assert.Equal(
            1,
            await observer.Jobs.CountAsync(
                job => job.JobType == JobType.RefTestExpiration
                       && (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing),
                ct));
    }

    [Fact]
    public async Task ExistingLegacyActiveJobPreventsANewDuplicate()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var payload = Payload();
        var payloadJson = JsonSerializer.Serialize(payload, PayloadOptions);

        await using var context = database.CreateContext();
        context.Jobs.Add(Job.Create(JobType.RefTestExpiration, payloadJson));
        await context.SaveChangesAsync(ct);

        await EnqueueService(context).EnqueueRefTestExpirationAsync(payload, cancellationToken: ct);

        Assert.Equal(
            1,
            await context.Jobs.CountAsync(
                job => job.JobType == JobType.RefTestExpiration
                       && (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing),
                ct));
    }

    [Fact]
    public async Task CompletedAndFailedJobsDoNotBlockLaterEnqueues()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        await using var enqueueContext = database.CreateContext();
        await using var processingContext = database.CreateContext();
        var service = EnqueueService(processingContext);
        var payload = Payload();

        await EnqueueService(enqueueContext).EnqueueRefTestExpirationAsync(payload, cancellationToken: ct);
        var first = await processingContext.Jobs.SingleAsync(ct);
        Assert.Equal(
            $"expiration:{payload.RefTestId:N}:{payload.Action}",
            first.DeduplicationKey);
        first.MarkAsCompleted();
        await processingContext.SaveChangesAsync(ct);

        await service.EnqueueRefTestExpirationAsync(payload, cancellationToken: ct);
        var second = await processingContext.Jobs.SingleAsync(
            job => job.Status == JobStatus.Pending,
            ct);
        second.MarkAsFailed("Test failure", maxAttempts: 1);
        await processingContext.SaveChangesAsync(ct);

        await service.EnqueueRefTestExpirationAsync(payload, cancellationToken: ct);

        Assert.Equal(3, await processingContext.Jobs.CountAsync(ct));
        Assert.Equal(
            1,
            await processingContext.Jobs.CountAsync(
                job => job.JobType == JobType.RefTestExpiration
                       && (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing),
                ct));
    }

    [Fact]
    public async Task ExpirationSweepAndParticipantRequestShareTheDeduplicationBoundary()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var title = RefTestTitle.Create("Season 2026");
        var refTest = RefTest.Create(
            title.Id,
            "Ada",
            "Lovelace",
            "ada@example.org",
            numberOfQuestions: 10,
            maxTimeInMinutes: 30,
            questionIds: ["q1", "q2"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: true);
        var token = refTest.GetIssuedToken();

        await using (var context = database.CreateContext())
        {
            context.RefTestTitles.Add(title);
            context.RefTests.Add(refTest);
            await context.SaveChangesAsync(ct);
        }

        var configuration = new RefTestExpirationConfiguration
        {
            ExpirationCheckIntervalMinutes = 60,
            StartupDelaySeconds = 0,
            ExpirationIfNotStarted = TimeSpan.Zero
        };
        await RunExpirationSweepAsync(database, configuration, refTest.Id, ct);

        await using (var requestContext = database.CreateContext())
        {
            await Assert.ThrowsAsync<RefTestExpiredException>(() =>
                RefTestQueries.GetRefTestByTokenAsync(
                    token,
                    requestContext,
                    configuration,
                    SessionTokenService(),
                    EnqueueService(requestContext),
                    ct));
        }

        await using var observer = database.CreateContext();
        Assert.Equal(
            1,
            await observer.Jobs.CountAsync(
                job => job.JobType == JobType.RefTestExpiration
                       && (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing),
                ct));
    }

    [Fact]
    public void TerminalTransitionsReleaseTheDeduplicationKey()
    {
        var completed = CreateKeyedJob();
        completed.MarkAsCompleted();
        Assert.Null(completed.DeduplicationKey);

        var failed = CreateKeyedJob();
        failed.MarkAsFailed("Test failure", maxAttempts: 1);
        Assert.Null(failed.DeduplicationKey);

        var permanentlyFailed = CreateKeyedJob();
        permanentlyFailed.MarkAsPermanentlyFailed("Test failure");
        Assert.Null(permanentlyFailed.DeduplicationKey);

        var abandoned = CreateKeyedJob();
        abandoned.MarkAsProcessing(TimeSpan.Zero);
        Assert.True(abandoned.MarkAsFailedAfterAttemptsExhausted(
            DateTime.UtcNow.AddSeconds(1),
            maxAttempts: 1,
            "Test failure"));
        Assert.Null(abandoned.DeduplicationKey);

        var cancelled = CreateKeyedJob();
        cancelled.Cancel("Test cancellation");
        Assert.Null(cancelled.DeduplicationKey);

        var completionSaveRetry = CreateKeyedJob();
        var key = completionSaveRetry.DeduplicationKey;
        completionSaveRetry.MarkAsCompleted();
        completionSaveRetry.MarkAsFailed("Completion save failed", maxAttempts: 3);
        Assert.Equal(key, completionSaveRetry.DeduplicationKey);
    }

    private static Job CreateKeyedJob() =>
        Job.Create(
            JobType.RefTestExpiration,
            "{}",
            deduplicationKey: $"expiration:{Guid.NewGuid():N}:{RefTestExpirationAction.MarkAsExpired}");

    private static async Task RunExpirationSweepAsync(
        SqliteTestDatabase database,
        RefTestExpirationConfiguration configuration,
        Guid refTestId,
        CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<RefTestManagementContext>>(
            new TestContextFactory(database));
        services.AddScoped<RefTestManagementContext>(_ => database.CreateContext());
        services.AddScoped<IJobEnqueueService>(provider =>
            EnqueueService(provider.GetRequiredService<RefTestManagementContext>()));

        using var serviceProvider = services.BuildServiceProvider();
        var expirationService = new RefTestExpirationService(
            serviceProvider,
            NullLogger<RefTestExpirationService>.Instance,
            configuration);
        await expirationService.StartAsync(cancellationToken);

        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                await using var observer = database.CreateContext();
                if (await observer.Jobs.AnyAsync(
                        job => job.DeduplicationKey
                                 == $"expiration:{refTestId:N}:{RefTestExpirationAction.MarkAsExpired}"
                               && (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing),
                        cancellationToken))
                    return;

                await Task.Delay(10, cancellationToken);
            }

            throw new TimeoutException("The expiration sweep did not enqueue its due job.");
        }
        finally
        {
            await expirationService.StopAsync(CancellationToken.None);
        }
    }

    private sealed class TestContextFactory(SqliteTestDatabase database)
        : IDbContextFactory<RefTestManagementContext>
    {
        public RefTestManagementContext CreateDbContext() => database.CreateContext();

        public Task<RefTestManagementContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}
