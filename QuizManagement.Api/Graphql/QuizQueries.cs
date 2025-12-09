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
    /// Get quiz session by token (includes questions via resolver)
    /// </summary>
    public static async Task<QuizSession?> GetQuizSession(
        string token,
        [Service] QuizManagementContext context,
        CancellationToken cancellationToken)
    {
        return await context.QuizSessions
            .FirstOrDefaultAsync(s => s.Token == token, cancellationToken);
    }
    
    /// <summary>
    /// Get all quiz sessions with filtering and sorting
    /// </summary>
    [Authorize]
    [UsePaging]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<QuizSession> GetQuizSessions(
        [Service] QuizManagementContext context)
    {
        return context.QuizSessions;
    }
}

