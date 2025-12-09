using System.Text.Json;
using QuizManagement.Application.Models;

namespace QuizManagement.Api.Graphql;

public class AnswerType : ObjectType<Answer>
{
    protected override void Configure(IObjectTypeDescriptor<Answer> descriptor)
    {
        descriptor.Name(nameof(Answer));
        descriptor.Description("IHF quiz answer");
        
        descriptor.BindFieldsExplicitly();
        
        descriptor.Field(x => x.Id).Description("Answer id");
        descriptor.Field(x => x.Phrase).Description("Answer phrase");
        descriptor.Field(x => x.Translations)
            .Type<JsonType>()
            .Description("Translations of the answer phrase")
            .Resolve(ctx => JsonSerializer.Serialize(ctx.Parent<Answer>().Translations));
    }
}