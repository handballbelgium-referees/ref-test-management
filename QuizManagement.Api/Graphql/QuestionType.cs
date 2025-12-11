using System.Text.Json;
using QuizManagement.Application.Models;

namespace QuizManagement.Api.Graphql;

public class QuestionType : ObjectType<Question>
{
    protected override void Configure(IObjectTypeDescriptor<Question> descriptor)
    {
        descriptor.Name(nameof(Question));
        descriptor.Description("IHF quiz question");
        
        descriptor.BindFieldsExplicitly();
        
        descriptor.Field(x => x.Id).Description("Question id");
        descriptor.Field(x => x.Number).Description("Question number");
        descriptor.Field(x => x.Phrase).Description("Question phrase");
        descriptor.Field(x => x.Translations)
            .Type<JsonType>()
            .Description("Translations of the question phrase")
            .Resolve(ctx => JsonSerializer.Serialize(ctx.Parent<Question>().Translations));
        
        descriptor.Field(x => x.Answers).Description("Answers for this question");
    }
}