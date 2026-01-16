using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql;

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
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    /// <exception cref="RefTestExpiredException"></exception>
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    [Error<RefTestExpiredException>]
    public static async Task<RefTest?> GetRefTestByTokenAsync(
        string token,
        RefTestManagementContext context,
        BackgroundServiceConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var refTest = await context.RefTests
            .FirstOrDefaultAsync(x => x.Token == token, cancellationToken);

        if (refTest is null)
            throw new RefTestNotFoundException(token);

        if (refTest.Status == RefTestStatus.Completed)
            throw new InvalidRefTestStatusException(refTest.Status,
                [RefTestStatus.Pending, RefTestStatus.InProgress]);

        if (!refTest.IsExpired(configuration.ExpirationIfNotStarted)) 
            return refTest;

        refTest.Expire();
        context.RefTests.Update(refTest);
        await context.SaveChangesAsync(cancellationToken);

        throw new RefTestExpiredException(token);
    }
    
    /// <summary>
    /// Get all RefTest titles
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    [Authorize]
    [UsePaging]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<RefTestTitle> GetRefTestTitles(RefTestManagementContext context)
    => context.RefTestTitles;

    /// <summary>
    /// Get all RefTests
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    [Authorize]
    [UsePaging]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<RefTest> GetRefTests(RefTestManagementContext context)
        => context.RefTests;
    
    /// <summary>
    /// Get a RefTest by id
    /// </summary>
    /// <param name="id"></param>
    /// <param name="dataLoader"></param>
    /// <returns></returns>
    [Authorize]
    public static Task<RefTest?> GetRefTest([ID<RefTest>]Guid id, RefTestByIdDataLoader dataLoader)
        => dataLoader.LoadAsync(id);

    [Authorize]
    public static Task<List<Question>> SearchQuestionsByNumber(string? number,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService)
        => ihfRulesQuestionsService.SearchQuestionsByNumberAsync(number);

    /// <summary>
    /// Get enabled languages from configuration
    /// </summary>
    /// <param name="languageConfiguration"></param>
    /// <returns></returns>
    public static string[] GetEnabledLanguages([Service] LanguageConfiguration languageConfiguration)
        => languageConfiguration.EnabledLanguages;
    
    /// <summary>
    /// Get score configuration
    /// </summary>
    /// <param name="scoreConfiguration"></param>
    /// <returns></returns>
    public static ScoreConfiguration GetScoreConfiguration([Service] ScoreConfiguration scoreConfiguration)
        => scoreConfiguration;

    /// <summary>
    /// Get results mail scheduled delay minutes
    /// </summary>
    /// <param name="emailConfiguration"></param>
    /// <returns></returns>
    public static int GetResultsEmailDelayMinutes([Service] EmailConfiguration emailConfiguration)
        => emailConfiguration.ScheduledDelayMinutes;
}