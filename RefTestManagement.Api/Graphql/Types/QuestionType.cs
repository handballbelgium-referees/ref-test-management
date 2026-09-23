using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Security;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Types;

public class QuestionType : ObjectType<Question>
{
    protected override void Configure(IObjectTypeDescriptor<Question> descriptor)
    {
        descriptor.Name(nameof(Question));
        descriptor.Description("IHF RefTest question");

        descriptor.BindFieldsExplicitly();

        descriptor.Field(x => x.Id).Description("Question id");
        descriptor.Field(x => x.Number).Description("Question number")
            .Authorize(TaskAuthorizationPolicyProvider.AnyOf(
                Permissions.RefTests.ViewDetailQuestions,
                Permissions.Questions.Search,
                Permissions.Questions.View));
        descriptor.Field(x => x.Phrase)
            .Type<AnyType>()
            .Description("Translations of the question phrase")
            .Authorize(TaskAuthorizationPolicyProvider.AnyOf(
                Permissions.RefTests.ViewDetailQuestions,
                Permissions.Questions.Search,
                Permissions.Questions.View))
            .Resolve(ctx =>
                JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(ctx.Parent<Question>().Phrase)));

        descriptor.Field(x => x.Answers)
            .Description("Answers for this RefTest question")
            .Authorize(TaskAuthorizationPolicyProvider.AnyOf(
                Permissions.RefTests.ViewDetailQuestions,
                Permissions.Questions.Search,
                Permissions.Questions.View));
    }
}