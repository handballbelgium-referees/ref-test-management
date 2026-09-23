using Handball.Belgium.RefTestManagement.Api.Graphql.Queries;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Security;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Types;

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

        var viewRefTests = TaskAuthorizationPolicyProvider.AnyOf(
            Permissions.RefTests.ViewList,
            Permissions.RefTests.ViewDetail);
        var viewDetails = TaskAuthorizationPolicyProvider.AnyOf(
            Permissions.RefTests.ViewDetail,
            Permissions.RefTests.ViewDetailQuestions);

        descriptor.Field(x => x.Title)
            .Description("Title of the RefTest")
            .Authorize(viewRefTests);

        descriptor.Field(x => x.FirstName)
            .Description("First name of the user who started the RefTest")
            .Authorize(viewRefTests);
        descriptor.Field(x => x.LastName)
            .Description("Last name of the user who started the RefTest")
            .Authorize(viewRefTests);
        descriptor.Field(x => x.FullName)
            .Name("name")
            .Description("Name of the user who started the RefTest (e.g., )")
            .Authorize(viewRefTests);
        descriptor.Field(x => x.Email)
            .Description("Email of the user who started the RefTest")
            .Authorize(viewRefTests);
        descriptor.Field(x => x.InvitationSent)
            .Description("Indication of invitation was sent")
            .Authorize(viewRefTests);
        descriptor.Field(x => x.SendInvitationsAutomatically)
            .Description("Indication of whether invitations are sent automatically")
            .Authorize(viewRefTests);
        descriptor.Field(x => x.MaxTimeInMinutes)
            .Description("Maximum time in minutes for the RefTest")
            .Authorize(viewRefTests);
        descriptor.Field(x => x.NumberOfQuestions)
            .Description("Number of questions in the RefTest")
            .Authorize(viewRefTests);
        descriptor.Field(x => x.CreatedAt)
            .Description("Creation date and time of the RefTest")
            .Authorize(viewRefTests);
        descriptor.Field(x => x.StartedAt)
            .Description("Start date and time of the RefTest")
            .Authorize(viewRefTests);
        descriptor.Field(x => x.CompletedAt)
            .Description("Completion date and time of the RefTest")
            .Authorize(viewRefTests);
        descriptor.Field(x => x.Percentage)
            .Description("Percentage of correct answers")
            .Authorize(viewDetails);
        descriptor.Field(x => x.QuestionScore)
            .Description("Score based on fully correct questions")
            .Authorize(viewDetails);
        descriptor.Field(x => x.AnswerScore)
            .Description("Score based on individual answers")
            .Authorize(viewDetails);
        descriptor.Field(x => x.QuestionTotal)
            .Description("Total possible question score")
            .Authorize(viewDetails);
        descriptor.Field(x => x.AnswerTotal)
            .Description("Total possible answer score")
            .Authorize(viewDetails);
        descriptor.Field(x => x.WrongQuestionIds)
            .Description("List of question IDs that were answered incorrectly")
            .Authorize(viewDetails);
        descriptor.Field(x => x.WrongAnswerIds)
            .Description("List of answer IDs that were answered incorrectly")
            .Authorize(viewDetails);
        descriptor.Field(x => x.ResultsSent)
            .Description("Indication of results were sent")
            .Authorize(viewDetails);
        descriptor.Field(x => x.SendResultsAutomatically)
            .Description("Indication of whether results are sent automatically")
            .Authorize(viewRefTests);
        descriptor.Field(x => x.Status)
            .Description(
                "Status of the RefTest (e.g., InProgress, Completed, Expired). Expired tests are automatically processed by a background service.")
            .Authorize(viewRefTests);
        descriptor.Field(x => x.CurrentQuestionIndex)
            .Description("Index of the current question")
            .Authorize(viewDetails);
        descriptor.Field(x => x.SelectedAnswerIds)
            .Description("List of selected answer IDs")
            .Authorize(viewDetails);
        descriptor.Field("questions")
            .Description("Questions for this RefTest")
            .Authorize(viewDetails)
            .Argument("includeNumber", x => x.Type<BooleanType>().DefaultValue(false))
            .Argument("includeIsCorrect", x => x.Type<BooleanType>().DefaultValue(false))
            .Argument("randomAnswerOrder", x => x.Type<BooleanType>().DefaultValue(true))
            .Resolve((ctx, ct) =>
                GetQuestions(ctx.Parent<RefTestDto>().QuestionIds, ctx.Service<IIhfRulesQuestionsService>(),
                    ctx.ArgumentValue<bool>("includeNumber"), ctx.ArgumentValue<bool>("includeIsCorrect"),
                    ctx.ArgumentValue<bool>("randomAnswerOrder"), ct));
        descriptor.Field(x => x.Language)
            .Description("Language where the RefTest was taken")
            .Authorize(viewDetails);
        descriptor.Field(x => x.RejectionReason)
            .Description("Reason why the RefTest was rejected during approval review")
            .Authorize(viewDetails);
        descriptor.Field(x => x.ScheduledAt)
            .Description(
                "Date/time from which this RefTest can be started; invitation email fires at this time when automated invitations are enabled")
            .Authorize(viewRefTests);
        descriptor.Field(x => x.IsAnonymized)
            .Description("Indication of whether the RefTest has been anonymized (privacy erasure/consent withdrawal)")
            .Authorize(viewDetails);
        descriptor.Field(x => x.AnonymizedAt)
            .Description("Date/time when the RefTest was anonymized (privacy erasure/consent withdrawal)")
            .Authorize(viewDetails);
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