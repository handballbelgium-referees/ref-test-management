using System.Reflection;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Creation;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using HotChocolate;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// An oversized createRefTests request is rejected before it touches the database or the
/// question bank.
/// </summary>
public sealed class RefTestCreationLimitTests
{
    private const int Limit = RefTestCreationMutations.MaxBatchSize;

    [Theory]
    [InlineData(Limit + 1, 10)]
    [InlineData(1, Limit + 1)]
    public async Task OversizedRequestsAreRejectedBeforeAnyWork(int users, int questions)
    {
        var input = new CreateRefTestsInput(
            new Title(null, "Season"),
            [.. Enumerable.Range(0, users).Select(i => new User("Ada", "Lovelace", $"ada{i}@example.org"))],
            NumberOfQuestions: questions,
            RandomQuestionsForEachUser: true,
            MaxTimeInMinutes: 30);

        // Every dependency throws if used: the limit must be checked first.
        var ex = await Assert.ThrowsAsync<GraphQLException>(() => RefTestCreationMutations.CreateRefTestsAsync(
            input,
            context: null!,
            Unused<IIhfRulesQuestionsService>(),
            Unused<IJobEnqueueService>(),
            Unused<IRefTestSubscriptionService>(),
            new HttpContextAccessor(),
            NullLoggerFactory.Instance,
            TestContext.Current.CancellationToken));

        Assert.Equal("REFTEST_BATCH_TOO_LARGE", Assert.Single(ex.Errors).Code);
    }

    private static T Unused<T>() where T : class => DispatchProxy.Create<T, ThrowingProxy>();

    public class ThrowingProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException($"{targetMethod?.Name} must not be called for an oversized request.");
    }
}
