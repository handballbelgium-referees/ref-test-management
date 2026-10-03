using System.Reflection;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Email;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class RefTestEmailMutationPersistenceTests
{
    [Fact]
    public async Task SendInvitationsAsync_PersistsManualInvitationAndContinuesAfterInvalidRefTest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();

        Guid invalidRefTestId;
        Guid pendingRefTestId;
        await using (var seedContext = database.CreateContext())
        {
            var title = RefTestTitle.Create("Season 2026");
            seedContext.RefTestTitles.Add(title);
            await seedContext.SaveChangesAsync(cancellationToken);

            var completedRefTest = NewCompletedRefTest(title.Id);
            var pendingRefTest = NewRefTest(title.Id, "Ada", "Lovelace", "ada@example.org");
            invalidRefTestId = completedRefTest.Id;
            pendingRefTestId = pendingRefTest.Id;
            seedContext.RefTests.AddRange(completedRefTest, pendingRefTest);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        var tokenProtection = new RefTestInvitationTokenProtection(new EphemeralDataProtectionProvider());
        await using var resolverContext = database.CreateContext();
        await using var jobServiceContext = database.CreateContext();
        var jobEnqueueService = new JobEnqueueService(
            jobServiceContext,
            tokenProtection,
            NullLogger<JobEnqueueService>.Instance);

        var result = await RefTestEmailMutations.SendInvitationsAsync(
            new SendInvitationsInput([invalidRefTestId, pendingRefTestId]),
            resolverContext,
            jobEnqueueService,
            NullLoggerFactory.Instance,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            cancellationToken);

        Assert.Equal(2, result.TotalRequested);
        Assert.Equal(1, result.SuccessfullySent);
        Assert.Equal(1, result.Failed);
        Assert.Equal(invalidRefTestId, Assert.Single(result.Errors).RefTestId);

        Guid jobId;
        string expectedToken;
        await using (var persistedContext = database.CreateContext())
        {
            var persistedRefTest = await persistedContext.RefTests
                .SingleAsync(refTest => refTest.Id == pendingRefTestId, cancellationToken);
            var queuedJob = await persistedContext.Jobs.SingleAsync(cancellationToken);
            Assert.Equal(JobType.InvitationEmail, queuedJob.JobType);

            using var payload = JsonDocument.Parse(queuedJob.Payload);
            Assert.Equal(
                pendingRefTestId,
                payload.RootElement.GetProperty("refTestId").GetGuid());
            Assert.Equal(
                persistedRefTest.Token,
                payload.RootElement.GetProperty("tokenHash").GetString());

            var protectedToken = Assert.IsType<string>(persistedRefTest.ProtectedInvitationToken);
            expectedToken = tokenProtection.Unprotect(protectedToken);
            Assert.Equal(persistedRefTest.Token, RefTest.HashToken(expectedToken));
            jobId = queuedJob.Id;
        }

        var emailService = new RecordingEmailService();
        var subscriptionService = DispatchProxy.Create<
            IRefTestSubscriptionService,
            NoOpRefTestSubscriptionServiceProxy>();
        await using var deliveryContext = database.CreateContext();
        var job = await deliveryContext.Jobs.SingleAsync(candidate => candidate.Id == jobId, cancellationToken);
        var handler = new InvitationEmailJobHandler(
            emailService,
            deliveryContext,
            subscriptionService,
            tokenProtection,
            NullLogger<InvitationEmailJobHandler>.Instance);

        await handler.HandleAsync(job, cancellationToken);

        Assert.Equal(1, emailService.SentCount);
        Assert.Equal(pendingRefTestId, emailService.SentRefTestId);
        Assert.Equal(expectedToken, emailService.SentToken);
        Assert.NotNull((await deliveryContext.RefTests
            .SingleAsync(refTest => refTest.Id == pendingRefTestId, cancellationToken)).InvitationSentAt);
    }

    [Fact]
    public async Task SendInvitationsAsync_ContinuesAfterSaveTimeConcurrencyFailure()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();

        Guid conflictedRefTestId;
        Guid laterRefTestId;
        string conflictedInitialToken;
        string laterInitialToken;
        await using (var seedContext = database.CreateContext())
        {
            var title = RefTestTitle.Create("Season 2026");
            seedContext.RefTestTitles.Add(title);
            await seedContext.SaveChangesAsync(cancellationToken);

            var conflictedRefTest = NewRefTest(title.Id, "Grace", "Hopper", "grace@example.org");
            var laterRefTest = NewRefTest(title.Id, "Ada", "Lovelace", "ada@example.org");
            conflictedRefTestId = conflictedRefTest.Id;
            laterRefTestId = laterRefTest.Id;
            conflictedInitialToken = conflictedRefTest.Token;
            laterInitialToken = laterRefTest.Token;
            seedContext.RefTests.AddRange(conflictedRefTest, laterRefTest);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        var tokenProtection = new RefTestInvitationTokenProtection(new EphemeralDataProtectionProvider());
        await using var resolverContext = database.CreateContext();
        await using var jobServiceContext = database.CreateContext();
        var innerJobEnqueueService = new JobEnqueueService(
            jobServiceContext,
            tokenProtection,
            NullLogger<JobEnqueueService>.Instance);
        var jobEnqueueService = DispatchProxy.Create<
            IJobEnqueueService,
            CompetingRefTestUpdateJobEnqueueProxy>();
        var competingUpdateProxy = (CompetingRefTestUpdateJobEnqueueProxy)(object)jobEnqueueService;
        competingUpdateProxy.Configure(innerJobEnqueueService, database, conflictedRefTestId);

        var result = await RefTestEmailMutations.SendInvitationsAsync(
            new SendInvitationsInput([conflictedRefTestId, laterRefTestId]),
            resolverContext,
            jobEnqueueService,
            NullLoggerFactory.Instance,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            cancellationToken);

        Assert.Equal(2, result.TotalRequested);
        Assert.Equal(1, result.SuccessfullySent);
        Assert.Equal(1, result.Failed);
        Assert.Equal(conflictedRefTestId, Assert.Single(result.Errors).RefTestId);
        Assert.True(competingUpdateProxy.CompetingUpdateSaved);
        Assert.IsType<DbUpdateConcurrencyException>(competingUpdateProxy.Failure);

        await using var persistedContext = database.CreateContext();
        var conflictedRefTestAfterFailure = await persistedContext.RefTests
            .SingleAsync(refTest => refTest.Id == conflictedRefTestId, cancellationToken);
        var laterRefTestAfterFailure = await persistedContext.RefTests
            .SingleAsync(refTest => refTest.Id == laterRefTestId, cancellationToken);
        Assert.True(conflictedRefTestAfterFailure.SendInvitationsAutomatically);
        Assert.Equal(conflictedInitialToken, conflictedRefTestAfterFailure.Token);
        Assert.Null(conflictedRefTestAfterFailure.ProtectedInvitationToken);
        Assert.NotEqual(laterInitialToken, laterRefTestAfterFailure.Token);

        var protectedToken = Assert.IsType<string>(laterRefTestAfterFailure.ProtectedInvitationToken);
        var issuedToken = tokenProtection.Unprotect(protectedToken);
        Assert.Equal(laterRefTestAfterFailure.Token, RefTest.HashToken(issuedToken));

        var queuedJob = await persistedContext.Jobs.SingleAsync(cancellationToken);
        Assert.Equal(JobType.InvitationEmail, queuedJob.JobType);
        using var payload = JsonDocument.Parse(queuedJob.Payload);
        Assert.Equal(laterRefTestId, payload.RootElement.GetProperty("refTestId").GetGuid());
        Assert.Equal(
            laterRefTestAfterFailure.Token,
            payload.RootElement.GetProperty("tokenHash").GetString());
    }

    private static RefTest NewRefTest(Guid titleId, string firstName, string lastName, string email) =>
        RefTest.Create(
            titleId: titleId,
            firstName: firstName,
            lastName: lastName,
            email: email,
            numberOfQuestions: 10,
            maxTimeInMinutes: 30,
            questionIds: ["q1", "q2"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: false,
            requiresApproval: false);

    private static RefTest NewCompletedRefTest(Guid titleId)
    {
        var refTest = NewRefTest(titleId, "Grace", "Hopper", "grace@example.org");
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
        return refTest;
    }

    public class CompetingRefTestUpdateJobEnqueueProxy : DispatchProxy
    {
        private IJobEnqueueService? _innerService;
        private SqliteTestDatabase? _database;
        private Guid _refTestId;
        private bool _competingUpdateSaved;

        public Exception? Failure { get; private set; }
        public bool CompetingUpdateSaved => _competingUpdateSaved;

        internal void Configure(
            IJobEnqueueService innerService,
            SqliteTestDatabase database,
            Guid refTestId)
        {
            _innerService = innerService;
            _database = database;
            _refTestId = refTestId;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null || args is null)
                throw new InvalidOperationException("Expected an invocation of the job enqueue service.");

            var innerService = _innerService
                ?? throw new InvalidOperationException("The job enqueue service proxy is not configured.");

            return targetMethod.Name == nameof(IJobEnqueueService.EnqueueInvitationEmailAsync)
                ? EnqueueInvitationWithCompetingUpdateAsync(innerService, targetMethod, args)
                : targetMethod.Invoke(innerService, args);
        }

        private async Task EnqueueInvitationWithCompetingUpdateAsync(
            IJobEnqueueService innerService,
            MethodInfo targetMethod,
            object?[] args)
        {
            try
            {
                if (!_competingUpdateSaved)
                {
                    var database = _database
                        ?? throw new InvalidOperationException("The job enqueue service proxy is not configured.");
                    var cancellationToken = args.Length > 4 && args[4] is CancellationToken token
                        ? token
                        : default;

                    await using var competingContext = database.CreateContext();
                    var refTest = await competingContext.RefTests
                        .SingleAsync(candidate => candidate.Id == _refTestId, cancellationToken);
                    refTest.UpdateNotificationSettings(
                        sendInvitationsAutomatically: true,
                        sendResultsAutomatically: false);
                    await competingContext.SaveChangesAsync(cancellationToken);
                    _competingUpdateSaved = true;
                }

                var enqueueTask = targetMethod.Invoke(innerService, args) as Task
                    ?? throw new InvalidOperationException("Invitation enqueue did not return a task.");
                await enqueueTask;
            }
            catch (Exception exception)
            {
                Failure = exception;
                throw;
            }
        }
    }

    private sealed class RecordingEmailService : IEmailService
    {
        public Guid SentRefTestId { get; private set; }
        public string? SentToken { get; private set; }
        public int SentCount { get; private set; }

        public Task SendRefTestInvitationAsync(
            Guid refTestId,
            string name,
            string email,
            string token,
            int numberOfQuestions,
            int maxTimeInMinutes,
            CancellationToken cancellationToken)
        {
            SentRefTestId = refTestId;
            SentToken = token;
            SentCount++;
            return Task.CompletedTask;
        }

        public Task SendRefTestResultsAsync(
            Guid refTestId,
            string name,
            string email,
            int questionScore,
            int answerScore,
            int totalQuestions,
            int answerTotal,
            double percentage,
            List<string> selectedAnswerIds,
            List<string> wrongQuestionIds,
            List<string> wrongAnswerIds,
            List<Question> questionsWithCorrectAnswers,
            bool scheduleEmail,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SendReportEmailAsync(
            string recipientEmail,
            byte[] excelReport,
            byte[] pdfReport,
            string timestamp,
            int refTestCount,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SendApprovalNotificationAsync(
            string approverName,
            string approverEmail,
            string creatorName,
            string? titleValue,
            List<(string FullName, string Email, DateTime? ScheduledAt)> refTestItems,
            string baseUrl,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SendApprovalDecisionAsync(
            string creatorName,
            string creatorEmail,
            string approverName,
            bool isApproved,
            string? rejectionReason,
            string? titleValue,
            List<(string FullName, string Email, DateTime? ScheduledAt)> refTestItems,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SendPersonalDataExportVerificationAsync(
            string recipientEmail,
            string challengeKey,
            DateTime expiresAt,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SendPrivacyWithdrawalVerificationAsync(
            string recipientEmail,
            string challengeKey,
            DateTime expiresAt,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> SendPersonalDataExportAsync(
            string recipientEmail,
            IReadOnlyList<EmailAttachment> attachments,
            Func<CancellationToken, Task<bool>> finalDeliverabilityCheck,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    public class NoOpRefTestSubscriptionServiceProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType == typeof(Task)
                ? Task.CompletedTask
                : throw new NotSupportedException($"Unexpected {targetMethod?.Name} call.");
    }
}
