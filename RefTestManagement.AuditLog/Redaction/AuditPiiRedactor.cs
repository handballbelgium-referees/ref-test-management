using System.Text.Json.Nodes;
using Handball.Belgium.RefTestManagement.Domain.RefTests.Events;

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
    /// Invitation credentials are deliberately absent: the stored hash and protected retry copy
    /// are registered as excluded properties, and no domain event carries either value —
    /// <c>RefTestSoftResetEvent</c> records only a boolean and <c>RefTestTokenRegeneratedEvent</c>
    /// has no payload. If an event is ever added that carries a token value, add the key here.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> PiiKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "firstName",
            "lastName",
            "email",
            "emailAddress",
            "key",
            "keyHash",
            "challengeKey",
            "confirmationKey",
            "verificationKey",
            "protectedKey",
            "protectedDeliveryKey"
        };

    /// <summary>Keys used by the old/new diff payload shape.</summary>
    private static readonly HashSet<string> DiffKeys =
        new(StringComparer.OrdinalIgnoreCase) { "old", "new", "oldValue", "newValue" };

    /// <summary>
    /// Replaces known personal-data keys (name/email) inside an audit event's JSON <c>Data</c>
    /// payload with <see cref="RedactedValue"/>, leaving the rest of the event (type, timestamp,
    /// actor, other fields) intact. Rejection-event free text is also redacted when the event type
    /// is supplied. Handles both the flat shape used by
    /// <c>RefTestCreatedEvent</c> (e.g. <c>{"firstName":"John",...}</c>) and the old/new diff
    /// shape used by <c>RefTestDetailsUpdatedEvent</c>
    /// (e.g. <c>{"firstName":{"old":"John","new":"Jane"},...}</c>).
    /// </summary>
    /// <param name="data">The serialized audit-event payload.</param>
    /// <param name="eventType">
    /// The event type, when known. Rejection free text is redacted only for rejection events.
    /// </param>
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
    public static string? RedactData(string? data, string? eventType = null)
    {
        if (string.IsNullOrEmpty(data))
            return data;

        if (JsonNode.Parse(data) is not JsonObject node)
            return data;

        var changed = RedactObject(node);
        if (string.Equals(eventType, RefTestRejectedEvent.EventType, StringComparison.Ordinal))
            changed |= RedactRejectionReason(node);

        return changed ? node.ToJsonString() : data;
    }

    private static bool RedactRejectionReason(JsonObject node)
    {
        // RefTestRejectedEvent stores this user-supplied text as a top-level "reason" property.
        // Keep the event and its other accountability data, but never keep the free text after
        // the event has been redacted.
        foreach (var key in node.Select(property => property.Key).ToArray())
        {
            if (!string.Equals(key, "reason", StringComparison.OrdinalIgnoreCase) || node[key] is null)
                continue;

            if (node[key] is JsonValue value
                && value.TryGetValue<string>(out var reason)
                && string.Equals(reason, RedactedValue, StringComparison.Ordinal))
            {
                return false;
            }

            node[key] = RedactedValue;
            return true;
        }

        return false;
    }

    private static bool RedactObject(JsonObject node)
    {
        var changed = false;

        // Materialize the property names first: the loop assigns back into the same object.
        foreach (var key in node.Select(property => property.Key).ToArray())
        {
            var value = node[key];
            if (value is null)
                continue;

            if (PiiKeys.Contains(key))
            {
                if (value is JsonObject diff && diff.Any(property => DiffKeys.Contains(property.Key)))
                {
                    foreach (var diffKey in diff.Select(property => property.Key).ToArray())
                    {
                        if (DiffKeys.Contains(diffKey))
                        {
                            diff[diffKey] = RedactedValue;
                            changed = true;
                        }
                    }

                    changed |= RedactObject(diff);
                }
                else
                {
                    node[key] = RedactedValue;
                    changed = true;
                }

                continue;
            }

            if (value is JsonObject childObject)
                changed |= RedactObject(childObject);
            else if (value is JsonArray childArray)
                changed |= RedactArray(childArray);
        }

        return changed;
    }

    private static bool RedactArray(JsonArray node)
    {
        var changed = false;
        for (var index = 0; index < node.Count; index++)
        {
            switch (node[index])
            {
                case JsonObject childObject:
                    changed |= RedactObject(childObject);
                    break;
                case JsonArray childArray:
                    changed |= RedactArray(childArray);
                    break;
            }
        }

        return changed;
    }
}
