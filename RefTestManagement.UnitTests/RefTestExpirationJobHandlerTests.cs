using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class RefTestExpirationJobHandlerTests
{
    private const int MaxAttempts = 3;
    private const string ProviderMarker = "provider-controlled-expiration-marker";

    [Fact]
    public async Task ProviderExceptionIsNotLoggedAndExpirationJobRemainsRetryable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        Guid jobId;
        Guid refTestId;

        await using (var seedContext = database.CreateContext())
        {
            var title = RefTestTitle.Create("Season opener");
            seedContext.RefTestTitles.Add(title);
            await seedContext.SaveChangesAsync(cancellationToken);

            var refTest = RefTest.Create(
                title.Id,
                "Ada",
                "Lovelace",
                "ada@example.org",
                numberOfQuestions: 1,
                maxTimeInMinutes: 30,
                questionIds: ["q1"],
                sendInvitationAutomatically: false,
                sendResultsAutomatically: false);
            refTest.AcceptPrivacyNotice("v1");
            refTest.Start("v1");

            var job = Job.Create(
                JobType.RefTestExpiration,
                JsonSerializer.Serialize(new RefTestExpirationPayload(
                    refTest.Id,
                    RefTestExpirationAction.AutoComplete)));
            job.MarkAsProcessing(TimeSpan.FromMinutes(5));

            seedContext.RefTests.Add(refTest);
            seedContext.Jobs.Add(job);
            await seedContext.SaveChangesAsync(cancellationToken);

            jobId = job.Id;
            refTestId = refTest.Id;
        }

        var log = new CapturingLogger();
        var handler = new RefTestExpirationJobHandler(
            new TestDbContextFactory(database),
            CreateSubscriptionService(),
            CreateFailingQuestionsService(new HttpRequestException(ProviderMarker)),
            new EmailConfiguration(),
            new RefTestInvitationTokenProtection(new EphemeralDataProtectionProvider()),
            log,
            NullLogger<JobEnqueueService>.Instance);
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IJobHandler>(JobType.RefTestExpiration, handler);
        using var serviceProvider = services.BuildServiceProvider();

        await using (var processingContext = database.CreateContext())
        {
            var job = await processingContext.Jobs.SingleAsync(
                candidate => candidate.Id == jobId,
                cancellationToken);
            await BackgroundJobService.ProcessJobAsync(
                job,
                serviceProvider,
                processingContext,
                NullLogger.Instance,
                MaxAttempts,
                cancellationToken);
        }

        await using var verificationContext = database.CreateContext();
        var retriedJob = await verificationContext.Jobs.SingleAsync(
            candidate => candidate.Id == jobId,
            cancellationToken);

        Assert.Equal(JobStatus.Pending, retriedJob.Status);
        Assert.Equal(1, retriedJob.Attempts);
        Assert.Null(retriedJob.LockedUntil);
        Assert.Equal(ProviderMarker, retriedJob.ErrorMessage);

        Assert.DoesNotContain(log.Entries, entry =>
            entry.Message.Contains(ProviderMarker, StringComparison.Ordinal));
        Assert.All(log.Entries, entry => Assert.Null(entry.Exception));

        var failureLog = Assert.Single(log.Entries, entry =>
            entry.Message.Contains("RefTest expiration job failed", StringComparison.Ordinal));
        Assert.Contains(jobId.ToString(), failureLog.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(refTestId.ToString(), failureLog.Message, StringComparison.Ordinal);
    }

    private static IIhfRulesQuestionsService CreateFailingQuestionsService(Exception failure)
    {
        var service = DispatchProxy.Create<IIhfRulesQuestionsService, FailingQuestionsServiceProxy>();
        ((FailingQuestionsServiceProxy)(object)service).Failure = failure;
        return service;
    }

    private static IRefTestSubscriptionService CreateSubscriptionService() =>
        DispatchProxy.Create<IRefTestSubscriptionService, NoOpSubscriptionServiceProxy>();

    private sealed class TestDbContextFactory(SqliteTestDatabase database)
        : IDbContextFactory<RefTestManagementContext>
    {
        public RefTestManagementContext CreateDbContext() => database.CreateContext();

        public Task<RefTestManagementContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed record CapturedLog(string Message, Exception? Exception);

    private sealed class CapturingLogger : ILogger<RefTestExpirationJobHandler>
    {
        public List<CapturedLog> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new CapturedLog(formatter(state, exception), exception));
    }

    public class FailingQuestionsServiceProxy : DispatchProxy
    {
        public Exception? Failure { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IIhfRulesQuestionsService.CalculateScoreAsync))
                throw new NotSupportedException();

            throw Failure ?? new InvalidOperationException("No provider failure was configured.");
        }
    }

    public class NoOpSubscriptionServiceProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Task.CompletedTask;
    }
}
