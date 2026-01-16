using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Models;

namespace Handball.Belgium.RefTestManagement.Api.Graphql;

public class AnswerType : ObjectType<Answer>
{
    protected override void Configure(IObjectTypeDescriptor<Answer> descriptor)
    {
        descriptor.Name(nameof(Answer));
        descriptor.Description("IHF RefTest answer");
        
        descriptor.BindFieldsExplicitly();
        
        descriptor.Field(x => x.Id).Description("Answer id");
        descriptor.Field(x => x.Number).Description("Answer number").Authorize();
        descriptor.Field(x => x.Phrase)
            .Type<JsonType>()
            .Description("Translations of the answer phrase")
            .Resolve(ctx => JsonSerializer.Serialize(ctx.Parent<Answer>().Phrase));
        descriptor.Field(x => x.IsCorrect).Description("Answer is correct").Authorize();
    }
}