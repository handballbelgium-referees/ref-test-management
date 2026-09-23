using Handball.Belgium.RefTestManagement.Api.Graphql.Queries;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Security;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Types;

public class RefTestTitleType : ObjectType<RefTestTitleDto>
{
    protected override void Configure(IObjectTypeDescriptor<RefTestTitleDto> descriptor)
    {
        descriptor.Name(nameof(RefTestTitle));
        descriptor.Description("The title of the RefTest");
        
        descriptor.BindFieldsExplicitly();
        descriptor.ImplementsNode()
            .IdField(x => x.Id)
            .ResolveNode((ctx, id) => ctx.DataLoader<RefTestTitleByIdDataLoader>().LoadAsync(id, ctx.RequestAborted))
            .Description("The RefTest title id");

        descriptor.Field(x => x.Value)
            .Description("RefTest title value")
            .Authorize(TaskAuthorizationPolicyProvider.AnyOf(
                Permissions.RefTests.ViewList,
                Permissions.RefTests.ViewDetail));
    }
}
