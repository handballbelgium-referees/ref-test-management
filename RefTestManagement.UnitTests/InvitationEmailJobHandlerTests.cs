using System.Reflection;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class InvitationEmailJobHandlerTests
{
    [Fact]
    public async Task MarksInvitationSentOnlyAfterProviderAcceptance()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();
        var tokenProtection = new RefTestInvitationTokenProtection(new EphemeralDataProtectionProvider());
        var (refTest, job) = await CreateInvitationJobAsync(context, tokenProtection, ct);
        var emailService = CreateEmailService(wasAccepted: true);
        var handler = new InvitationEmailJobHandler(
            emailService,
            context,
            CreateSubscriptionService(),
            tokenProtection,
            NullLogger<InvitationEmailJobHandler>.Instance);

        await handler.HandleAsync(job, ct);

        Assert.NotNull(refTest.InvitationSentAt);
    }

    [Fact]
    public async Task RejectedInvitationRemainsUnsentAndCanBeRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();
        var tokenProtection = new RefTestInvitationTokenProtection(new EphemeralDataProtectionProvider());
        var (refTest, job) = await CreateInvitationJobAsync(context, tokenProtection, ct);
        var handler = new InvitationEmailJobHandler(
            CreateEmailService(wasAccepted: false),
            context,
            CreateSubscriptionService(),
            tokenProtection,
            NullLogger<InvitationEmailJobHandler>.Instance);

        await Assert.ThrowsAsync<EmailException>(() => handler.HandleAsync(job, ct));

        Assert.Null(refTest.InvitationSentAt);
        Assert.Equal(JobStatus.Pending, job.Status);
    }

    [Fact]
    public async Task SubmissionExceptionLeavesInvitationUnsent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();
        var tokenProtection = new RefTestInvitationTokenProtection(new EphemeralDataProtectionProvider());
        var (refTest, job) = await CreateInvitationJobAsync(context, tokenProtection, ct);
        var handler = new InvitationEmailJobHandler(
            CreateEmailService(wasAccepted: false, new HttpRequestException("provider request failed")),
            context,
            CreateSubscriptionService(),
            tokenProtection,
            NullLogger<InvitationEmailJobHandler>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() => handler.HandleAsync(job, ct));

        Assert.Null(refTest.InvitationSentAt);
    }

    [Fact]
    public async Task SkipsInvitationWhenTheTokenHasBeenRotated()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();

        var title = RefTestTitle.Create("Season opener");
        context.RefTestTitles.Add(title);
        await context.SaveChangesAsync(ct);

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
        var staleToken = refTest.GetIssuedToken();
        refTest.RegenerateToken();
        var tokenProtection = new RefTestInvitationTokenProtection(new EphemeralDataProtectionProvider());
        refTest.StoreProtectedInvitationToken(tokenProtection.Protect(refTest.GetIssuedToken()));

        var payload = new InvitationEmailPayload(
            refTest.Id,
            refTest.FullName,
            refTest.Email,
            RefTest.HashToken(staleToken),
            refTest.NumberOfQuestions,
            refTest.MaxTimeInMinutes);
        var job = Job.Create(JobType.InvitationEmail, JsonSerializer.Serialize(payload));
        context.RefTests.Add(refTest);
        context.Jobs.Add(job);
        await context.SaveChangesAsync(ct);

        var handler = new InvitationEmailJobHandler(
            null!,
            context,
            null!,
            tokenProtection,
            NullLogger<InvitationEmailJobHandler>.Instance);

        await handler.HandleAsync(job, ct);

        Assert.Null(refTest.InvitationSentAt);
        Assert.NotNull(refTest.ProtectedInvitationToken);
    }

    private static async Task<(RefTest RefTest, Job Job)> CreateInvitationJobAsync(
        RefTestManagementContext context,
        RefTestInvitationTokenProtection tokenProtection,
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
        var token = refTest.GetIssuedToken();
        refTest.StoreProtectedInvitationToken(tokenProtection.Protect(token));
        var payload = new InvitationEmailPayload(
            refTest.Id,
            refTest.FullName,
            refTest.Email,
            RefTest.HashToken(token),
            refTest.NumberOfQuestions,
            refTest.MaxTimeInMinutes);
        var job = Job.Create(JobType.InvitationEmail, JsonSerializer.Serialize(payload));
        context.RefTests.Add(refTest);
        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);
        return (refTest, job);
    }

    private static IEmailService CreateEmailService(bool wasAccepted, Exception? failure = null)
    {
        var emailService = DispatchProxy.Create<IEmailService, EmailServiceProxy>();
        var proxy = (EmailServiceProxy)(object)emailService;
        proxy.WasAccepted = wasAccepted;
        proxy.Failure = failure;
        return emailService;
    }

    private static IRefTestSubscriptionService CreateSubscriptionService() =>
        DispatchProxy.Create<IRefTestSubscriptionService, NoOpSubscriptionProxy>();

    public class EmailServiceProxy : DispatchProxy
    {
        public bool WasAccepted { get; set; }
        public Exception? Failure { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IEmailService.SendRefTestInvitationAsync))
                throw new NotSupportedException();

            if (Failure is not null)
                throw Failure;

            return Task.FromResult(WasAccepted);
        }
    }

    public class NoOpSubscriptionProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Task.CompletedTask;
    }
}
