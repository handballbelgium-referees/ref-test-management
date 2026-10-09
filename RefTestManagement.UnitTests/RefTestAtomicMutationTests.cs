using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Approval;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Reset;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;
using Handball.Belgium.RefTestManagement.Application.RefTests.Creation;
using Handball.Belgium.RefTestManagement.Application.RefTests.Approval;
using Handball.Belgium.RefTestManagement.Application.RefTests.Reset;
using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Persistence;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Infrastructure.Security;
using Handball.Belgium.RefTestManagement.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class RefTestAtomicMutationTests
{
    private static readonly JsonSerializerOptions JobJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static RefTestInvitationTokenProtection TokenProtection() =>
        new(new EphemeralDataProtectionProvider());

    private static HttpContextAccessor HttpContextAccessor(bool canApprove = false)
    {
        var claims = new List<Claim>
        {
            new("name", "Test Creator"),
            new("email", "creator@example.org")
        };
        if (canApprove)
            claims.Add(new Claim("permissions", Permissions.RefTests.Approve));

        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            }
        };
    }

    private static AuditSaveChangesInterceptor AuditInterceptor(IHttpContextAccessor accessor) =>
        new(accessor, new AuditLogOptions());

    private static async Task<Guid> SeedTitleAsync(
        SqliteTestDatabase database,
        CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        var title = RefTestTitle.Create("Season 2026");
        context.RefTestTitles.Add(title);
        await context.SaveChangesAsync(cancellationToken);
        return title.Id;
    }

    private static CreateRefTestsCommand CreateCommand(
        Guid titleId,
        IReadOnlyList<CreateRefTestUser> users,
        bool requiresApproval) =>
        new(titleId, null, users, 2, false, 30, null, true, true, null, requiresApproval);

    private static RefTestApprovalHandler CreateApprovalHandler(
        RefTestManagementContext context,
        IHttpContextAccessor accessor,
        IJobEnqueueService jobService) =>
        new(
            new RefTestRepository(context, new RefTestSessionTokenService(
                new EphemeralDataProtectionProvider(), TimeProvider.System)),
            (IUnitOfWork)context,
            jobService,
            SubscriptionService(),
            new HttpCurrentUser(accessor),
            NullLogger<RefTestApprovalHandler>.Instance);

    private static RefTestResetHandler CreateResetHandler(
        RefTestManagementContext context,
        IHttpContextAccessor accessor,
        IJobEnqueueService jobService) =>
        new(
            new RefTestRepository(context, new RefTestSessionTokenService(
                new EphemeralDataProtectionProvider(), TimeProvider.System)),
            (IUnitOfWork)context,
            jobService,
            SubscriptionService(),
            new HttpCurrentUser(accessor),
            NullLogger<RefTestResetHandler>.Instance);

    private static RefTestCreationHandler CreateHandler(
        RefTestManagementContext context,
        IHttpContextAccessor accessor,
        IJobEnqueueService jobService) =>
        new(
            new RefTestRepository(context, new RefTestSessionTokenService(
                new EphemeralDataProtectionProvider(), TimeProvider.System)),
            new RefTestTitleRepository(context),
            (IUnitOfWork)context,
            QuestionService(),
            jobService,
            SubscriptionService(),
            new HttpCurrentUser(accessor),
            NullLogger<RefTestCreationHandler>.Instance);

    [Fact]
    public async Task CreateRefTestsAsync_DropsOnlyItemsWhoseInvitationCouldNotBeEnqueued()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database, cancellationToken);
        var accessor = HttpContextAccessor(canApprove: true);
        var tokenProtection = TokenProtection();

        await using var context = database.CreateContext(AuditInterceptor(accessor));
        var innerJobService = new JobEnqueueService(
            context, tokenProtection, NullLogger<JobEnqueueService>.Instance);
        var jobService = FailAfterStaging(
            innerJobService,
            nameof(IJobEnqueueService.EnqueueInvitationEmailAsync),
            args => args[0] is RefTest { FirstName: "Grace" });

        var result = await CreateHandler(context, accessor, jobService).HandleAsync(
            CreateCommand(titleId,
            [
                new CreateRefTestUser("Grace", "Hopper", "grace@example.org"),
                new CreateRefTestUser("Ada", "Lovelace", "ada@example.org")
            ],
            requiresApproval: false),
            cancellationToken);

        Assert.Equal(2, result.TotalRequested);
        Assert.Equal(1, result.SuccessfullyCreated);
        Assert.Equal(1, result.Failed);
        Assert.Equal("Grace", Assert.Single(result.Errors).User.FirstName);
        Assert.Equal("Ada", Assert.Single(result.CreatedRefTests).FirstName);

        await using var observer = database.CreateContext();
        var storedRefTest = await observer.RefTests.SingleAsync(cancellationToken);
        Assert.Equal("Ada", storedRefTest.FirstName);

        var invitationJob = await observer.Jobs.SingleAsync(cancellationToken);
        using var payload = JsonDocument.Parse(invitationJob.Payload);
        Assert.Equal(
            storedRefTest.Id,
            payload.RootElement.GetProperty("refTestId").GetGuid());
        Assert.Equal("Ada Lovelace", payload.RootElement.GetProperty("name").GetString());

        // The failed item never entered the unit of work, so it also has no audit record.
        Assert.Equal(2, await observer.AuditEvents.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task CreateRefTestsAsync_FailsApprovalBatchWhenItsNotificationCannotBeEnqueued()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database, cancellationToken);
        var accessor = HttpContextAccessor();

        await using var context = database.CreateContext(AuditInterceptor(accessor));
        var innerJobService = new JobEnqueueService(
            context, TokenProtection(), NullLogger<JobEnqueueService>.Instance);
        var jobService = FailAfterStaging(
            innerJobService,
            nameof(IJobEnqueueService.EnqueueApprovalNotificationAsync),
            _ => true);

        var result = await CreateHandler(context, accessor, jobService).HandleAsync(
            CreateCommand(titleId,
            [
                new CreateRefTestUser("Grace", "Hopper", "grace@example.org"),
                new CreateRefTestUser("Ada", "Lovelace", "ada@example.org")
            ],
            requiresApproval: true),
            cancellationToken);

        Assert.Equal(2, result.TotalRequested);
        Assert.Equal(0, result.SuccessfullyCreated);
        Assert.Equal(2, result.Failed);
        Assert.Equal(2, result.Errors.Count);
        Assert.Empty(result.CreatedRefTests);

        await using var observer = database.CreateContext();
        Assert.Empty(await observer.RefTests.ToListAsync(cancellationToken));
        Assert.Empty(await observer.Jobs.ToListAsync(cancellationToken));
        Assert.Empty(await observer.AuditEvents.ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task ResetRefTestsAsync_RollsBackFailedItemsAndCountsOnlyCommittedResets()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database, cancellationToken);

        var failedRefTest = RefTest.Create(
            titleId, "Grace", "Hopper", "grace@example.org",
            numberOfQuestions: 2, maxTimeInMinutes: 30, questionIds: ["q1", "q2"],
            sendInvitationAutomatically: true, sendResultsAutomatically: true);
        var resetRefTest = RefTest.Create(
            titleId, "Ada", "Lovelace", "ada@example.org",
            numberOfQuestions: 2, maxTimeInMinutes: 30, questionIds: ["q1", "q2"],
            sendInvitationAutomatically: true, sendResultsAutomatically: true);
        resetRefTest.SendInvitation();
        resetRefTest.AcceptPrivacyNotice("v1");
        resetRefTest.Start("v1");

        var failedResultJob = Job.Create(
            JobType.ResultEmail,
            JsonSerializer.Serialize(new ResultEmailPayload(
                failedRefTest.Id, failedRefTest.FullName, failedRefTest.Email,
                1, 1, 2, 2, 50, [], [], []), JobJsonOptions));
        var resetResultJob = Job.Create(
            JobType.ResultEmail,
            JsonSerializer.Serialize(new ResultEmailPayload(
                resetRefTest.Id, resetRefTest.FullName, resetRefTest.Email,
                1, 1, 2, 2, 50, [], [], []), JobJsonOptions));

        await using (var seed = database.CreateContext())
        {
            seed.RefTests.AddRange(failedRefTest, resetRefTest);
            seed.Jobs.AddRange(failedResultJob, resetResultJob);
            await seed.SaveChangesAsync(cancellationToken);
        }

        var accessor = HttpContextAccessor();
        await using (var context = database.CreateContext(AuditInterceptor(accessor)))
        {
            var result = await CreateResetHandler(
                    context, accessor,
                    new JobEnqueueService(context, TokenProtection(), NullLogger<JobEnqueueService>.Instance))
                .ResetAsync(
                    [failedRefTest.Id, resetRefTest.Id], RefTestResetType.Soft, regenerateToken: false,
                    cancellationToken);

            Assert.Equal(2, result.TotalRequested);
            Assert.Equal(1, result.SuccessfullyReset);
            Assert.Single(result.Errors);
            Assert.Equal(failedRefTest.Id, Assert.Single(result.Errors).RefTestId);
            Assert.Equal(resetRefTest.Id, Assert.Single(result.ResetRefTests).Id);
        }

        await using var observer = database.CreateContext();
        var storedFailedRefTest = await observer.RefTests.SingleAsync(
            refTest => refTest.Id == failedRefTest.Id, cancellationToken);
        Assert.Equal(RefTestStatus.Pending, storedFailedRefTest.Status);
        Assert.Null(storedFailedRefTest.StartedAt);

        var storedResetRefTest = await observer.RefTests.SingleAsync(
            refTest => refTest.Id == resetRefTest.Id, cancellationToken);
        Assert.Equal(RefTestStatus.Pending, storedResetRefTest.Status);
        Assert.Null(storedResetRefTest.StartedAt);

        var jobs = await observer.Jobs.ToListAsync(cancellationToken);
        Assert.Equal(JobStatus.Pending, jobs.Single(job => job.Id == failedResultJob.Id).Status);
        Assert.Equal(JobStatus.Cancelled, jobs.Single(job => job.Id == resetResultJob.Id).Status);

        var auditEvents = await observer.AuditEvents.ToListAsync(cancellationToken);
        Assert.DoesNotContain(auditEvents, audit => audit.StreamId == failedRefTest.Id.ToString());
        Assert.Contains(auditEvents, audit => audit.StreamId == resetRefTest.Id.ToString());
    }

    [Fact]
    public async Task ApproveRefTestsAsync_RollsBackFailedItemsAndRequiresInvitationJobs()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database, cancellationToken);

        var invalidRefTest = RefTest.Create(
            titleId, "Invalid", "Status", "invalid@example.org",
            numberOfQuestions: 2, maxTimeInMinutes: 30, questionIds: ["q1", "q2"],
            sendInvitationAutomatically: true, sendResultsAutomatically: true,
            creatorName: "Test Creator", creatorEmail: "creator@example.org");
        var failedInvitationRefTest = RefTest.Create(
            titleId, "Grace", "Hopper", "grace@example.org",
            numberOfQuestions: 2, maxTimeInMinutes: 30, questionIds: ["q1", "q2"],
            sendInvitationAutomatically: true, sendResultsAutomatically: true,
            requiresApproval: true,
            creatorName: "Test Creator", creatorEmail: "creator@example.org");
        var approvedRefTest = RefTest.Create(
            titleId, "Ada", "Lovelace", "ada@example.org",
            numberOfQuestions: 2, maxTimeInMinutes: 30, questionIds: ["q1", "q2"],
            sendInvitationAutomatically: true, sendResultsAutomatically: true,
            requiresApproval: true,
            creatorName: "Test Creator", creatorEmail: "creator@example.org");
        var failedInvitationToken = failedInvitationRefTest.Token;

        await using (var seed = database.CreateContext())
        {
            seed.RefTests.AddRange(invalidRefTest, failedInvitationRefTest, approvedRefTest);
            await seed.SaveChangesAsync(cancellationToken);
        }

        var accessor = HttpContextAccessor(canApprove: true);
        var tokenProtection = TokenProtection();
        await using (var context = database.CreateContext(AuditInterceptor(accessor)))
        {
            var innerJobService = new JobEnqueueService(
                context, tokenProtection, NullLogger<JobEnqueueService>.Instance);
            var jobService = FailAfterStaging(
                innerJobService,
                nameof(IJobEnqueueService.EnqueueInvitationEmailAsync),
                args => args[0] is RefTest { FirstName: "Grace" });

            var result = await CreateApprovalHandler(context, accessor, jobService).ApproveAsync(
                [invalidRefTest.Id, failedInvitationRefTest.Id, approvedRefTest.Id], cancellationToken);

            Assert.Equal(3, result.TotalRequested);
            Assert.Single(result.ApprovedRefTests);
            Assert.Equal(2, result.Errors.Count);
            Assert.Equal(2, result.Errors.Count);
            Assert.Equal(approvedRefTest.Id, Assert.Single(result.ApprovedRefTests).Id);
            Assert.Contains(result.Errors, error => error.RefTestId == invalidRefTest.Id);
            Assert.Contains(result.Errors, error => error.RefTestId == failedInvitationRefTest.Id);
        }

        await using var observer = database.CreateContext();
        var storedInvalidRefTest = await observer.RefTests.SingleAsync(
            refTest => refTest.Id == invalidRefTest.Id, cancellationToken);
        Assert.Equal(RefTestStatus.Pending, storedInvalidRefTest.Status);

        var storedFailedInvitationRefTest = await observer.RefTests.SingleAsync(
            refTest => refTest.Id == failedInvitationRefTest.Id, cancellationToken);
        Assert.Equal(RefTestStatus.PendingApproval, storedFailedInvitationRefTest.Status);
        Assert.Equal(failedInvitationToken, storedFailedInvitationRefTest.Token);
        Assert.Null(storedFailedInvitationRefTest.ProtectedInvitationToken);

        var storedApprovedRefTest = await observer.RefTests.SingleAsync(
            refTest => refTest.Id == approvedRefTest.Id, cancellationToken);
        Assert.Equal(RefTestStatus.Pending, storedApprovedRefTest.Status);
        Assert.NotEqual(approvedRefTest.Token, storedApprovedRefTest.Token);

        var jobs = await observer.Jobs.ToListAsync(cancellationToken);
        var invitationJob = Assert.Single(jobs, job => job.JobType == JobType.InvitationEmail);
        using (var payload = JsonDocument.Parse(invitationJob.Payload))
            Assert.Equal(
                storedApprovedRefTest.Id,
                payload.RootElement.GetProperty("refTestId").GetGuid());
        Assert.Single(jobs, job => job.JobType == JobType.ApprovalDecisionEmail);

        var auditEvents = await observer.AuditEvents.ToListAsync(cancellationToken);
        Assert.DoesNotContain(auditEvents, audit =>
            audit.StreamId == invalidRefTest.Id.ToString() ||
            audit.StreamId == failedInvitationRefTest.Id.ToString());
        Assert.Contains(auditEvents, audit => audit.StreamId == approvedRefTest.Id.ToString());
    }

    [Fact]
    public async Task ReviveRefTestsAsync_RollsBackFailedItemsAndCommitsSuccessfulItems()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database, cancellationToken);
        var tokenProtection = TokenProtection();
        var fixtures = await SeedExpiredRefTestsAsync(database, titleId, cancellationToken);
        var accessor = HttpContextAccessor();

        await using var context = database.CreateContext(AuditInterceptor(accessor));
        var innerJobService = new JobEnqueueService(
            context, tokenProtection, NullLogger<JobEnqueueService>.Instance);
        var jobService = FailAfterStaging(
            innerJobService,
            nameof(IJobEnqueueService.EnqueueInvitationEmailAsync),
            args => args[0] is RefTest refTest && refTest.Id == fixtures[0].Id);

        var result = await CreateResetHandler(context, accessor, jobService)
            .ReviveAsync([fixtures[0].Id, fixtures[1].Id], cancellationToken);

        Assert.Equal(2, result.TotalRequested);
        Assert.Single(result.RevivedRefTests);
        Assert.Single(result.Errors);
        Assert.Equal(fixtures[0].Id, Assert.Single(result.Errors).RefTestId);
        Assert.Equal(fixtures[1].Id, Assert.Single(result.RevivedRefTests).Id);
        var restoredRefTest = context.RefTests.Local.Single(refTest => refTest.Id == fixtures[0].Id);
        Assert.Equal(fixtures[0].Token, restoredRefTest.Token);
        Assert.Empty(restoredRefTest.DomainEvents);

        await using var observer = database.CreateContext();
        var failedRefTest = await observer.RefTests.SingleAsync(
            refTest => refTest.Id == fixtures[0].Id, cancellationToken);
        Assert.Equal(RefTestStatus.Expired, failedRefTest.Status);
        Assert.Equal(fixtures[0].Token, failedRefTest.Token);
        Assert.Equal(fixtures[0].CreatedAt, failedRefTest.CreatedAt);
        Assert.Equal(fixtures[0].ExpiredAt, failedRefTest.ExpiredAt);
        Assert.NotNull(failedRefTest.InvitationSentAt);
        Assert.Null(failedRefTest.ProtectedInvitationToken);

        var revivedRefTest = await observer.RefTests.SingleAsync(
            refTest => refTest.Id == fixtures[1].Id, cancellationToken);
        Assert.Equal(RefTestStatus.Pending, revivedRefTest.Status);
        Assert.NotEqual(fixtures[1].Token, revivedRefTest.Token);
        Assert.Null(revivedRefTest.ExpiredAt);
        Assert.Null(revivedRefTest.InvitationSentAt);
        var protectedToken = Assert.IsType<string>(revivedRefTest.ProtectedInvitationToken);
        Assert.Equal(
            revivedRefTest.Token,
            RefTest.HashToken(tokenProtection.Unprotect(protectedToken)));

        var jobs = await observer.Jobs.ToListAsync(cancellationToken);
        Assert.Equal(
            JobStatus.Pending,
            jobs.Single(job => job.Id == fixtures[0].InvitationJobId).Status);
        Assert.Equal(
            JobStatus.Cancelled,
            jobs.Single(job => job.Id == fixtures[1].InvitationJobId).Status);
        var newInvitationJob = Assert.Single(
            jobs,
            job => job.Id != fixtures[0].InvitationJobId && job.Id != fixtures[1].InvitationJobId);
        using var payload = JsonDocument.Parse(newInvitationJob.Payload);
        Assert.Equal(fixtures[1].Id, payload.RootElement.GetProperty("refTestId").GetGuid());
        Assert.Equal(
            revivedRefTest.Token,
            payload.RootElement.GetProperty("tokenHash").GetString());

        var auditEvents = await observer.AuditEvents.ToListAsync(cancellationToken);
        Assert.DoesNotContain(auditEvents, audit => audit.StreamId == fixtures[0].Id.ToString());
        Assert.Contains(auditEvents, audit =>
            audit.StreamId == fixtures[1].Id.ToString() && audit.Type == "RefTestRevived");
    }

    [Fact]
    public async Task AuditSaveChangesInterceptor_RetainsDomainEventsWhenSaveFails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database, cancellationToken);
        var accessor = HttpContextAccessor();
        var refTest = RefTest.Create(
            titleId,
            "Ada",
            "Lovelace",
            "ada@example.org",
            numberOfQuestions: 2,
            maxTimeInMinutes: 30,
            questionIds: ["q1", "q2"],
            sendInvitationAutomatically: true,
            sendResultsAutomatically: true);
        var duplicateJob = Job.Create(JobType.InvitationEmail, "{}");

        await using var context = database.CreateContext(AuditInterceptor(accessor));
        context.RefTests.Add(refTest);
        context.Jobs.Add(duplicateJob);

        await using (var seeder = database.CreateContext())
        {
            seeder.Jobs.Add(duplicateJob);
            await seeder.SaveChangesAsync(cancellationToken);
        }

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));

        Assert.NotEmpty(refTest.DomainEvents);
        Assert.DoesNotContain(
            context.ChangeTracker.Entries<AuditEvent>(),
            entry => entry.State == EntityState.Added);
        await using (var observer = database.CreateContext())
        {
            Assert.Empty(await observer.RefTests.ToListAsync(cancellationToken));
            Assert.Empty(await observer.AuditEvents.ToListAsync(cancellationToken));
        }

        context.Entry(duplicateJob).State = EntityState.Detached;
        await context.SaveChangesAsync(cancellationToken);
        Assert.Empty(refTest.DomainEvents);

        await using var verification = database.CreateContext();
        var auditEvent = Assert.Single(await verification.AuditEvents.ToListAsync(cancellationToken));
        Assert.Equal(refTest.Id.ToString(), auditEvent.StreamId);
        Assert.Equal("RefTestCreated", auditEvent.Type);
    }

    private static async Task<ExpiredRefTestFixture[]> SeedExpiredRefTestsAsync(
        SqliteTestDatabase database,
        Guid titleId,
        CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        var fixtures = new List<ExpiredRefTestFixture>();

        foreach (var (firstName, lastName, email) in new[]
                 {
                     ("Grace", "Hopper", "grace@example.org"),
                     ("Ada", "Lovelace", "ada@example.org")
                 })
        {
            var refTest = RefTest.Create(
                titleId,
                firstName,
                lastName,
                email,
                numberOfQuestions: 2,
                maxTimeInMinutes: 30,
                questionIds: ["q1", "q2"],
                sendInvitationAutomatically: true,
                sendResultsAutomatically: true);
            refTest.SendInvitation();
            refTest.Expire();

            var invitationJob = Job.Create(
                JobType.InvitationEmail,
                JsonSerializer.Serialize(
                    new InvitationEmailPayload(
                        refTest.Id,
                        refTest.FullName,
                        refTest.Email,
                        refTest.Token,
                        refTest.NumberOfQuestions,
                        refTest.MaxTimeInMinutes),
                    JobJsonOptions));

            context.RefTests.Add(refTest);
            context.Jobs.Add(invitationJob);
            fixtures.Add(new ExpiredRefTestFixture(
                refTest.Id,
                refTest.Token,
                refTest.CreatedAt,
                refTest.ExpiredAt!.Value,
                invitationJob.Id));
        }

        await context.SaveChangesAsync(cancellationToken);
        return [.. fixtures];
    }

    private static IJobEnqueueService FailAfterStaging(
        IJobEnqueueService innerService,
        string methodName,
        Func<object?[], bool> shouldFail)
    {
        var proxy = DispatchProxy.Create<IJobEnqueueService, JobEnqueueFailureProxy>();
        ((JobEnqueueFailureProxy)(object)proxy).Configure(innerService, methodName, shouldFail);
        return proxy;
    }

    private static IIhfRulesQuestionsService QuestionService() =>
        DispatchProxy.Create<IIhfRulesQuestionsService, QuestionServiceProxy>();

    private static IRefTestSubscriptionService SubscriptionService() =>
        DispatchProxy.Create<IRefTestSubscriptionService, NoOpSubscriptionServiceProxy>();

    private sealed record ExpiredRefTestFixture(
        Guid Id,
        string Token,
        DateTime CreatedAt,
        DateTime ExpiredAt,
        Guid InvitationJobId);

    public class JobEnqueueFailureProxy : DispatchProxy
    {
        private IJobEnqueueService? _innerService;
        private Func<object?[], bool>? _shouldFail;
        private string? _methodName;

        public void Configure(
            IJobEnqueueService innerService,
            string methodName,
            Func<object?[], bool> shouldFail)
        {
            _innerService = innerService;
            _methodName = methodName;
            _shouldFail = shouldFail;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
                throw new InvalidOperationException("Expected an enqueue-service invocation.");

            var innerService = _innerService
                ?? throw new InvalidOperationException("The enqueue-service proxy is not configured.");
            var arguments = args ?? [];
            var task = targetMethod.Invoke(innerService, arguments) as Task
                ?? throw new InvalidOperationException("The enqueue operation did not return a task.");

            return targetMethod.Name == _methodName && _shouldFail?.Invoke(arguments) == true
                ? FailAfterStagingAsync(task)
                : task;
        }

        private static async Task FailAfterStagingAsync(Task enqueueTask)
        {
            await enqueueTask;
            throw new InvalidOperationException("Injected enqueue failure after staging.");
        }
    }

    public class QuestionServiceProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IIhfRulesQuestionsService.GetRandomQuestionIdsAsync)
                ? Task.FromResult<List<string>>(["q1", "q2"])
                : throw new NotSupportedException($"Unexpected {targetMethod?.Name} call.");
    }

    public class NoOpSubscriptionServiceProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType == typeof(Task)
                ? Task.CompletedTask
                : throw new NotSupportedException($"Unexpected {targetMethod?.Name} call.");
    }
}
