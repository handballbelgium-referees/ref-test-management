using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Api.Graphql.Subscriptions;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate;
using HotChocolate.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class RefTestSubscriptionsTests
{
    [Fact]
    public void TimeExtensionResolverMapsConsecutivePublisherEvents()
    {
        var refTestId = Guid.NewGuid();
        var firstAt = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);
        var secondAt = firstAt.AddMinutes(1);

        var first = RefTestSubscriptions.RefTestTimeExtended(
            refTestId,
            new RefTestTimeExtendedEvent(refTestId, 31, 1, firstAt));
        var second = RefTestSubscriptions.RefTestTimeExtended(
            refTestId,
            new RefTestTimeExtendedEvent(refTestId, 32, 2, secondAt));

        Assert.Equal(new RefTestTimeExtended(refTestId, 31, 1, firstAt), first);
        Assert.Equal(new RefTestTimeExtended(refTestId, 32, 2, secondAt), second);
    }

    [Fact]
    public async Task AdminResolversMapConsecutiveEventsAndRevalidateEachPermission()
    {
        var currentPermissions = PermissionSet(
            Permissions.RefTests.ViewDetail,
            Permissions.RefTests.ViewList);
        var permissionSnapshotService = new FakePermissionSnapshotService(
            currentPermissions,
            currentPermissions,
            currentPermissions,
            currentPermissions);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var authorizationProvider = CreateAuthorizationProvider();
        var authorizationService = authorizationProvider.GetRequiredService<IAuthorizationService>();
        var user = AuthenticatedUser();
        var refTestId = Guid.NewGuid();
        var firstAt = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);
        var secondAt = firstAt.AddMinutes(1);

        var detailStarted = Assert.IsType<RefTestStarted>(await RefTestSubscriptions.RefTestUpdated(
            refTestId,
            new RefTestStartedEvent(refTestId, RefTestStatus.Pending, firstAt),
            user,
            permissionSnapshotService,
            authorizationService,
            cancellationToken));
        var detailInvitation = Assert.IsType<RefTestInvitationSent>(await RefTestSubscriptions.RefTestUpdated(
            refTestId,
            new RefTestInvitationSentEvent(refTestId, secondAt),
            user,
            permissionSnapshotService,
            authorizationService,
            cancellationToken));
        var listExpired = Assert.IsType<RefTestExpired>(await RefTestSubscriptions.RefTestsUpdated(
            new RefTestExpiredEvent(refTestId, RefTestStatus.Expired, firstAt),
            user,
            permissionSnapshotService,
            authorizationService,
            cancellationToken));
        var listResultSent = Assert.IsType<RefTestResultSent>(await RefTestSubscriptions.RefTestsUpdated(
            new RefTestResultSentEvent(refTestId, secondAt),
            user,
            permissionSnapshotService,
            authorizationService,
            cancellationToken));

        Assert.Equal(firstAt, detailStarted.StartedAt);
        Assert.Equal(secondAt, detailInvitation.SentAt);
        Assert.Equal(firstAt, listExpired.ExpiredAt);
        Assert.Equal(secondAt, listResultSent.SentAt);
        Assert.Equal(4, permissionSnapshotService.Calls);
    }

    [Fact]
    public async Task AdminResolverTerminatesWhenPermissionIsRevoked()
    {
        var permissionSnapshotService = new FakePermissionSnapshotService(
            PermissionSet(Permissions.RefTests.ViewDetail),
            PermissionSet());
        using var authorizationProvider = CreateAuthorizationProvider();
        var authorizationService = authorizationProvider.GetRequiredService<IAuthorizationService>();
        var user = AuthenticatedUser();
        var refTestId = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;
        var message = new RefTestStartedEvent(
            refTestId,
            RefTestStatus.Pending,
            new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc));

        Assert.IsType<RefTestStarted>(await RefTestSubscriptions.RefTestUpdated(
            refTestId,
            message,
            user,
            permissionSnapshotService,
            authorizationService,
            cancellationToken));
        await Assert.ThrowsAsync<GraphQLException>(() => RefTestSubscriptions.RefTestUpdated(
            refTestId,
            message,
            user,
            permissionSnapshotService,
            authorizationService,
            cancellationToken));

        Assert.Equal(2, permissionSnapshotService.Calls);
    }

    [Fact]
    public async Task SessionLockEmitsBlockedAndCompletesForMalformedCredentials()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var subscription = RefTestSubscriptions.SubscribeToRefTestSessionLock(
            "invalid",
            "session",
            null!,
            null!,
            null!,
            TimeProvider.System,
            cancellationToken).GetAsyncEnumerator(cancellationToken);

        Assert.True(await subscription.MoveNextAsync());
        Assert.Equal(RefTestSessionStatus.Blocked, subscription.Current.Status);
        Assert.False(await subscription.MoveNextAsync());
    }

    [Fact]
    public async Task SessionLockRemainsActiveAfterAcquisitionAndReleasesOnTeardown()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database, cancellationToken);
        var refTest = NewRefTest(titleId);
        refTest.AcceptPrivacyNotice("v1");
        var sessionTokenService = new RefTestSessionTokenService(
            new EphemeralDataProtectionProvider(),
            TimeProvider.System);
        var token = sessionTokenService.Create(refTest);

        await using (var seedContext = database.CreateContext())
        {
            seedContext.RefTests.Add(refTest);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        await using var context = database.CreateContext();
        using var subscriptionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        var sessionService = new RefTestSessionService();
        await using var subscription = RefTestSubscriptions.SubscribeToRefTestSessionLock(
            token,
            "active-session",
            context,
            sessionService,
            sessionTokenService,
            TimeProvider.System,
            subscriptionCancellation.Token).GetAsyncEnumerator(subscriptionCancellation.Token);

        Assert.True(await subscription.MoveNextAsync());
        Assert.Equal(RefTestSessionStatus.Acquired, subscription.Current.Status);

        var nextEvent = subscription.MoveNextAsync().AsTask();
        Assert.False(nextEvent.IsCompleted);
        subscriptionCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => nextEvent);
        Assert.True(await sessionService.TryAcquireSessionAsync(
            refTest.Id.ToString("N"), "replacement-session", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SubscriptionPublishersContinueSendingEventsToTheirTopics()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var eventSender = new RecordingTopicEventSender();
        var subscriptionService = new RefTestSubscriptionService(eventSender);
        var refTestId = Guid.NewGuid();
        var firstAt = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);
        var secondAt = firstAt.AddMinutes(1);

        await subscriptionService.PublishTimeExtendedAsync(refTestId, 31, 1, firstAt, cancellationToken);
        await subscriptionService.PublishTimeExtendedAsync(refTestId, 32, 2, secondAt, cancellationToken);
        await subscriptionService.PublishRefTestStartedAsync(
            refTestId,
            RefTestStatus.Pending,
            firstAt,
            cancellationToken);
        await subscriptionService.PublishRefTestStartedAsync(
            refTestId,
            RefTestStatus.InProgress,
            secondAt,
            cancellationToken);

        var timeTopic = RefTestSubscriptionService.TimeExtendedTopic.Replace(
            "{id}",
            refTestId.ToString());
        Assert.Equal(2, eventSender.Messages.Count(message => message.Topic == timeTopic));
        Assert.Equal(2, eventSender.Messages.Count(message => message.Topic == refTestId.ToString()));
        Assert.Equal(2, eventSender.Messages.Count(message =>
            message.Topic == RefTestSubscriptionService.GlobalTopic));
        Assert.Equal(2, eventSender.Messages.Count(message =>
            message.Message is RefTestTimeExtendedEvent));
        Assert.Equal(4, eventSender.Messages.Count(message =>
            message.Message is RefTestStartedEvent));
    }

    [Fact]
    public async Task SubscriptionPublishersIncludeTransitionStateAndKeepAnswerIdsDetailOnly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var eventSender = new RecordingTopicEventSender();
        var subscriptionService = new RefTestSubscriptionService(eventSender);
        var refTestId = Guid.NewGuid();
        var titleId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);
        var approvedAt = createdAt.AddMinutes(1);
        var scheduledAt = createdAt.AddDays(1);
        var completedAt = createdAt.AddHours(1);
        var selectedAnswerIds = new[] { "question-1:answer-2", "question-2:answer-1" };

        await subscriptionService.PublishRefTestCreatedAsync(
            refTestId,
            "Ada Lovelace",
            "ada@example.org",
            titleId,
            "Season 2026",
            invitationSent: false,
            resultsSent: false,
            sendInvitationsAutomatically: true,
            sendResultsAutomatically: false,
            status: RefTestStatus.Pending,
            numberOfQuestions: 2,
            maxTimeInMinutes: 30,
            firstName: "Ada",
            lastName: "Lovelace",
            createdAt: createdAt,
            scheduledAt: scheduledAt,
            cancellationToken: cancellationToken);
        await subscriptionService.PublishRefTestApprovedAsync(
            refTestId, RefTestStatus.Rejected, RefTestStatus.Pending, approvedAt, createdAt,
            cancellationToken);
        await subscriptionService.PublishRefTestResetAsync(
            refTestId,
            RefTestStatus.Completed,
            RefTestResetType.Hard,
            RefTestStatus.Pending,
            createdAt,
            invitationSent: false,
            cancellationToken: cancellationToken);
        await subscriptionService.PublishRefTestRevivedAsync(
            refTestId, RefTestStatus.Pending, createdAt, invitationSent: false, cancellationToken);
        await subscriptionService.PublishRefTestCompletedAsync(
            refTestId,
            RefTestStatus.Completed,
            completedAt,
            questionScore: 1,
            questionTotal: 2,
            answerScore: 2,
            answerTotal: 3,
            percentage: 50,
            language: "en",
            selectedAnswerIds: selectedAnswerIds,
            cancellationToken: cancellationToken);

        var created = Assert.IsType<RefTestCreatedEvent>(eventSender.Messages.Single(message =>
            message.Topic == RefTestSubscriptionService.GlobalTopic &&
            message.Message is RefTestCreatedEvent).Message);
        Assert.Equal("Ada", created.FirstName);
        Assert.Equal("Lovelace", created.LastName);
        Assert.Equal(createdAt, created.CreatedAt);
        Assert.Equal(scheduledAt, created.ScheduledAt);

        var approved = Assert.IsType<RefTestApprovedEvent>(eventSender.Messages.Single(message =>
            message.Topic == RefTestSubscriptionService.GlobalTopic &&
            message.Message is RefTestApprovedEvent).Message);
        Assert.Equal(RefTestStatus.Rejected, approved.OldStatus);
        Assert.Equal(createdAt, approved.CreatedAt);

        var reset = Assert.IsType<RefTestResetEvent>(eventSender.Messages.Single(message =>
            message.Topic == RefTestSubscriptionService.GlobalTopic &&
            message.Message is RefTestResetEvent).Message);
        Assert.Equal(RefTestResetType.Hard, reset.ResetType);
        Assert.Equal(RefTestStatus.Pending, reset.Status);
        Assert.Equal(createdAt, reset.CreatedAt);
        Assert.False(reset.InvitationSent);

        var revived = Assert.IsType<RefTestRevivedEvent>(eventSender.Messages.Single(message =>
            message.Topic == RefTestSubscriptionService.GlobalTopic &&
            message.Message is RefTestRevivedEvent).Message);
        Assert.Equal(createdAt, revived.CreatedAt);
        Assert.False(revived.InvitationSent);

        var detailCompletion = Assert.IsType<RefTestCompletedEvent>(eventSender.Messages.Single(message =>
            message.Topic == refTestId.ToString() &&
            message.Message is RefTestCompletedEvent).Message);
        var listCompletion = Assert.IsType<RefTestCompletedEvent>(eventSender.Messages.Single(message =>
            message.Topic == RefTestSubscriptionService.GlobalTopic &&
            message.Message is RefTestCompletedEvent).Message);
        Assert.Equal(selectedAnswerIds, detailCompletion.SelectedAnswerIds);
        Assert.Null(listCompletion.SelectedAnswerIds);

        using var authorizationProvider = CreateAuthorizationProvider();
        var authorizationService = authorizationProvider.GetRequiredService<IAuthorizationService>();
        var permissionSnapshotService = new FakePermissionSnapshotService(
            PermissionSet(Permissions.RefTests.ViewDetail),
            PermissionSet(Permissions.RefTests.ViewList));
        var user = AuthenticatedUser();
        var detailEvent = Assert.IsType<RefTestCompleted>(await RefTestSubscriptions.RefTestUpdated(
            refTestId,
            detailCompletion,
            user,
            permissionSnapshotService,
            authorizationService,
            cancellationToken));
        var listEvent = Assert.IsType<RefTestCompleted>(await RefTestSubscriptions.RefTestsUpdated(
            listCompletion,
            user,
            permissionSnapshotService,
            authorizationService,
            cancellationToken));
        Assert.Equal(selectedAnswerIds, detailEvent.SelectedAnswerIds);
        Assert.Null(listEvent.SelectedAnswerIds);

        var approvedDetailEvent = Assert.IsType<RefTestApproved>(await RefTestSubscriptions.RefTestUpdated(
            refTestId,
            approved,
            user,
            new FakePermissionSnapshotService(PermissionSet(Permissions.RefTests.ViewDetail)),
            authorizationService,
            cancellationToken));
        var approvedListEvent = Assert.IsType<RefTestApproved>(await RefTestSubscriptions.RefTestsUpdated(
            approved,
            user,
            new FakePermissionSnapshotService(PermissionSet(Permissions.RefTests.ViewList)),
            authorizationService,
            cancellationToken));
        Assert.Equal(RefTestStatus.Rejected, approvedDetailEvent.OldStatus);
        Assert.Equal(RefTestStatus.Rejected, approvedListEvent.OldStatus);
    }

    private static RefTest NewRefTest(Guid titleId) =>
        RefTest.Create(
            titleId,
            "Ada",
            "Lovelace",
            "ada@example.org",
            numberOfQuestions: 10,
            maxTimeInMinutes: 30,
            questionIds: ["q1", "q2"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: true);

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

    private static ServiceProvider CreateAuthorizationProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTaskBasedAuthorization();
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal AuthenticatedUser() =>
        new(new ClaimsIdentity([new Claim("sub", "auth0|subscription-test")], "unit-test"));

    private static IReadOnlySet<string> PermissionSet(params string[] permissions) =>
        new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);

    private sealed class FakePermissionSnapshotService : IPermissionSnapshotService
    {
        private readonly Queue<IReadOnlySet<string>?> _snapshots;

        public FakePermissionSnapshotService(params IReadOnlySet<string>?[] snapshots) =>
            _snapshots = new Queue<IReadOnlySet<string>?>(snapshots);

        public int Calls { get; private set; }

        public Task<IReadOnlySet<string>?> GetCurrentPermissionsAsync(
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_snapshots.Dequeue());
        }
    }

    private sealed class RecordingTopicEventSender : ITopicEventSender
    {
        public List<(string Topic, object Message)> Messages { get; } = [];

        public ValueTask SendAsync<TMessage>(
            string topicName,
            TMessage message,
            CancellationToken cancellationToken = default)
        {
            Messages.Add((topicName, message!));
            return ValueTask.CompletedTask;
        }

        public ValueTask CompleteAsync(string topicName) => ValueTask.CompletedTask;
    }
}
