using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql;

/// <summary>
/// GraphQL type extension for RefTest to add resolved fields
/// </summary>
public class RefTestType : ObjectType<RefTestDto>
{
    protected override void Configure(IObjectTypeDescriptor<RefTestDto> descriptor)
    {
        descriptor.Name(nameof(RefTest));
        descriptor.Description("RefTest");

        descriptor.BindFieldsExplicitly();

        descriptor.ImplementsNode()
            .IdField(x => x.Id)
            .ResolveNode((ctx, id) => ctx.DataLoader<RefTestByIdDataLoader>().LoadAsync(id, ctx.RequestAborted))
            .Description("The RefTest id");

        descriptor.Field(x => x.Title)
            .Description("Title of the RefTest");

        descriptor.Field(x => x.FullName).Name("name").Description("Name of the user who started the RefTest (e.g., )");
        descriptor.Field(x => x.Email).Description("Email of the user who started the RefTest");
        descriptor.Field(x => x.InvitationSent).Description("Indication of invitation was sent").Authorize();
        descriptor.Field(x => x.MaxTimeInMinutes).Description("Maximum time in minutes for the RefTest");
        descriptor.Field(x => x.NumberOfQuestions).Description("Number of questions in the RefTest");
        descriptor.Field(x => x.CreatedAt).Description("Creation date and time of the RefTest").Authorize();
        descriptor.Field(x => x.StartedAt).Description("Start date and time of the RefTest");
        descriptor.Field(x => x.CompletedAt).Description("Completion date and time of the RefTest").Authorize();
        descriptor.Field(x => x.Percentage).Description("Percentage of correct answers");
        descriptor.Field(x => x.QuestionScore).Description("Score based on fully correct questions");
        descriptor.Field(x => x.AnswerScore).Description("Score based on individual answers");
        descriptor.Field(x => x.QuestionTotal).Description("Total possible question score");
        descriptor.Field(x => x.AnswerTotal).Description("Total possible answer score");
        descriptor.Field(x => x.WrongQuestionIds).Description("List of question IDs that were answered incorrectly");
        descriptor.Field(x => x.WrongAnswerIds).Description("List of answer IDs that were answered incorrectly");
        descriptor.Field(x => x.ResultsSent).Description("Indication of results were sent").Authorize();
        descriptor.Field(x => x.Status)
            .Description(
                "Status of the RefTest (e.g., InProgress, Completed, Expired). Expired tests are automatically processed by a background service.")
            .Authorize();
        descriptor.Field(x => x.CurrentQuestionIndex).Description("Index of the current question");
        descriptor.Field(x => x.SelectedAnswerIds).Description("List of selected answer IDs");
        descriptor.Field("questions")
            .Description("Questions for this RefTest")
            .Argument("includeNumber", x => x.Type<BooleanType>().DefaultValue(false))
            .Argument("includeIsCorrect", x => x.Type<BooleanType>().DefaultValue(false))
            .Argument("randomAnswerOrder", x => x.Type<BooleanType>().DefaultValue(true))
            .Resolve((ctx, ct) =>
                GetQuestions(ctx.Parent<RefTestDto>().QuestionIds, ctx.Service<IIhfRulesQuestionsService>(),
                    ctx.ArgumentValue<bool>("includeNumber"), ctx.ArgumentValue<bool>("includeIsCorrect"),
                    ctx.ArgumentValue<bool>("randomAnswerOrder"), ct));
    }

    /// <summary>
    /// Get questions for this RefTest
    /// </summary>
    private static Task<List<Question>> GetQuestions(
        IReadOnlyList<string> questionIds,
        IIhfRulesQuestionsService ihfRulesQuestionsService,
        bool includeNumber,
        bool includeIsCorrect,
        bool randomAnswerOrder,
        CancellationToken cancellationToken)
        => ihfRulesQuestionsService.GetQuestionsByIdAsync(questionIds, includeNumber, includeIsCorrect,
            randomAnswerOrder,
            cancellationToken);
}