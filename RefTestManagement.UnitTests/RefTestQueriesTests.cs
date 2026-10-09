using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Lifecycle;
using Handball.Belgium.RefTestManagement.Api.Graphql.Queries;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using GreenDonut.Data;
using HotChocolate.Data.Sorting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class RefTestQueriesTests
{
    private static JobEnqueueService NewJobEnqueueService(RefTestManagementContext context) =>
        new(
            context,
            new RefTestInvitationTokenProtection(new EphemeralDataProtectionProvider()),
            NullLogger<JobEnqueueService>.Instance);

    private static RefTestSessionTokenService NewSessionTokenService() =>
        new(new EphemeralDataProtectionProvider(), TimeProvider.System);

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

    private static async Task<Guid> SeedTitleAsync(SqliteTestDatabase database)
    {
        await using var context = database.CreateContext();
        var title = RefTestTitle.Create("Season 2026");
        context.RefTestTitles.Add(title);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return title.Id;
    }

    [Fact]
    public async Task GetRefTests_OrdersByIdByDefault_AndUsesIdAsTieBreakerAfterAClientSort()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);
        await using (var seedContext = database.CreateContext())
        {
            seedContext.RefTests.AddRange(NewRefTest(titleId), NewRefTest(titleId), NewRefTest(titleId));
            await seedContext.SaveChangesAsync(ct);
        }

        await using var context = database.CreateContext();
        var sorting = new CapturingSortingContext();
        var query = RefTestQueries.GetRefTests(context, sorting);
        var postSort = Assert.IsType<PostSortingAction<IQueryable<RefTestDto>>>(sorting.PostAction);

        // No client order: the default order is applied.
        var defaultIds = await postSort(false, query).Select(x => x.Id).ToListAsync(ct);
        // A client order whose key ties on every row: Id must decide the order.
        var tiedIds = await postSort(true, query.OrderBy(x => x.Email)).Select(x => x.Id).ToListAsync(ct);

        var expected = await context.RefTests.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
        Assert.Equal(3, expected.Count);
        Assert.Equal(expected, defaultIds);
        Assert.Equal(expected, tiedIds);
    }

    private sealed class CapturingSortingContext : ISortingContext
    {
        public object? PostAction { get; private set; }
        public bool IsDefined => false;
        public void OnAfterSortingApplied<T>(PostSortingAction<T> action) => PostAction = action;
        public void Handled(bool isHandled) => throw new NotSupportedException();
        public IReadOnlyList<IReadOnlyList<ISortingFieldInfo>> GetFields() => throw new NotSupportedException();
        public IList<IDictionary<string, object?>> ToList() => throw new NotSupportedException();
        public SortDefinition<T>? AsSortDefinition<T>() => throw new NotSupportedException();
    }

    [Fact]
    public async Task GetRefTestByToken_ReturnsCompletedRefTestScores()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);

        var refTest = NewRefTest(titleId);
        var token = refTest.GetIssuedToken();
        refTest.AcceptPrivacyNotice("v1");
        refTest.Start("v1");
        refTest.Complete(
            questionScore: 8,
            answerScore: 15,
            answerTotal: 20,
            percentage: 75,
            selectedAnswerIds: ["a1", "a2"],
            wrongQuestionIds: ["q2"],
            wrongAnswerIds: ["a3"],
            language: "en",
            source: RefTestCompletionSource.Participant);

        await using (var seedContext = database.CreateContext())
        {
            seedContext.RefTests.Add(refTest);
            await seedContext.SaveChangesAsync(ct);
        }

        await using var context = database.CreateContext();
        var storedToken = await context.RefTests
            .Where(candidate => candidate.Id == refTest.Id)
            .Select(candidate => candidate.Token)
            .SingleAsync(ct);
        var sessionTokenService = NewSessionTokenService();

        Assert.Equal(RefTest.HashToken(token), storedToken);

        var session = await RefTestLifecycleMutations.CreateRefTestSessionAsync(
            token,
            context,
            sessionTokenService,
            ct);
        var queryResult = await RefTestQueries.GetRefTestByTokenAsync(
            session.SessionToken,
            context,
            new RefTestExpirationConfiguration(),
            sessionTokenService,
            NewJobEnqueueService(context),
            ct);

        Assert.NotNull(queryResult);
        Assert.Equal(RefTestStatus.Completed, queryResult.Status);
        Assert.Equal(8, queryResult.QuestionScore);
        Assert.Equal(2, queryResult.QuestionTotal);
        Assert.Equal(15, queryResult.AnswerScore);
        Assert.Equal(20, queryResult.AnswerTotal);
        Assert.Equal(75, queryResult.Percentage);
        Assert.True(queryResult.SendResultsAutomatically);

        await Assert.ThrowsAsync<RefTestNotFoundException>(() =>
            RefTestQueries.GetRefTestByTokenAsync(
                storedToken,
                context,
                new RefTestExpirationConfiguration(),
                sessionTokenService,
                NewJobEnqueueService(context),
                ct));
    }
}
