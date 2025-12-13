using Handball.Belgium.Rules.Quiz.Domain;
using Microsoft.EntityFrameworkCore;
using QuizManagement.Application.Models;
using QuizManagement.Application.Services;
using QuizManagement.Infrastructure;

namespace QuizManagement.Api.Graphql;

/// <summary>
/// GraphQL type extension for QuizSession to add resolved fields
/// </summary>
public class QuizSessionTypeExtension : ObjectType<QuizSession>
{
    protected override void Configure(IObjectTypeDescriptor<QuizSession> descriptor)
    {
        descriptor.Name(nameof(QuizSession));
        descriptor.Description("IHF quiz session");

        descriptor.BindFieldsExplicitly();

        descriptor.ImplementsNode()
            .IdField(x => x.Id)
            .ResolveNode((ctx, id) => ctx.DataLoader<QuizSessionByIdDataLoader>().LoadAsync(id, ctx.RequestAborted))
            .Description("The quiz session id");

        descriptor.Field(x => x.Title)
            .Description("Title of the quiz")
            .Resolve(ctx =>
                ctx.DataLoader<QuizTitleByIdDataLoader>()
                    .LoadAsync(ctx.Parent<QuizSession>().TitleId, ctx.RequestAborted));

        descriptor.Field("name").Description("Name of the user who started the quiz (e.g., )").Resolve(ctx =>
            $"{ctx.Parent<QuizSession>().FirstName} {ctx.Parent<QuizSession>().LastName}");
        descriptor.Field(x => x.Email).Description("Email of the user who started the quiz");
        descriptor.Field(x => x.InvitationSent).Description("Indication of invitation was sent").Authorize();
        descriptor.Field(x => x.MaxTimeInMinutes).Description("Maximum time in minutes for the quiz");
        descriptor.Field(x => x.NumberOfQuestions).Description("Number of questions in the quiz");
        descriptor.Field(x => x.CreatedAt).Description("Creation date and time of the quiz session").Authorize();
        descriptor.Field(x => x.StartedAt).Description("Start date and time of the quiz session").Authorize();
        descriptor.Field(x => x.CompletedAt).Description("Completion date and time of the quiz session").Authorize();
        descriptor.Field(x => x.Percentage).Description("Percentage of correct answers");
        descriptor.Field(x => x.Score).Description("Score of the quiz session");
        descriptor.Field(x => x.WrongQuestionIds).Description("List of question IDs that were answered incorrectly");
        descriptor.Field(x => x.WrongAnswerIds).Description("List of answer IDs that were answered incorrectly");
        descriptor.Field(x => x.ResultsSent).Description("Indication of results were sent").Authorize();
        descriptor.Field(x => x.Status)
            .Description("Status of the quiz session (e.g., InProgress, Completed, Expired)")
            .Resolve(async ctx =>
            {
                var session = ctx.Parent<QuizSession>();
                var contextFactory = ctx.Services.GetRequiredService<IDbContextFactory<QuizManagementContext>>();
                await using var context = await contextFactory.CreateDbContextAsync(ctx.RequestAborted);

                if (session.IsExpired())
                    session.ExpireSession();
                await context.SaveChangesAsync(ctx.RequestAborted);

                return session.Status;
            })
            .Authorize();
        descriptor.Field("questions")
            .Description("Questions for this quiz session")
            .Argument("includeNumber", x => x.Type<BooleanType>().DefaultValue(false))
            .Argument("includeIsCorrect", x => x.Type<BooleanType>().DefaultValue(false))
            .Resolve((ctx, ct) =>
                GetQuestions(ctx.Parent<QuizSession>(), ctx.Service<IIhfRulesQuestionsService>(),
                    ctx.ArgumentValue<bool>("includeNumber"), ctx.ArgumentValue<bool>("includeIsCorrect"), ct));
    }

    /// <summary>
    /// Get questions for this quiz session
    /// </summary>
    private static Task<List<Question>> GetQuestions(
        [Parent] QuizSession session,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService,
        [Argument] bool includeNumber,
        [Argument] bool includeIsCorrect,
        CancellationToken cancellationToken)
        => ihfRulesQuestionsService.GetQuestionsByIdAsync(session.QuestionIds, includeNumber, includeIsCorrect,
            cancellationToken);
}