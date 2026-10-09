using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Lifecycle;
using Handball.Belgium.RefTestManagement.Api.Graphql.Queries;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;
using Handball.Belgium.RefTestManagement.Application.RefTests.Lifecycle;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Infrastructure.Persistence;
using Handball.Belgium.RefTestManagement.Infrastructure.Security;
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

        var session = await CreateSessionAsync(context, sessionTokenService, token, ct);
        var queryResult = await RefTestQueries.GetRefTestByTokenAsync(
            session.SessionToken,
            context,
            new RefTestExpirationConfiguration(),
            new RefTestRepository(context, sessionTokenService),
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
                new RefTestRepository(context, sessionTokenService),
                NewJobEnqueueService(context),
                ct));
    }

    private static async Task<ParticipantSessionDto> CreateSessionAsync(
        RefTestManagementContext context,
        RefTestSessionTokenService tokenService,
        string token,
        CancellationToken cancellationToken)
    {
        var handler = new RefTestLifecycleHandler(
            new RefTestRepository(context, tokenService),
            context,
            tokenService,
            new(),
            new(),
            null!,
            null!,
            new(),
            null!);
        return new ParticipantSessionDto(
            await handler.CreateSessionAsync(token, cancellationToken));
    }
}
