using Handball.Belgium.RefTestManagement.Api.Graphql.Queries;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class RefTestQueriesTests
{
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
    public async Task GetRefTestByToken_ReturnsCompletedRefTestScores()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var titleId = await SeedTitleAsync(database);

        var refTest = NewRefTest(titleId);
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
        var queryResult = await RefTestQueries.GetRefTestByTokenAsync(
            refTest.Token,
            context,
            new RefTestExpirationConfiguration(),
            new JobEnqueueService(context, NullLogger<JobEnqueueService>.Instance),
            ct);

        Assert.NotNull(queryResult);
        Assert.Equal(RefTestStatus.Completed, queryResult.Status);
        Assert.Equal(8, queryResult.QuestionScore);
        Assert.Equal(2, queryResult.QuestionTotal);
        Assert.Equal(15, queryResult.AnswerScore);
        Assert.Equal(20, queryResult.AnswerTotal);
        Assert.Equal(75, queryResult.Percentage);
        Assert.True(queryResult.SendResultsAutomatically);
    }
}
