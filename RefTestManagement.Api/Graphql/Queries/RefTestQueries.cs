using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Api.Graphql.Types;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using HotChocolate.Authorization;
using HotChocolate.Caching;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Queries;

/// <summary>
/// RefTest queries
/// </summary>
[QueryType]
public static class RefTestQueries
{
    /// <summary>
    /// Get a RefTest by token
    /// </summary>
    /// <param name="token"></param>
    /// <param name="context"></param>
    /// <param name="configuration"></param>
    /// <param name="jobEnqueueService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    /// <exception cref="RefTestExpiredException"></exception>
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    [Error<RefTestExpiredException>]
    public static async Task<RefTestDto?> GetRefTestByTokenAsync(
        string token,
        RefTestManagementContext context,
        [Service] RefTestExpirationConfiguration configuration,
        [Service] IJobEnqueueService jobEnqueueService,
        CancellationToken cancellationToken)
    {
        var refTest = await context.RefTests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Token == token, cancellationToken);

        if (refTest is null)
            throw new RefTestNotFoundException(token);

        if (refTest.Status == RefTestStatus.Completed)
            throw new InvalidRefTestStatusException(refTest.Status,
                [RefTestStatus.Pending, RefTestStatus.InProgress]);

        if (!refTest.IsExpired(configuration.ExpirationIfNotStarted))
            return refTest.ToDto();

        // Enqueue a specific job to handle this expired test
        await jobEnqueueService.EnqueueRefTestExpirationAsync(
            new RefTestExpirationPayload(refTest.Id, RefTestExpirationAction.MarkAsExpired),
            cancellationToken: cancellationToken);

        throw new RefTestExpiredException(token);
    }

    /// <summary>
    /// Get all RefTest titles
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    [Authorize]
    [UsePaging]
    [UseProjection]
    [UseFiltering<RefTestTitleFilterType>]
    [UseSorting<RefTestTitleSortType>]
    public static IQueryable<RefTestTitleDto> GetRefTestTitles(RefTestManagementContext context)
        => context.RefTestTitles
            .AsNoTracking()
            .Select(RefTestTitleMappings.ToDto);

    /// <summary>
    /// Get all RefTests
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    [Authorize]
    [UsePaging]
    [UseProjection]
    [UseFiltering<RefTestFilterType>]
    [UseSorting<RefTestSortType>]
    public static IQueryable<RefTestDto> GetRefTests(RefTestManagementContext context)
        => context.RefTests
            .AsNoTracking()
            .Include(x => x.Title)
            .Select(RefTestMappings.ToDto);

    /// <summary>
    /// Get a RefTest by id
    /// </summary>
    /// <param name="id"></param>
    /// <param name="dataLoader"></param>
    /// <returns></returns>
    [Authorize]
    public static Task<RefTestDto?> GetRefTest([ID<RefTest>] Guid id, RefTestByIdDataLoader dataLoader)
        => dataLoader.LoadAsync(id);

    /// <summary>
    /// Search questions by number
    /// </summary>
    /// <param name="number"></param>
    /// <param name="ihfRulesQuestionsService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Authorize]
    public static Task<List<Question>> SearchQuestionsByNumber(string? number,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService, CancellationToken cancellationToken)
        => ihfRulesQuestionsService.SearchQuestionsByNumberAsync(number, cancellationToken);

    /// <summary>
    /// Get questions by number
    /// </summary>
    /// <param name="numbers"></param>
    /// <param name="ihfRulesQuestionsService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Authorize]
    public static Task<List<Question>> GetQuestionsByNumber(List<string> numbers,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService, CancellationToken cancellationToken)
        => ihfRulesQuestionsService.GetQuestionsByNumberAsync(numbers, cancellationToken);

    /// <summary>
    /// Get enabled languages from configuration
    /// </summary>
    /// <param name="languageConfiguration"></param>
    /// <returns></returns>
    [CacheControl(MaxAge = 3600)]
    public static string[] GetEnabledLanguages([Service] LanguageConfiguration languageConfiguration)
        => languageConfiguration.EnabledLanguages;

    /// <summary>
    /// Get score configuration
    /// </summary>
    /// <param name="scoreConfiguration"></param>
    /// <returns></returns>
    [CacheControl(MaxAge = 3600)]
    public static ScoreConfiguration GetScoreConfiguration([Service] ScoreConfiguration scoreConfiguration)
        => scoreConfiguration;

    /// <summary>
    /// Get results mail scheduled delay minutes
    /// </summary>
    /// <param name="emailConfiguration"></param>
    /// <returns></returns>
    [CacheControl(MaxAge = 3600)]
    public static int GetResultsEmailDelayMinutes([Service] EmailConfiguration emailConfiguration)
        => emailConfiguration.ScheduledDelayMinutes;
}
