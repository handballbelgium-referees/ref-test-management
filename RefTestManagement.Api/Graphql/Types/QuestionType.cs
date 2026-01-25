using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Models;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Types;

public class QuestionType : ObjectType<Question>
{
    protected override void Configure(IObjectTypeDescriptor<Question> descriptor)
    {
        descriptor.Name(nameof(Question));
        descriptor.Description("IHF RefTest question");
        
        descriptor.BindFieldsExplicitly();
        
        descriptor.Field(x => x.Id).Description("Question id");
        descriptor.Field(x => x.Number).Description("Question number").Authorize();
        descriptor.Field(x => x.Phrase)
            .Type<JsonType>()
            .Description("Translations of the question phrase")
            .Resolve(ctx => JsonSerializer.Serialize(ctx.Parent<Question>().Phrase));
        
        descriptor.Field(x => x.Answers).Description("Answers for this RefTest question");
    }
}
