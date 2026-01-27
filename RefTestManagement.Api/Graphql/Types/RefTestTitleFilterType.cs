using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using HotChocolate.Data.Filters;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Types;

public class RefTestTitleFilterType : FilterInputType<RefTestTitleDto>
{
    protected override void Configure(IFilterInputTypeDescriptor<RefTestTitleDto> descriptor)
    {
        descriptor.BindFieldsExplicitly();
        descriptor.Name($"{nameof(RefTestTitle)}FilterInput");
        descriptor.Description("Filter RefTest titles based on Value");
        descriptor.Field(x => x.Value).Description("Filter on RefTest title value");
    }
}
