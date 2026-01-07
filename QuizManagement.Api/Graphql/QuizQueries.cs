using Handball.Belgium.Rules.Quiz.Domain;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuizManagement.Application.Models;
using QuizManagement.Application.Services;
using QuizManagement.Infrastructure;
using QuizManagement.Infrastructure.Services;

namespace QuizManagement.Api.Graphql;

/// <summary>
/// Quiz queries
/// </summary>
[QueryType]
public static class QuizQueries
{
    /// <summary>
    /// Get a quiz session by token
    /// </summary>
    /// <param name="token"></param>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="QuizSessionNotFoundException"></exception>
    /// <exception cref="InvalidQuizSessionStatusException"></exception>
    /// <exception cref="QuizSessionExpiredException"></exception>
    [Error<QuizSessionNotFoundException>]
    [Error<InvalidQuizSessionStatusException>]
    [Error<QuizSessionExpiredException>]
    public static async Task<QuizSession?> GetQuizSessionByTokenAsync(
        string token,
        QuizManagementContext context,
        CancellationToken cancellationToken)
    {
        var session = await context.QuizSessions
            .FirstOrDefaultAsync(x => x.Token == token, cancellationToken);

        if (session is null)
            throw new QuizSessionNotFoundException(token);

        if (session.Status == QuizSessionStatus.Completed)
            throw new InvalidQuizSessionStatusException(session.Status,
                [QuizSessionStatus.Pending, QuizSessionStatus.InProgress]);

        return session.IsExpired() ? throw new QuizSessionExpiredException(token) : session;
    }
    
    /// <summary>
    /// Get all quiz titles
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    [Authorize]
    [UsePaging]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<QuizTitle> GetQuizTitles(QuizManagementContext context)
    => context.QuizTitles;

    /// <summary>
    /// Get all quiz sessions
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    [Authorize]
    [UsePaging]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<QuizSession> GetQuizSessions(QuizManagementContext context)
        => context.QuizSessions;

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