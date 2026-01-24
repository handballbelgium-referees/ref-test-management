using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using HotChocolate.Data.Sorting;

namespace Handball.Belgium.RefTestManagement.Api.Graphql;

public class RefTestTitleSortType : SortInputType<RefTestTitleDto>
{
    protected override void Configure(ISortInputTypeDescriptor<RefTestTitleDto> descriptor)
    {
        descriptor.BindFieldsExplicitly();
        descriptor.Name($"{nameof(RefTestTitle)}SortInput");
        descriptor.Description("Sort RefTest titles by Value");
        descriptor.Field(x => x.Id).Description("Sort on RefTest title id");
        descriptor.Field(x => x.Value).Description("Sort on RefTest title value");
    }
}