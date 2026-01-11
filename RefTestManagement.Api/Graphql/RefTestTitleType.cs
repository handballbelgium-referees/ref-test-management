using Handball.Belgium.RefTestManagement.Domain;

namespace Handball.Belgium.RefTestManagement.Api.Graphql;

public class RefTestTitleType : ObjectType<RefTestTitle>
{
    protected override void Configure(IObjectTypeDescriptor<RefTestTitle> descriptor)
    {
        descriptor.Name(nameof(RefTestTitle));
        descriptor.Description("The title of the RefTest");
        
        descriptor.BindFieldsExplicitly();
        descriptor.ImplementsNode()
            .IdField(x => x.Id)
            .ResolveNode((ctx, id) => ctx.DataLoader<RefTestTitleByIdDataLoader>().LoadAsync(id, ctx.RequestAborted)!)
            .Description("The RefTest title id");

        descriptor.Field(x => x.Value).Description("RefTest title value");
    }
}