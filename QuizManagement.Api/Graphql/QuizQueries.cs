using Handball.Belgium.Rules.Quiz.Domain;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
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
    /// <param name="dataLoader"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<QuizSession?> GetQuizSessionByTokenAsync(
        string token,
        QuizSessionByTokenDataLoader dataLoader,
        CancellationToken cancellationToken)
        => dataLoader.LoadAsync(token, cancellationToken);

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
}