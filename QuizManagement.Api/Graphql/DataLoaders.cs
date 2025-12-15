using Handball.Belgium.Rules.Quiz.Domain;
using Microsoft.EntityFrameworkCore;
using QuizManagement.Infrastructure;

namespace QuizManagement.Api.Graphql;

public static class DataLoaders
{
    [DataLoader]
    public static async Task<IReadOnlyDictionary<Guid, QuizSession>> GetQuizSessionById(
        IReadOnlyList<Guid> ids,
        IDbContextFactory<QuizManagementContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.QuizSessions
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    [DataLoader]
    public static async Task<IReadOnlyDictionary<Guid, QuizTitle>> GetQuizTitleById(
        IReadOnlyList<Guid> ids,
        IDbContextFactory<QuizManagementContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.QuizTitles
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }
}