using Handball.Belgium.Rules.Quiz.Domain;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
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
    /// Start a quiz session
    /// </summary>
    /// <param name="token"></param>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="QuizSessionNotFoundException"></exception>
    /// <exception cref="QuizSessionExpiredException"></exception>
    /// <exception cref="InvalidQuizSessionStatusException"></exception>
    [Error<QuizSessionNotFoundException>]
    [Error<QuizSessionExpiredException>]
    [Error<InvalidQuizSessionStatusException>]
    public static async Task<QuizSession> StartQuizSessionAsync(
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
    /// Complete a quiz session and send results email
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="ihfRulesQuestionsService"></param>
    /// <param name="emailService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="QuizSessionNotFoundException"></exception>
    /// <exception cref="InvalidQuizSessionStatusException"></exception>
    /// <exception cref="EmailException"></exception>
    [Error<QuizSessionNotFoundException>]
    [Error<InvalidQuizSessionStatusException>]
    [Error<EmailException>]
    public static async Task<QuizSession> CompleteQuizAsync(
        CompleteQuizInput input,
        QuizManagementContext context,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService,
        [Service] IEmailService emailService,
        CancellationToken cancellationToken)
    {
        var session = await context.QuizSessions
            .FirstOrDefaultAsync(s => s.Token == input.Token, cancellationToken);

        if (session == null)
            throw new QuizSessionNotFoundException(input.Token);

        if (session.Status != QuizSessionStatus.InProgress)
            throw new InvalidQuizSessionStatusException(session.Status, QuizSessionStatus.InProgress);

        // Use existing score calculation logic
        var scoreResult = await ihfRulesQuestionsService.CalculateScoreAsync(
            input.QuestionIds,
            input.SelectedAnswerIds,
            cancellationToken
        );

        // Complete session with calculated results
        session.CompleteSession(
            scoreResult.Score,
            scoreResult.Total,
            scoreResult.Percentage,
            scoreResult.WrongQuestionsIds.ToList(),
            scoreResult.WrongAnswerIds.ToList()
        );

        await context.SaveChangesAsync(cancellationToken);

        // Send results email
        await emailService.SendQuizResultsAsync(
            session.Email,
            scoreResult.Score,
            scoreResult.Total,
            scoreResult.Percentage
        );

        return session;
    }

    /// <summary>
    /// Create multiple quiz sessions for multiple users
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="ihfRulesQuestionsService"></param>
    /// <param name="emailService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Authorize]
    public static async Task<BulkQuizSessionResult> CreateBulkQuizSessionsAsync(
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
            : await ihfRulesQuestionsService.GetQuestionIdsByNumberAsync(input.SpecificQuestionNumbers,
                cancellationToken);
        
        Guid titleId;

        switch (input.Title.Id)
        {
            case not null:
                titleId = input.Title.Id.Value;
                break;
            case null when input.Title.Name is not null:
            {
                var title = QuizTitle.Create(input.Title.Name);
                context.QuizTitles.Add(title);
                await context.SaveChangesAsync(cancellationToken);
            
                titleId = title.Id;
                break;
            }
            default:
                throw new ArgumentException("Either Title.Id or Title.Name must be provided.");
        }

        foreach (var user in input.Users)
        {
            try
            {
                // If no specific question numbers were specified, get random questions
                var questionIds = specifiedQuestionIds.Count == 0
                    ? await ihfRulesQuestionsService.GetRandomQuestionIdsAsync(input.NumberOfQuestions,
                        cancellationToken)
                    : specifiedQuestionIds;

                var session = QuizSession.Create(
                    titleId,
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

        if (!input.SendInvitations)
        {
            context.QuizSessions.AddRange(createdSessions);
            await context.SaveChangesAsync(cancellationToken);

            return result;
        }

        // Send invitation emails to all participants
        foreach (var session in createdSessions)
        {
            try
            {
                await emailService.SendQuizInvitationAsync(
                    session.FullName,
                    session.Email,
                    session.Token,
                    session.NumberOfQuestions,
                    session.MaxTimeInMinutes
                );

                result.CreatedSessions.Add(session);
                session.SendInvitation();
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
        
        context.QuizSessions.AddRange(createdSessions);
        await context.SaveChangesAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Send quiz invitation emails to quiz sessions
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="emailService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Authorize]
    public static async Task<SendInvitationsResult> SendInvitationsAsync(
        SendInvitationsInput input,
        QuizManagementContext context,
        [Service] IEmailService emailService,
        CancellationToken cancellationToken)
    {
        var sessions = await context.QuizSessions
            .Where(s => input.Ids.Contains(s.Id))
            .ToListAsync(cancellationToken);

        var result = new SendInvitationsResult
        {
            TotalRequested = input.Ids.Count,
            SentSessions = [],
            Errors = []
        };

        foreach (var id in input.Ids)
        {
            var session = sessions.FirstOrDefault(x => x.Id == id);

            try
            {
                if (session is null)
                    throw new QuizSessionNotFoundException(id.ToString());

                if (session.Status != QuizSessionStatus.Pending)
                    throw new InvalidQuizSessionStatusException(session.Status, QuizSessionStatus.Pending);

                await emailService.SendQuizInvitationAsync(
                    session.FullName,
                    session.Email,
                    session.Token,
                    session.NumberOfQuestions,
                    session.MaxTimeInMinutes
                );

                result.SentSessions.Add(session);
                result.SuccessfullySent++;
                
                session.SendInvitation();
            }
            catch (Exception e)
            {
                result.Failed++;

                // Email failed, or session was not found, or session is not pending
                // Log the error but don't fail the entire operation
                result.Errors.Add(new SendInvitationError
                {
                    QuizSessionId = id,
                    User = session is null ? null : new User(session.FirstName, session.LastName, session.Email),
                    ErrorMessage = e.Message
                });
            }
        }

        context.QuizSessions.UpdateRange(sessions);
        await context.SaveChangesAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Delete a quiz session
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="QuizSessionNotFoundException"></exception>
    [Authorize]
    public static async Task<DeleteQuizSessionsResult> DeleteQuizSessionsAsync(
        DeleteQuizSessionsInput input,
        QuizManagementContext context,
        CancellationToken cancellationToken)
    {
        var sessions = await context.QuizSessions
            .Where(s => input.Ids.Contains(s.Id))
            .ToListAsync(cancellationToken);
        
        var result = new DeleteQuizSessionsResult
        {
            TotalRequested = input.Ids.Count,
            DeletedSessions = [],
            Errors = []
        };

        foreach (var id in input.Ids)
        {
            var session = sessions.FirstOrDefault(x => x.Id == id);

            try
            {
                if (session is null)
                    throw new QuizSessionNotFoundException(id.ToString());
                
                context.QuizSessions.Remove(session);
                result.SuccessfullyDeleted++;
                result.DeletedSessions.Add(session);
            }
            catch (Exception e)
            {
                result.Failed++;
                result.Errors.Add(new DeleteQuizSessionError
                {
                    QuizSessionId = id,
                    ErrorMessage = e.Message
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return result;
    }
}