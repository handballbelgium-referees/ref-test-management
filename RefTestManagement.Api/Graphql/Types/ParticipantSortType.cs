using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.Participants;
using HotChocolate.Data.Sorting;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Types;

public sealed class ParticipantSortType : SortInputType<ParticipantDto>
{
    protected override void Configure(ISortInputTypeDescriptor<ParticipantDto> descriptor)
    {
        descriptor.BindFieldsExplicitly();
        descriptor.Name($"{nameof(Participant)}SortInput");
        descriptor.Description("Sort participants by name, email, type, level, and timestamps");
        descriptor.Field(x => x.FirstName).Description("Sort on participant first name");
        descriptor.Field(x => x.LastName).Description("Sort on participant last name");
        descriptor.Field(x => x.FullName).Name("name").Description("Sort on participant full name");
        descriptor.Field(x => x.Email).Description("Sort on participant email");
        descriptor.Field(x => x.Type).Description("Sort on participant type");
        descriptor.Field(x => x.Level).Description("Sort on participant referee level");
        descriptor.Field(x => x.CreatedAt).Description("Sort on participant creation date");
        descriptor.Field(x => x.UpdatedAt).Description("Sort on participant update date");
    }
}
