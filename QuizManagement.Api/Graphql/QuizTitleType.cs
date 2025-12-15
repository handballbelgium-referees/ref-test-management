using Handball.Belgium.Rules.Quiz.Domain;

namespace QuizManagement.Api.Graphql;

public class QuizTitleType : ObjectType<QuizTitle>
{
    protected override void Configure(IObjectTypeDescriptor<QuizTitle> descriptor)
    {
        descriptor.Name(nameof(QuizTitle));
        descriptor.Description("The title of the quiz session");
        
        descriptor.BindFieldsExplicitly();
        descriptor.ImplementsNode()
            .IdField(x => x.Id)
            .ResolveNode((ctx, id) => ctx.DataLoader<QuizTitleByIdDataLoader>().LoadAsync(id, ctx.RequestAborted)!)
            .Description("The quiz title id");

        descriptor.Field(x => x.Value).Description("Quiz title value");
    }
}