using Handball.Belgium.RefTestManagement.Api.Graphql.Queries;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.Participants;
using Handball.Belgium.RefTestManagement.Security;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Types;

public sealed class ParticipantObjectType : ObjectType<ParticipantDto>
{
    protected override void Configure(IObjectTypeDescriptor<ParticipantDto> descriptor)
    {
        descriptor.Name(nameof(Participant));
        descriptor.Description("A reusable participant record for RefTests");

        descriptor.BindFieldsExplicitly();
        descriptor.ImplementsNode()
            .IdField(x => x.Id)
            .ResolveNode((ctx, id) => ctx.DataLoader<ParticipantByIdDataLoader>().LoadAsync(id, ctx.RequestAborted))
            .Description("The participant id");

        descriptor.Field(x => x.FirstName)
            .Authorize(TaskAuthorizationPolicyProvider.AnyOf(
                Permissions.Participants.ViewList,
                Permissions.Participants.Update));
        descriptor.Field(x => x.LastName)
            .Authorize(TaskAuthorizationPolicyProvider.AnyOf(
                Permissions.Participants.ViewList,
                Permissions.Participants.Update));
        descriptor.Field(x => x.FullName)
            .Name("name")
            .Authorize(TaskAuthorizationPolicyProvider.AnyOf(
                Permissions.Participants.ViewList,
                Permissions.Participants.Update));
        descriptor.Field(x => x.Email)
            .Authorize(TaskAuthorizationPolicyProvider.AnyOf(
                Permissions.Participants.ViewList,
                Permissions.Participants.Update));
        descriptor.Field(x => x.Type)
            .Authorize(TaskAuthorizationPolicyProvider.AnyOf(
                Permissions.Participants.ViewList,
                Permissions.Participants.Update));
        descriptor.Field(x => x.Level)
            .Authorize(TaskAuthorizationPolicyProvider.AnyOf(
                Permissions.Participants.ViewList,
                Permissions.Participants.Update));
        descriptor.Field(x => x.CreatedAt)
            .Authorize(TaskAuthorizationPolicyProvider.AnyOf(
                Permissions.Participants.ViewList,
                Permissions.Participants.Update));
        descriptor.Field(x => x.UpdatedAt)
            .Authorize(TaskAuthorizationPolicyProvider.AnyOf(
                Permissions.Participants.ViewList,
                Permissions.Participants.Update));
    }
}
