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
    private static readonly string[] PiiKeys = ["firstName", "lastName", "email"];

    /// <summary>Keys used by the old/new diff payload shape.</summary>
    private static readonly string[] DiffKeys = ["old", "new", "oldValue", "newValue"];

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
    public static string? RedactData(string? data)
    {
        if (string.IsNullOrEmpty(data))
            return data;

        if (JsonNode.Parse(data) is not JsonObject node)
            return data;

        var changed = false;

        foreach (var key in PiiKeys)
        {
            if (!node.TryGetPropertyValue(key, out var value) || value is null)
                continue;

            if (value is JsonObject diff)
            {
                foreach (var diffKey in DiffKeys)
                {
                    if (!diff.ContainsKey(diffKey))
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
