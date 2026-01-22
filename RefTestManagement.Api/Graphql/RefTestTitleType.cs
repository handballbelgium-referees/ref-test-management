using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;

namespace Handball.Belgium.RefTestManagement.Api.Graphql;

public class RefTestTitleType : ObjectType<RefTestTitleDto>
{
    protected override void Configure(IObjectTypeDescriptor<RefTestTitleDto> descriptor)
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