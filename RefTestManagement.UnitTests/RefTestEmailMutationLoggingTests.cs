using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.RefTests.Email;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Persistence;
using Handball.Belgium.RefTestManagement.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public class RefTestEmailMutationLoggingTests
{
    [Fact]
    public async Task SendInvitationsAsync_MasksEmailAddressesBeforeLoggingJobFailures()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;
        var refTestId = await SeedPendingRefTestAsync(database, titleId);

        using var loggerProvider = new CapturingLoggerProvider();
        using var loggerFactory = CreateLoggerFactory(loggerProvider);

        await using var context = database.CreateContext();
        var result = await CreateHandler(
            context,
            ThrowingJobEnqueueService.ForInvitation(new InvalidOperationException("SMTP rejected john.doe@example.com")),
            loggerFactory,
            "trace-invite").SendInvitationsAsync(
            new SendInvitationsCommand([refTestId]),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Failed);
        Assert.Single(result.Errors);
        AssertLoggedFailure(loggerProvider, "trace-invite", "SendInvitationsAsync");
    }

    [Fact]
    public async Task SendResultsAsync_MasksEmailAddressesBeforeLoggingJobFailures()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;
        var refTestId = await SeedCompletedRefTestAsync(database, titleId);

        using var loggerProvider = new CapturingLoggerProvider();
        using var loggerFactory = CreateLoggerFactory(loggerProvider);

        await using var context = database.CreateContext();
        var result = await CreateHandler(
            context,
            ThrowingJobEnqueueService.ForResult(new InvalidOperationException("SMTP rejected john.doe@example.com")),
            loggerFactory,
            "trace-result").SendResultsAsync(
            new SendResultsCommand([refTestId]),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Failed);
        Assert.Single(result.Errors);
        AssertLoggedFailure(loggerProvider, "trace-result", "SendResultsAsync");
    }

    [Fact]
    public async Task SendReportAsync_MasksEmailAddressesBeforeLoggingJobFailures()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;
        var refTestId = await SeedCompletedRefTestAsync(database, titleId);

        using var loggerProvider = new CapturingLoggerProvider();
        using var loggerFactory = CreateLoggerFactory(loggerProvider);

        await using var context = database.CreateContext();
        var result = await CreateHandler(
            context,
            ThrowingJobEnqueueService.ForReport(new InvalidOperationException("SMTP rejected john.doe@example.com")),
            loggerFactory,
            "trace-report").SendReportAsync(
            new SendReportCommand([refTestId]),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Failed to enqueue report job. Please try again later.", result.Message);
        AssertLoggedFailure(loggerProvider, "trace-report", "SendReportAsync");
    }

    private static RefTestEmailHandler CreateHandler(
        RefTestManagementContext context,
        IJobEnqueueService jobEnqueueService,
        ILoggerFactory loggerFactory,
        string correlationId) =>
        new(
            new RefTestRepository(
                context,
                new RefTestSessionTokenService(new EphemeralDataProtectionProvider(), TimeProvider.System)),
            context,
            jobEnqueueService,
            new ReportConfiguration { RecipientEmails = ["staff@example.org"] },
            new ScoreConfiguration(),
            new TestCurrentUser(correlationId),
            loggerFactory.CreateLogger<RefTestEmailHandler>());

    private sealed class TestCurrentUser(string correlationId) : ICurrentUser
    {
        public string DisplayName => "Test";
        public string Email => "test@example.org";
        public string CorrelationId => correlationId;
        public IReadOnlySet<string> Permissions => new HashSet<string>();
    }

    private static ILoggerFactory CreateLoggerFactory(CapturingLoggerProvider provider) =>
        LoggerFactory.Create(builder =>
            builder
                .AddProvider(provider)
                .SetMinimumLevel(LogLevel.Trace));

    private static void AssertLoggedFailure(
        CapturingLoggerProvider provider,
        string expectedCorrelationId,
        string expectedOperationName)
    {
        var loggedException = Assert.Single(provider.Exceptions);
        Assert.NotNull(loggedException);
        Assert.DoesNotContain("john.doe@example.com", loggedException.ToString(), StringComparison.Ordinal);
        Assert.Contains("j***@example.com", loggedException.ToString(), StringComparison.Ordinal);

        var message = Assert.Single(provider.Messages);
        Assert.Contains(expectedCorrelationId, message, StringComparison.Ordinal);
        Assert.Contains(expectedOperationName, message, StringComparison.Ordinal);
    }

    private static async Task<(SqliteTestDatabase Database, Guid TitleId)> SeedTitleAsync()
    {
        var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();
        context.RefTestTitles.Add(RefTestTitle.Create("Season 2026"));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (database, await context.RefTestTitles.Select(t => t.Id).SingleAsync(TestContext.Current.CancellationToken));
    }

    private static RefTest NewRefTest(Guid titleId) =>
        RefTest.Create(
            titleId: titleId,
            firstName: "Ada",
            lastName: "Lovelace",
            email: "ada@example.org",
            numberOfQuestions: 10,
            maxTimeInMinutes: 30,
            questionIds: ["q1", "q2"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: false,
            requiresApproval: false);

    private static async Task<Guid> SeedPendingRefTestAsync(SqliteTestDatabase database, Guid titleId)
    {
        await using var context = database.CreateContext();
        var refTest = NewRefTest(titleId);
        context.RefTests.Add(refTest);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return refTest.Id;
    }

    private static async Task<Guid> SeedCompletedRefTestAsync(SqliteTestDatabase database, Guid titleId)
    {
        await using var context = database.CreateContext();
        var refTest = NewRefTest(titleId);
        refTest.AcceptPrivacyNotice("v1.0");
        refTest.Start("v1.0");
        refTest.Complete(
            questionScore: 8,
            answerScore: 8,
            answerTotal: 10,
            percentage: 80,
            selectedAnswerIds: ["a1"],
            wrongQuestionIds: ["q2"],
            wrongAnswerIds: ["a2"],
            language: "en",
            source: RefTestCompletionSource.Participant);
        context.RefTests.Add(refTest);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return refTest.Id;
    }

    private sealed class ThrowingJobEnqueueService : IJobEnqueueService
    {
        private readonly Exception? _invitationException;
        private readonly Exception? _resultException;
        private readonly Exception? _reportException;

        private ThrowingJobEnqueueService(Exception? invitationException, Exception? resultException, Exception? reportException)
        {
            _invitationException = invitationException;
            _resultException = resultException;
            _reportException = reportException;
        }

        public static ThrowingJobEnqueueService ForInvitation(Exception exception) =>
            new(exception, null, null);

        public static ThrowingJobEnqueueService ForResult(Exception exception) =>
            new(null, exception, null);

        public static ThrowingJobEnqueueService ForReport(Exception exception) =>
            new(null, null, exception);

        public Task EnqueueInvitationEmailAsync(
            RefTest refTest,
            DateTime? executeAfter = null,
            bool saveChanges = true,
            IJobPersistenceContext? unitOfWorkContext = null,
            CancellationToken cancellationToken = default) =>
            Task.FromException(_invitationException ?? new NotSupportedException());

        public Task EnqueueResultEmailAsync(
            ResultEmailPayload payload,
            DateTime? executeAfter = null,
            bool saveChanges = true,
            IJobPersistenceContext? unitOfWorkContext = null,
            CancellationToken cancellationToken = default) =>
            Task.FromException(_resultException ?? new NotSupportedException());

        public Task EnqueueReportEmailAsync(
            ReportEmailPayload payload,
            DateTime? executeAfter = null,
            bool saveChanges = true,
            IJobPersistenceContext? unitOfWorkContext = null,
            CancellationToken cancellationToken = default) =>
            Task.FromException(_reportException ?? new NotSupportedException());

        public Task EnqueueRefTestExpirationAsync(
            RefTestExpirationPayload payload,
            DateTime? executeAfter = null,
            bool saveChanges = true,
            IJobPersistenceContext? unitOfWorkContext = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task EnqueueApprovalNotificationAsync(
            ApprovalNotificationEmailPayload payload,
            bool saveChanges = true,
            IJobPersistenceContext? unitOfWorkContext = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task EnqueueApprovalDecisionEmailAsync(
            ApprovalDecisionEmailPayload payload,
            bool saveChanges = true,
            IJobPersistenceContext? unitOfWorkContext = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task EnqueuePersonalDataExportChallengeEmailAsync(
            PersonalDataExportChallengeEmailPayload payload,
            bool saveChanges = true,
            IJobPersistenceContext? unitOfWorkContext = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task EnqueuePersonalDataExportDeliveryEmailAsync(
            PersonalDataExportDeliveryEmailPayload payload,
            bool saveChanges = true,
            IJobPersistenceContext? unitOfWorkContext = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task EnqueuePrivacyWithdrawalChallengeEmailAsync(
            PrivacyWithdrawalChallengeEmailPayload payload,
            bool saveChanges = true,
            IJobPersistenceContext? unitOfWorkContext = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Guid> EnqueuePrivacyWithdrawalBatchAsync(
            PrivacyWithdrawalBatchPayload payload,
            bool saveChanges = true,
            IJobPersistenceContext? unitOfWorkContext = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task CancelPendingJobsForRefTestAsync(
            Guid refTestId,
            bool saveChanges = true,
            IJobPersistenceContext? unitOfWorkContext = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task CancelPendingResultEmailsAsync(
            Guid refTestId,
            bool saveChanges = true,
            IJobPersistenceContext? unitOfWorkContext = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<Exception?> Exceptions { get; } = [];
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
                Func<TState, Exception?, string> formatter)
            {
                provider.Exceptions.Add(exception);
                provider.Messages.Add(formatter(state, exception));
            }
        }
    }
}
