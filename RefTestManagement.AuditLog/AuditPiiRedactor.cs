using System.Text.Json.Nodes;

namespace Handball.Belgium.RefTestManagement.AuditLog;

/// <summary>
/// Removes personal data from audit event payloads. Shared by the two paths that need it: the
/// privacy erasure service (redacting a single participant's trail on request) and the audit log
/// cleanup service (redacting every trail once it passes the retention window).
/// </summary>
public static class AuditPiiRedactor
{
    /// <summary>Placeholder written in place of a personal-data value.</summary>
    public const string RedactedValue = "***";

    /// <summary>
    /// Property keys whose values are considered personal data in audit event JSON payloads.
    /// <para>
    /// The invitation token is deliberately absent: it is registered as an excluded property
    /// (<c>ExcludeProperty("Token")</c>) so entity-change payloads never capture it, and no
    /// domain event carries it either — <c>RefTestSoftResetEvent</c> records only a boolean and
    /// <c>RefTestTokenRegeneratedEvent</c> has no payload. If an event is ever added that
    /// carries a token value, add the key here.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> PiiKeys =
        new(StringComparer.OrdinalIgnoreCase) { "firstName", "lastName", "email" };

    /// <summary>Keys used by the old/new diff payload shape.</summary>
    private static readonly HashSet<string> DiffKeys =
        new(StringComparer.OrdinalIgnoreCase) { "old", "new", "oldValue", "newValue" };

    /// <summary>
    /// Replaces known personal-data keys (name/email) inside an audit event's JSON <c>Data</c>
    /// payload with <see cref="RedactedValue"/>, leaving the rest of the event (type, timestamp,
    /// actor, other fields) intact. Handles both the flat shape used by
    /// <c>RefTestCreatedEvent</c> (e.g. <c>{"firstName":"John",...}</c>) and the old/new diff
    /// shape used by <c>RefTestDetailsUpdatedEvent</c>
    /// (e.g. <c>{"firstName":{"old":"John","new":"Jane"},...}</c>).
    /// </summary>
    /// <returns>
    /// The redacted JSON, or the input unchanged when it is null, empty, or not a JSON object.
    /// Reference-equal to the input when nothing needed redacting.
    /// </returns>
    /// <remarks>
    /// Key matching is case-insensitive and driven by the payload's own property names rather
    /// than by looking the known keys up: domain events serialize camelCase, but the audit
    /// interceptor's property-diff path writes raw PascalCase EF property names, and
    /// <see cref="JsonObject"/> lookups are case-sensitive. Matching on what the payload
    /// actually contains keeps both shapes covered without having to enumerate every casing.
    /// </remarks>
    public static string? RedactData(string? data)
    {
        if (string.IsNullOrEmpty(data))
            return data;

        if (JsonNode.Parse(data) is not JsonObject node)
            return data;

        var changed = false;

        // Materialize the property names first: the loop assigns back into the same object.
        foreach (var key in node.Select(property => property.Key).ToArray())
        {
            if (!PiiKeys.Contains(key))
                continue;

            var value = node[key];
            if (value is null)
                continue;

            if (value is JsonObject diff)
            {
                foreach (var diffKey in diff.Select(property => property.Key).ToArray())
                {
                    if (!DiffKeys.Contains(diffKey))
                        continue;

                    diff[diffKey] = RedactedValue;
                    changed = true;
                }
            }
            else
            {
                node[key] = RedactedValue;
                changed = true;
            }
        }

        return changed ? node.ToJsonString() : data;
    }
}
