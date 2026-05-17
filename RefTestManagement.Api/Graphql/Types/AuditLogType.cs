using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Types;

public class AuditLogType : ObjectType<AuditLogDto>
{
    protected override void Configure(IObjectTypeDescriptor<AuditLogDto> descriptor)
    {
        descriptor.BindFieldsImplicitly();

        descriptor.Field(x => x.Id).Description("Unique identifier of the audit log entry.");
        descriptor.Field(x => x.EntityType).Description("The type of entity that was changed (e.g. RefTest, RefTestTitle).");
        descriptor.Field(x => x.EntityId).Description("The primary key of the entity that was changed.");
        descriptor.Field(x => x.Action).Description("The action performed: Created, Modified, or Deleted.");
        descriptor.Field(x => x.Changes).Description("JSON object describing the property changes. For Modified entries contains old/new values; for list properties contains added/removed arrays.");
        descriptor.Field(x => x.ActorName).Description("Display name of the user who triggered the change, or 'System' for background operations.");
        descriptor.Field(x => x.ActorEmail).Description("Email of the user who triggered the change, or empty for background operations.");
        descriptor.Field(x => x.Timestamp).Description("UTC timestamp of when the change occurred.");
    }
}
