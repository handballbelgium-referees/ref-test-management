using Handball.Belgium.Rules.Quiz.Domain;
using HotChocolate.Authorization;
using HotChocolate.CostAnalysis.Types;
using Microsoft.EntityFrameworkCore;
using QuizManagement.Application.Models;
using QuizManagement.Application.Services;
using QuizManagement.Infrastructure;

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
    /// Get a quiz session by id
    /// </summary>
    /// <param name="id"></param>
    /// <param name="dataLoader"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Authorize]
    public static Task<QuizSession?> GetQuizSessionByIdAsync(Guid id, QuizSessionByIdDataLoader dataLoader,
        CancellationToken cancellationToken)
        => dataLoader.LoadAsync(id, cancellationToken);

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
}