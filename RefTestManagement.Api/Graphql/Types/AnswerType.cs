using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Security;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Types;

public class AnswerType : ObjectType<Answer>
{
    protected override void Configure(IObjectTypeDescriptor<Answer> descriptor)
    {
        descriptor.Name(nameof(Answer));
        descriptor.Description("IHF RefTest answer");

        descriptor.BindFieldsExplicitly();

        descriptor.Field(x => x.Id).Description("Answer id");
        descriptor.Field(x => x.Number).Description("Answer number")
            .Authorize(TaskAuthorizationPolicyProvider.AnyOf(
                Permissions.RefTests.ViewDetailQuestions,
                Permissions.Questions.View,
                Permissions.Questions.Search));
        descriptor.Field(x => x.Phrase)
            .Type<AnyType>()
            .Description("Translations of the answer phrase")
            .Authorize(TaskAuthorizationPolicyProvider.AnyOf(
                Permissions.RefTests.ViewDetailQuestions,
                Permissions.Questions.Search,
                Permissions.Questions.View))
            .Resolve(ctx =>
                JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(ctx.Parent<Answer>().Phrase)));
        descriptor.Field(x => x.IsCorrect).Description("Answer is correct")
            .Authorize(Permissions.RefTests.ViewDetailQuestions);
    }
}