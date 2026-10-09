using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Api.Graphql.Types;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Selecting <c>questions</c> on many RefTests in one request must not call the external question
/// bank once per RefTest, and each RefTest must still get its own questions in its own order.
/// </summary>
public sealed class RefTestQuestionsBatchingTests
{
    [Fact]
    public async Task QuestionsForManyRefTestsLoadInOneUpstreamCall()
    {
        var ct = TestContext.Current.CancellationToken;
        var questions = new CountingQuestionsService();
        await using var app = await StartServerAsync(questions);

        using var result = await ExecuteAsync(app.GetTestClient(),
            "query { refTests { questions(includeNumber: true) { id } } }", ct);

        var refTests = result.RootElement.GetProperty("data").GetProperty("refTests");
        Assert.Equal(["q2", "q1"], QuestionIds(refTests[0]));
        Assert.Equal(["q1", "q3"], QuestionIds(refTests[1]));
        Assert.Equal(["q3"], QuestionIds(refTests[2]));

        var call = Assert.Single(questions.Calls);
        Assert.Equal(["missing", "q1", "q2", "q3"], call.Ids.Order());
        Assert.True(call.IncludeNumber);
    }

    [Fact]
    public async Task DifferentArgumentsAreLoadedSeparately()
    {
        var ct = TestContext.Current.CancellationToken;
        var questions = new CountingQuestionsService();
        await using var app = await StartServerAsync(questions);

        using var result = await ExecuteAsync(app.GetTestClient(),
            "query { refTests { a: questions(includeIsCorrect: true) { id } b: questions { id } } }", ct);

        Assert.False(result.RootElement.TryGetProperty("errors", out _));
        Assert.Equal(2, questions.Calls.Count);
        Assert.Contains(questions.Calls, call => call.IncludeIsCorrect);
        Assert.Contains(questions.Calls, call => !call.IncludeIsCorrect);
    }

    private static string[] QuestionIds(JsonElement refTest) =>
        [.. refTest.GetProperty("questions").EnumerateArray().Select(q => q.GetProperty("id").GetString()!)];

    private static async Task<WebApplication> StartServerAsync(IIhfRulesQuestionsService questions)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTaskBasedAuthorization();
        builder.Services.AddSingleton(questions);
        builder.Services
            .AddGraphQLServer()
            .AddQueryType<Query>()
            .AddType<RefTestType>()
            .AddType<QuestionType>()
            .AddType<AnswerType>()
            .AddDefaultNodeIdSerializer(useUrlSafeBase64: true)
            .AddGlobalObjectIdentification(false)
            .AddAuthorization();

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("permissions", Permissions.RefTests.ViewDetailQuestions)], "test"));
            await next();
        });
        app.MapGraphQL();
        await app.StartAsync();
        return app;
    }

    private static async Task<JsonDocument> ExecuteAsync(HttpClient client, string query, CancellationToken ct)
    {
        using var content = new StringContent(JsonSerializer.Serialize(new { query }), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/graphql", content, ct);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    public sealed class Query
    {
        public RefTestDto[] RefTests =>
        [
            NewRefTest(["q2", "q1"]),
            NewRefTest(["q1", "q3"]),
            NewRefTest(["q3", "missing"])
        ];

        private static RefTestDto NewRefTest(List<string> questionIds) => new()
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            FullName = "Test User",
            Email = "test@example.org",
            QuestionIds = questionIds
        };
    }

    private sealed record Call(IReadOnlyList<string> Ids, bool IncludeNumber, bool IncludeIsCorrect);

    private sealed class CountingQuestionsService : IIhfRulesQuestionsService
    {
        public ConcurrentQueue<Call> Calls { get; } = new();

        public Task<List<Question>> GetQuestionsByIdAsync(IReadOnlyList<string> ids, bool includeNumber = false,
            bool includeIsCorrect = false, bool randomAnswerOrder = true, CancellationToken cancellationToken = default)
        {
            Calls.Enqueue(new Call([.. ids], includeNumber, includeIsCorrect));
            // Like the real question bank: unknown IDs are simply absent from the result.
            return Task.FromResult<List<Question>>(
                [.. ids.Where(id => id != "missing").Select(id => new Question(id, new Dictionary<string, string>(), []))]);
        }

        public Task<List<string>> GetRandomQuestionIdsAsync(int count, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<List<string>> GetQuestionIdsByNumberAsync(List<string> numbers, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<List<Question>> SearchQuestionsByNumberAsync(string? number, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<List<Question>> GetQuestionsByNumberAsync(List<string> numbers, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ScoreCalculation> CalculateScoreAsync(IReadOnlyList<string> questionIds,
            IReadOnlyList<string> selectedAnswerIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
