using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Api.Graphql.Types;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;
using HotChocolate.Caching;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Queries;

/// <summary>
/// RefTest queries
/// </summary>
[QueryType]
public static partial class RefTestQueries
{
    /// <summary>
    /// Gets the public privacy notice details for test participants.
    /// </summary>
    [CacheControl(MaxAge = 3600)]
    public static PrivacyNoticeDto GetPrivacyNotice([Service] PrivacyConfiguration privacyConfiguration)
        => new(
            privacyConfiguration.ControllerName,
            privacyConfiguration.ControllerAddress,
            privacyConfiguration.ContactEmail,
            privacyConfiguration.NoticeVersion,
            privacyConfiguration.NoticeEffectiveDate,
            privacyConfiguration.RetentionYears);

    /// <summary>
    /// Get a RefTest by token
    /// </summary>
    /// <param name="token">The token of the RefTest to retrieve.</param>
    /// <param name="context">The database context for accessing RefTests and related entities.</param>
    /// <param name="configuration">The configuration for RefTest expiration.</param>
    /// <param name="jobEnqueueService">Service for enqueuing and canceling job notifications.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>The RefTestDto if found and valid; otherwise, throws an exception.</returns>
    /// <exception cref="RefTestNotFoundException"></exception>
    /// <exception cref="InvalidRefTestStatusException"></exception>
    /// <exception cref="RefTestExpiredException"></exception>
    [Error<RefTestNotFoundException>]
    [Error<InvalidRefTestStatusException>]
    [Error<RefTestExpiredException>]
    public static async Task<ParticipantRefTestDto?> GetRefTestByTokenAsync(
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

        if (refTest.IsAnonymized)
            throw new InvalidRefTestStatusException("This RefTest's consent has been withdrawn");

        if (!refTest.IsExpired(configuration.ExpirationIfNotStarted))
            return refTest.ToParticipantDto();

        // Enqueue a specific job to handle this expired test
        var action = refTest.Status == RefTestStatus.InProgress
            ? RefTestExpirationAction.AutoComplete
            : RefTestExpirationAction.MarkAsExpired;

        await jobEnqueueService.EnqueueRefTestExpirationAsync(
            new RefTestExpirationPayload(refTest.Id, action),
            cancellationToken: cancellationToken);

        throw new RefTestExpiredException(token);
    }

    /// <summary>
    /// Get all RefTest titles
    /// </summary>
    /// <param name="context">The database context for accessing RefTest titles and related entities.</param>
    /// <returns>A list of RefTestTitleDto objects.</returns>
    [Authorize(Policy = Permissions.RefTests.ViewTitles)]
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
    /// <param name="context">The database context for accessing RefTests and related entities.</param>
    /// <returns>A list of RefTestDto objects.</returns>
    [Authorize(Policy = Permissions.RefTests.ViewList)]
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
    /// <param name="id">The ID of the RefTest to retrieve.</param>
    /// <param name="dataLoader">The data loader for fetching RefTest entities by ID.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>The RefTestDto if found; otherwise, throws a RefTestNotFoundException.</returns>
    [Authorize(Policy = Permissions.RefTests.ViewDetail)]
    [Error<RefTestNotFoundException>]
    public static async Task<RefTestDto> GetRefTest([ID<RefTestDto>] Guid id, RefTestByIdDataLoader dataLoader,
        CancellationToken cancellationToken)
        => await dataLoader.LoadAsync(id, cancellationToken) ?? throw new RefTestNotFoundException(id);


    /// <summary>
    /// Search questions by number
    /// </summary>
    /// <param name="number">The number of the question to search for.</param>
    /// <param name="ihfRulesQuestionsService">The service for accessing IHF rules questions.</param>
    /// <param name="cancellationToken">Token for cancellation of the operation.</param>
    /// <returns>A list of questions matching the specified number.</returns>
    [Authorize(Policy = Permissions.Questions.Search)]
    public static Task<List<Question>> SearchQuestionsByNumber(string? number,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService, CancellationToken cancellationToken)
        => ihfRulesQuestionsService.SearchQuestionsByNumberAsync(number, cancellationToken);

    /// <summary>
    /// Get questions by number
    /// </summary>
    /// <param name="numbers">The list of question numbers to retrieve.</param>
    /// <param name="ihfRulesQuestionsService">The service for accessing IHF rules questions.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Authorize(Policy = Permissions.Questions.View)]
    public static Task<List<Question>> GetQuestionsByNumber(List<string> numbers,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService, CancellationToken cancellationToken)
        => ihfRulesQuestionsService.GetQuestionsByNumberAsync(numbers, cancellationToken);

    /// <summary>
    /// Get enabled languages from configuration
    /// </summary>
    /// <param name="languageConfiguration">The language configuration containing enabled languages.</param>
    /// <returns>An array of enabled languages.</returns>
    [CacheControl(MaxAge = 3600)]
    public static string[] GetEnabledLanguages([Service] LanguageConfiguration languageConfiguration)
        => languageConfiguration.EnabledLanguages;

    /// <summary>
    /// Get score configuration
    /// </summary>
    /// <param name="scoreConfiguration">The score configuration containing score settings.</param>
    /// <returns>The score configuration.</returns>
    [CacheControl(MaxAge = 3600)]
    public static ScoreConfiguration GetScoreConfiguration([Service] ScoreConfiguration scoreConfiguration)
        => scoreConfiguration;

    /// <summary>
    /// Get results mail scheduled delay minutes
    /// </summary>
    /// <param name="emailConfiguration">The email configuration containing scheduled delay settings.</param>
    /// <returns>The scheduled delay minutes for results email.</returns>
    [CacheControl(MaxAge = 3600)]
    public static int GetResultsEmailDelayMinutes([Service] EmailConfiguration emailConfiguration)
        => emailConfiguration.ScheduledDelayMinutes;
}