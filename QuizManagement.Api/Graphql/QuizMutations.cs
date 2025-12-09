using Handball.Belgium.Rules.Quiz.Domain;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuizManagement.Api.Graphql.Models;
using QuizManagement.Application.Services;
using QuizManagement.Infrastructure;
using QuizManagement.Infrastructure.Services;

namespace QuizManagement.Api.Graphql;

/// <summary>
/// Quiz mutations
/// </summary>
[MutationType]
public static class QuizMutations
{
    /// <summary>
    /// Start a quiz session using the token
    /// </summary>
    /// <returns>Quiz session with questions</returns>
    [Error<QuizSessionNotFoundException>]
    [Error<QuizSessionExpiredException>]
    public static async Task<QuizSession> StartQuizSession(
        string token,
        QuizManagementContext context,
        CancellationToken cancellationToken)
    {
        var session = await context.QuizSessions
            .FirstOrDefaultAsync(s => s.Token == token, cancellationToken);

        if (session == null)
            throw new QuizSessionNotFoundException(token);

        if (session.IsExpired())
        {
            session.ExpireSession();
            await context.SaveChangesAsync(cancellationToken);
            throw new QuizSessionExpiredException(token);
        }

        if (session.Status == QuizSessionStatus.InProgress)
            return session;

        session.StartSession();
        await context.SaveChangesAsync(cancellationToken);

        return session;
    }

    /// <summary>
    /// Complete the quiz with all answers and calculate the score
    /// </summary>
    /// <returns>Quiz session with calculated score</returns>
    // [Error<QuizSessionNotFoundException>]
    // [Error<InvalidQuizSessionStatusException>]
    // public static async Task<QuizSession> CompleteQuiz(
    //     CompleteQuizInput input,
    //     QuizManagementContext context,
    //     RulesQuestionsContext rulesContext,
    //     [Service] IEmailService emailService,
    //     [Service] IOptions<ScoreConfiguration> scoreOptions,
    //     [Service] IExecutionContextAccessor executionContextAccessor,
    //     CancellationToken cancellationToken)
    // {
    //     var session = await context.QuizSessions
    //         .FirstOrDefaultAsync(s => s.Token == input.Token, cancellationToken);
    //
    //     if (session == null)
    //         throw new QuizSessionNotFoundException(input.Token);
    //
    //     if (session.Status != QuizSessionStatus.InProgress)
    //         throw new InvalidQuizSessionStatusException(session.Status, QuizSessionStatus.InProgress);
    //
    //     // Use existing score calculation logic
    //     var scoreResult = ScoreCalculationQueries.CalculateScore(
    //         input.QuestionIds,
    //         input.SelectedAnswerIds,
    //         scoreOptions,
    //         executionContextAccessor,
    //         rulesContext
    //     );
    //
    //     // Complete session with calculated results
    //     session.CompleteSession(
    //         scoreResult.Score,
    //         scoreResult.Total,
    //         scoreResult.Percentage,
    //         scoreResult.WrongQuestionsIds.ToList(),
    //         scoreResult.WrongAnswerIds.ToList()
    //     );
    //
    //     await context.SaveChangesAsync(cancellationToken);
    //
    //     // Send results email
    //     await emailService.SendQuizResultsAsync(
    //         session.Email,
    //         scoreResult.Score,
    //         scoreResult.Total,
    //         scoreResult.Percentage * 100
    //     );
    //
    //     return session;
    // }
    
     /// <summary>
    /// Create quiz sessions for multiple participants at once (Admin only)
    /// </summary>
    /// <returns>Bulk creation result</returns>
    // [Authorize]
    public static async Task<BulkQuizSessionResult> CreateBulkQuizSessions(
        CreateBulkQuizSessionsInput input,
        QuizManagementContext context,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService,
        [Service] IEmailService emailService,
        CancellationToken cancellationToken)
    {
        var result = new BulkQuizSessionResult
        {
            TotalRequested = input.Users.Count
        };

        var createdSessions = new List<QuizSession>();
        
        // Get questionIds from question numbers if specified
        var specifiedQuestionIds = input.SpecificQuestionNumbers is null
            ? []
            : await ihfRulesQuestionsService.GetQuestionIdsByNumberAsync(input.SpecificQuestionNumbers, cancellationToken);
        
        foreach (var user in input.Users)
        {
            try
            {
                // If no specific question numbers were specified, get random questions
                var questionIds = specifiedQuestionIds.Count == 0
                    ? await ihfRulesQuestionsService.GetRandomQuestionIdsAsync(input.NumberOfQuestions, cancellationToken)
                    : specifiedQuestionIds;
                
                var session = QuizSession.Create(
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    input.NumberOfQuestions,
                    input.MaxTimeInMinutes,
                    questionIds
                );

                createdSessions.Add(session);
                result.SuccessfullyCreated++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add(new BulkCreationError
                {
                    User = user,
                    ErrorMessage = ex.Message
                });
            }
        }

        // Save all valid sessions to a database
        if (createdSessions.Count == 0)
            return result;

        context.QuizSessions.AddRange(createdSessions);
        await context.SaveChangesAsync(cancellationToken);

        // Send invitation emails to all participants
        foreach (var session in createdSessions)
        {
            try
            {
                await emailService.SendQuizInvitationAsync(
                    session.Email,
                    session.Token,
                    session.NumberOfQuestions,
                    session.MaxTimeInMinutes
                );

                result.CreatedSessions.Add(session);
            }
            catch (Exception ex)
            {
                // Email failed, but the session was created
                // Log the error but don't fail the entire operation
                result.Errors.Add(new BulkCreationError
                {
                    User = new User(session.FirstName, session.LastName, session.Email),
                    ErrorMessage = $"Session created but email failed: {ex.Message}"
                });
            }
        }

        return result;
    }
}