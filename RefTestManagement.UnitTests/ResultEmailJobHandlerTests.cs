using System.Reflection;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class ResultEmailJobHandlerTests
{
    [Fact]
    public async Task MarksResultsSentOnlyAfterProviderAcceptance()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();
        var (refTest, job) = await CreateResultJobAsync(context, cancellationToken);
        var handler = CreateHandler(context, wasAccepted: true);

        await handler.HandleAsync(job, cancellationToken);

        Assert.NotNull(refTest.ResultsSentAt);
    }

    [Fact]
    public async Task RejectedResultsRemainUnsentAndCanBeRetried()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();
        var (refTest, job) = await CreateResultJobAsync(context, cancellationToken);
        var handler = CreateHandler(context, wasAccepted: false);

        await Assert.ThrowsAsync<EmailException>(() => handler.HandleAsync(job, cancellationToken));

        Assert.Null(refTest.ResultsSentAt);
        Assert.Equal(JobStatus.Pending, job.Status);
    }

    [Fact]
    public async Task SubmissionExceptionLeavesResultsUnsent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();
        var (refTest, job) = await CreateResultJobAsync(context, cancellationToken);
        var handler = CreateHandler(
            context,
            wasAccepted: false,
            new HttpRequestException("provider request failed"));

        await Assert.ThrowsAsync<HttpRequestException>(() => handler.HandleAsync(job, cancellationToken));

        Assert.Null(refTest.ResultsSentAt);
    }

    private static ResultEmailJobHandler CreateHandler(
        RefTestManagementContext context,
        bool wasAccepted,
        Exception? failure = null)
    {
        var emailService = DispatchProxy.Create<IEmailService, EmailServiceProxy>();
        var emailProxy = (EmailServiceProxy)(object)emailService;
        emailProxy.WasAccepted = wasAccepted;
        emailProxy.Failure = failure;

        return new ResultEmailJobHandler(
            emailService,
            DispatchProxy.Create<IIhfRulesQuestionsService, QuestionServiceProxy>(),
            context,
            DispatchProxy.Create<IRefTestSubscriptionService, NoOpSubscriptionProxy>(),
            NullLogger<ResultEmailJobHandler>.Instance);
    }

    private static async Task<(RefTest RefTest, Job Job)> CreateResultJobAsync(
        RefTestManagementContext context,
        CancellationToken cancellationToken)
    {
        var title = RefTestTitle.Create("Season opener");
        context.RefTestTitles.Add(title);
        await context.SaveChangesAsync(cancellationToken);

        var refTest = RefTest.Create(
            title.Id,
            "Ada",
            "Lovelace",
            "ada@example.org",
            numberOfQuestions: 10,
            maxTimeInMinutes: 30,
            questionIds: ["q1"],
            sendInvitationAutomatically: true,
            sendResultsAutomatically: true);
        var payload = new ResultEmailPayload(
            refTest.Id,
            refTest.FullName,
            refTest.Email,
            QuestionScore: 8,
            AnswerScore: 12,
            TotalQuestions: 10,
            AnswerTotal: 15,
            Percentage: 80,
            SelectedAnswerIds: [],
            WrongQuestionIds: [],
            WrongAnswerIds: []);
        var job = Job.Create(JobType.ResultEmail, JsonSerializer.Serialize(payload));
        context.RefTests.Add(refTest);
        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);
        return (refTest, job);
    }

    public class EmailServiceProxy : DispatchProxy
    {
        public bool WasAccepted { get; set; }
        public Exception? Failure { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IEmailService.SendRefTestResultsAsync))
                throw new NotSupportedException();

            if (Failure is not null)
                throw Failure;

            return Task.FromResult(WasAccepted);
        }
    }

    public class QuestionServiceProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IIhfRulesQuestionsService.GetQuestionsByIdAsync))
                throw new NotSupportedException();

            return Task.FromResult(new List<Question>());
        }
    }

    public class NoOpSubscriptionProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Task.CompletedTask;
    }
}
