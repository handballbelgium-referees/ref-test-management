using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.Participants;
using HotChocolate.Data.Filters;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Types;

public sealed class ParticipantFilterType : FilterInputType<ParticipantDto>
{
    protected override void Configure(IFilterInputTypeDescriptor<ParticipantDto> descriptor)
    {
        descriptor.BindFieldsExplicitly();
        descriptor.Name($"{nameof(Participant)}FilterInput");
        descriptor.Description("Filter participants by name, email, type, level, and timestamps");
        descriptor.Field(x => x.FirstName).Description("Filter on participant first name");
        descriptor.Field(x => x.LastName).Description("Filter on participant last name");
        descriptor.Field(x => x.FullName).Name("name").Description("Filter on participant full name");
        descriptor.Field(x => x.Email).Description("Filter on participant email");
        descriptor.Field(x => x.Type).Description("Filter on participant type");
        descriptor.Field(x => x.Level).Description("Filter on participant referee level");
        descriptor.Field(x => x.CreatedAt).Description("Filter on participant creation date");
        descriptor.Field(x => x.UpdatedAt).Description("Filter on participant update date");
    }
}
