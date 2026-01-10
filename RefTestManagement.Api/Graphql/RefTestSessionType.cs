using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Domain;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql;

/// <summary>
/// GraphQL type extension for RefTest to add resolved fields
/// </summary>
public class RefTestType : ObjectType<RefTest>
{
    protected override void Configure(IObjectTypeDescriptor<RefTest> descriptor)
    {
        descriptor.Name(nameof(RefTest));
        descriptor.Description("RefTest");

        descriptor.BindFieldsExplicitly();

        descriptor.ImplementsNode()
            .IdField(x => x.Id)
            .ResolveNode((ctx, id) => ctx.DataLoader<RefTestByIdDataLoader>().LoadAsync(id, ctx.RequestAborted))
            .Description("The RefTest id");

        descriptor.Field(x => x.Title)
            .Description("Title of the RefTest")
            .Resolve(ctx =>
                ctx.DataLoader<RefTestTitleByIdDataLoader>()
                    .LoadAsync(ctx.Parent<RefTest>().TitleId, ctx.RequestAborted));

        descriptor.Field("name").Description("Name of the user who started the RefTest (e.g., )").Resolve(ctx =>
            $"{ctx.Parent<RefTest>().FirstName} {ctx.Parent<RefTest>().LastName}");
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
            .Description("Status of the RefTest (e.g., InProgress, Completed, Expired)")
            .Resolve(async ctx =>
            {
                var refTest = ctx.Parent<RefTest>();
                var contextFactory = ctx.Services.GetRequiredService<IDbContextFactory<RefTestManagementContext>>();
                await using var context = await contextFactory.CreateDbContextAsync(ctx.RequestAborted);

                if (!refTest.IsExpired()) 
                    return refTest.Status;
                
                refTest.Expire();

                context.RefTests.Update(refTest);
                await context.SaveChangesAsync(ctx.RequestAborted);

                return refTest.Status;
            })
            .Authorize();
        descriptor.Field("questions")
            .Description("Questions for this RefTest")
            .Argument("includeNumber", x => x.Type<BooleanType>().DefaultValue(false))
            .Argument("includeIsCorrect", x => x.Type<BooleanType>().DefaultValue(false))
            .Argument("randomAnswerOrder", x => x.Type<BooleanType>().DefaultValue(true))
            .Resolve((ctx, ct) =>
                GetQuestions(ctx.Parent<RefTest>(), ctx.Service<IIhfRulesQuestionsService>(),
                    ctx.ArgumentValue<bool>("includeNumber"), ctx.ArgumentValue<bool>("includeIsCorrect"),
                    ctx.ArgumentValue<bool>("randomAnswerOrder"), ct));
    }

    /// <summary>
    /// Get questions for this RefTest
    /// </summary>
    private static Task<List<Question>> GetQuestions(
        [Parent] RefTest refTest,
        [Service] IIhfRulesQuestionsService ihfRulesQuestionsService,
        [Argument] bool includeNumber,
        [Argument] bool includeIsCorrect,
        [Argument] bool randomAnswerOrder,
        CancellationToken cancellationToken)
        => ihfRulesQuestionsService.GetQuestionsByIdAsync(refTest.QuestionIds, includeNumber, includeIsCorrect,
            randomAnswerOrder,
            cancellationToken);
}