using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Types;

public class AuditLogType : ObjectType<AuditLogDto>
{
    protected override void Configure(IObjectTypeDescriptor<AuditLogDto> descriptor)
    {
        descriptor.BindFieldsImplicitly();

        descriptor.Field(x => x.SeqId).Description("Global monotonically increasing sequence ID across all audit events.");
        descriptor.Field(x => x.Id).Description("Unique identifier of this audit event.");
        descriptor.Field(x => x.StreamId).Description("The primary key of the aggregate that raised this event (e.g. RefTest ID).");
        descriptor.Field(x => x.Version).Description("Per-stream version counter starting at 1.");
        descriptor.Field(x => x.Type).Description("The domain event type name (e.g. RefTestCreated, RefTestApproved).");
        descriptor.Field(x => x.Data).Description("JSON payload containing event-specific change data.");
        descriptor.Field(x => x.Timestamp).Description("UTC timestamp of when the event occurred.");
        descriptor.Field(x => x.ActorName).Description("Display name of the user who triggered the event, or 'System' for background operations.");
        descriptor.Field(x => x.ActorEmail).Description("Email of the user who triggered the event, or empty for background operations.");
        descriptor.Field(x => x.Headers).Description("JSON metadata headers including the entity CLR type name.");
        descriptor.Field(x => x.IsArchived).Description("Whether this event has been soft-archived by the cleanup service.");

        descriptor.Field("nodeId")
            .Type<StringType>()
            .Description("Relay-encoded node ID for navigating to the referenced RefTest entity, or null for other entity types.")
            .Resolve(ctx =>
            {
                var dto = ctx.Parent<AuditLogDto>();
                if (string.IsNullOrEmpty(dto.Headers) || !Guid.TryParse(dto.StreamId, out var guid))
                    return null;

                try
                {
                    using var doc = JsonDocument.Parse(dto.Headers);
                    if (!doc.RootElement.TryGetProperty("entityType", out var entityTypeProp)
                        || entityTypeProp.GetString() != "RefTest")
                        return null;

                    return EncodeRelayId("RefTest", guid);
                }
                catch
                {
                    return null;
                }
            });
    }

    /// <summary>
    /// Encodes a relay node ID using HotChocolate's DefaultNodeIdSerializer format:
    /// URL-safe base64 (no padding) of UTF-8 type name + ':' + Guid.ToByteArray().
    /// </summary>
    private static string EncodeRelayId(string typeName, Guid id)
    {
        var prefixBytes = System.Text.Encoding.UTF8.GetBytes(typeName + ":");
        var guidBytes = id.ToByteArray();
        var combined = new byte[prefixBytes.Length + guidBytes.Length];
        prefixBytes.CopyTo(combined, 0);
        guidBytes.CopyTo(combined, prefixBytes.Length);

        return Convert.ToBase64String(combined)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
