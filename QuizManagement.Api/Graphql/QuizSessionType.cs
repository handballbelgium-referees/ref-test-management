using Handball.Belgium.Rules.Quiz.Domain;
using QuizManagement.Application;
using QuizManagement.Application.Models;
using QuizManagement.Application.Services;

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
            .ResolveNode((ctx, id) => ctx.DataLoader<QuizSessionByIdDataLoader>().LoadAsync(id, ctx.RequestAborted)!)
            .Description("The quiz session id");

        descriptor.Field(x => x.Email).Description("Email of the user who started the quiz");
        descriptor.Field(x => x.MaxTimeInMinutes).Description("Maximum time in minutes for the quiz");
        descriptor.Field(x => x.NumberOfQuestions).Description("Number of questions in the quiz");
        descriptor.Field(x => x.CreatedAt).Description("Creation date and time of the quiz session");
        descriptor.Field(x => x.StartedAt).Description("Start date and time of the quiz session");
        descriptor.Field(x => x.CompletedAt).Description("Completion date and time of the quiz session");
        descriptor.Field(x => x.Percentage).Description("Percentage of correct answers");
        descriptor.Field(x => x.Score).Description("Score of the quiz session");
        descriptor.Field(x => x.Status)
            .Description("Status of the quiz session (e.g., InProgress, Completed, Expired)");
        descriptor.Field("questions")
            .Description("Questions for this quiz session")
            .Resolve((ctx, ct) => GetQuestions(ctx.Parent<QuizSession>(), ctx.Service<IIhfRulesQuestionsService>(), ct));
    }

    /// <summary>
    /// Get questions for this quiz session
    /// </summary>
    private static async Task<List<Question>> GetQuestions(
        [Parent] QuizSession session,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService,
        CancellationToken cancellationToken)
    {
        if (session.IsExpired())
            throw new Exception("Quiz session has expired");

        return await ihfRulesQuestionsService.GetQuestionsByIdAsync(session.QuestionIds, cancellationToken);
    }
}