using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Queries;

public static class DataLoaders
{
    [DataLoader]
    public static async Task<IReadOnlyDictionary<Guid, RefTestDto>> GetRefTestById(
        IReadOnlyList<Guid> ids,
        IDbContextFactory<RefTestManagementContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.RefTests
            .AsNoTracking()
            .Include(x => x.Title)
            .Where(x => ids.Contains(x.Id))
            .Select(RefTestMappings.ToDto)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    [DataLoader]
    public static async Task<IReadOnlyDictionary<Guid, RefTestTitleDto>> GetRefTestTitleById(
        IReadOnlyList<Guid> ids,
        IDbContextFactory<RefTestManagementContext> contextFactory,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.RefTestTitles
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(RefTestTitleMappings.ToDto)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    /// <summary>
    /// Batches question lookups across every RefTest resolved in one request: one IHF call per
    /// distinct combination of options instead of one per RefTest. Within a request, RefTests that
    /// share a question also share its (possibly shuffled) answer order.
    /// </summary>
    [DataLoader]
    public static async Task<IReadOnlyDictionary<QuestionKey, Question>> GetQuestionByKey(
        IReadOnlyList<QuestionKey> keys,
        IIhfRulesQuestionsService questionsService,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<QuestionKey, Question>();
        foreach (var options in keys.GroupBy(key => key with { Id = string.Empty }))
        {
            var questions = await questionsService.GetQuestionsByIdAsync(
                [.. options.Select(key => key.Id).Distinct()],
                options.Key.IncludeNumber,
                options.Key.IncludeIsCorrect,
                options.Key.RandomAnswerOrder,
                cancellationToken);

            foreach (var question in questions)
                result[options.Key with { Id = question.Id }] = question;
        }

        return result;
    }
}

/// <summary>A question ID together with the options it is loaded with.</summary>
public sealed record QuestionKey(string Id, bool IncludeNumber, bool IncludeIsCorrect, bool RandomAnswerOrder);
