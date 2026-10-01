using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.AuditLog;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

/// <summary>
/// Removes unrelated third-party identity from RefTest audit events before they are exported.
/// This is purpose-scoped and does not modify the retained audit rows.
/// </summary>
public static class PersonalDataExportAuditSanitizer
{
    private const string RefTestCreatedEventType = "RefTestCreated";
    private const string RefTestDetailsUpdatedEventType = "RefTestDetailsUpdated";

    private static readonly EmailAddressAttribute EmailAddressValidator = new();
    private static readonly HashSet<string> IdentityChangeFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "firstName",
        "lastName",
        "email"
    };
    private static readonly HashSet<string> OldIdentityValueFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "old",
        "oldValue"
    };
    private static readonly HashSet<string> ThirdPartyFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "creatorName",
        "creatorEmail",
        "approverName",
        "approverEmail",
        "operatorName",
        "operatorEmail",
        "staffName",
        "staffEmail",
        "userName",
        "userEmail",
        "requestedBy",
        "rejectionReason",
        "reason",
        "comment",
        "comments",
        "note",
        "notes"
    };

    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "token",
        "invitationToken",
        "challengeToken",
        "challengeKey",
        "confirmationKey",
        "verificationKey",
        "key",
        "keyHash",
        "protectedKey",
        "protectedDeliveryKey",
        "password",
        "credential",
        "credentials",
        "secret",
        "answerKey",
        "questionAnswerKey",
        "correctAnswer",
        "correctAnswerId",
        "correctAnswerIds",
        "correctAnswers",
        "isCorrect"
    };

    private static readonly Regex EmailAddressPattern = new(
        @"(?<![A-Z0-9.!#$%&'*+/=?^_`{|}~-])[A-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Z0-9](?:[A-Z0-9-]*[A-Z0-9])?(?:\.[A-Z0-9](?:[A-Z0-9-]*[A-Z0-9])?)+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Returns a safe export projection without changing the stored event.</summary>
    public static PersonalDataExportAuditEventData Sanitize(AuditEvent auditEvent, string participantEmail)
        => Sanitize(auditEvent, participantEmail, auditEvent.Data);

    /// <summary>
    /// Returns retained events attributable to the participant after replaying RefTest ownership
    /// changes backwards from the verified recipient address. Events in unknown ownership
    /// intervals are omitted.
    /// </summary>
    public static List<PersonalDataExportAuditEventData> SanitizeHistory(
        IEnumerable<AuditEvent> auditEvents,
        string participantEmail)
    {
        var normalizedParticipantEmail = NormalizeEmail(participantEmail);
        var sanitizedEvents = new List<(long SeqId, PersonalDataExportAuditEventData Event)>();

        foreach (var streamEvents in auditEvents.GroupBy(auditEvent => auditEvent.StreamId, StringComparer.Ordinal))
        {
            var currentOwnerEmail = normalizedParticipantEmail;

            foreach (var auditEvent in streamEvents.OrderByDescending(auditEvent => auditEvent.SeqId))
            {
                if (auditEvent.RedactedAt is not null)
                {
                    // Retention redaction removes the ownership data. Only keep the row when the
                    // already-known owner for this point in the replay is the verified recipient;
                    // never infer ownership from the redacted payload itself.
                    if (IsOwner(currentOwnerEmail, normalizedParticipantEmail))
                        sanitizedEvents.Add((auditEvent.SeqId, Sanitize(auditEvent, participantEmail)));

                    // Whether or not this transition was attributable, its hidden identity fields
                    // cannot tell us who owned the RefTest before it, so earlier events must fail
                    // closed rather than inherit an assumed owner.
                    if (auditEvent.Type == RefTestDetailsUpdatedEventType)
                        currentOwnerEmail = null;

                    continue;
                }

                if (auditEvent.Type == RefTestDetailsUpdatedEventType)
                {
                    if (!TryReadUpdatedOwnerEmails(
                            auditEvent.Data,
                            out var oldOwnerEmail,
                            out var newOwnerEmail))
                    {
                        currentOwnerEmail = null;
                        continue;
                    }

                    var isSameAddressUpdate = oldOwnerEmail is not null
                                              && string.Equals(
                                                  oldOwnerEmail,
                                                  newOwnerEmail,
                                                  StringComparison.Ordinal);
                    if (IsOwner(currentOwnerEmail, normalizedParticipantEmail))
                    {
                        var data = isSameAddressUpdate
                            ? auditEvent.Data
                            : RedactOldIdentityValues(auditEvent.Data);
                        sanitizedEvents.Add((
                            auditEvent.SeqId,
                            Sanitize(auditEvent, participantEmail, data)));
                    }

                    currentOwnerEmail = oldOwnerEmail;
                    continue;
                }

                if (auditEvent.Type == RefTestCreatedEventType)
                {
                    currentOwnerEmail = TryReadCreatedOwnerEmail(auditEvent.Data);
                    if (IsOwner(currentOwnerEmail, normalizedParticipantEmail))
                        sanitizedEvents.Add((auditEvent.SeqId, Sanitize(auditEvent, participantEmail)));

                    continue;
                }

                if (IsOwner(currentOwnerEmail, normalizedParticipantEmail))
                    sanitizedEvents.Add((auditEvent.SeqId, Sanitize(auditEvent, participantEmail)));
            }
        }

        return sanitizedEvents
            .OrderBy(entry => entry.SeqId)
            .Select(entry => entry.Event)
            .ToList();
    }

    private static PersonalDataExportAuditEventData Sanitize(
        AuditEvent auditEvent,
        string participantEmail,
        string? data)
    {
        var (actorKind, actorName, actorEmail) = SanitizeActor(
            auditEvent.ActorName,
            auditEvent.ActorEmail,
            participantEmail);

        return new PersonalDataExportAuditEventData(
            auditEvent.StreamId,
            auditEvent.Version,
            auditEvent.Type,
            auditEvent.Timestamp,
            actorKind,
            actorName,
            actorEmail,
            SanitizeData(data, participantEmail),
            auditEvent.IsArchived,
            auditEvent.RedactedAt);
    }

    private static string? TryReadCreatedOwnerEmail(string? data)
    {
        var eventData = ParseObjectData(data);
        if (eventData is null
            || !TryGetStringProperty(eventData, "email", out var email)
            || !TryNormalizeEmail(email, out var normalizedEmail))
            return null;

        return normalizedEmail;
    }

    private static bool TryReadUpdatedOwnerEmails(
        string? data,
        out string? oldOwnerEmail,
        out string newOwnerEmail)
    {
        oldOwnerEmail = null;
        newOwnerEmail = string.Empty;

        var eventData = ParseObjectData(data);
        var emailChange = eventData is null ? null : GetObjectProperty(eventData, "email");
        if (emailChange is null)
            return false;

        if ((TryGetStringProperty(emailChange, "old", out var oldEmail)
             || TryGetStringProperty(emailChange, "oldValue", out oldEmail))
            && TryNormalizeEmail(oldEmail, out var normalizedOldEmail))
            oldOwnerEmail = normalizedOldEmail;

        if ((!TryGetStringProperty(emailChange, "new", out var newEmail)
             && !TryGetStringProperty(emailChange, "newValue", out newEmail))
            || !TryNormalizeEmail(newEmail, out newOwnerEmail))
            return false;

        return true;
    }

    private static JsonObject? ParseObjectData(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return null;

        try
        {
            return JsonNode.Parse(data) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonObject? GetObjectProperty(JsonObject obj, string propertyName)
    {
        foreach (var property in obj)
        {
            if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                return property.Value as JsonObject;
        }

        return null;
    }

    private static bool TryGetStringProperty(JsonObject obj, string propertyName, out string value)
    {
        foreach (var property in obj)
        {
            if (!string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (property.Value is JsonValue jsonValue
                && jsonValue.TryGetValue<string>(out var text)
                && text is not null)
            {
                value = text;
                return true;
            }

            break;
        }

        value = string.Empty;
        return false;
    }

    private static string? RedactOldIdentityValues(string? data)
    {
        var eventData = ParseObjectData(data);
        if (eventData is null)
            return null;

        foreach (var property in eventData.ToArray())
        {
            if (!IdentityChangeFields.Contains(property.Key)
                || property.Value is not JsonObject identityChange)
                continue;

            foreach (var identityValue in identityChange.ToArray())
            {
                if (OldIdentityValueFields.Contains(identityValue.Key))
                    identityChange[identityValue.Key] = AuditPiiRedactor.RedactedValue;
            }
        }

        return eventData.ToJsonString();
    }

    private static bool TryNormalizeEmail(string? email, out string normalizedEmail)
    {
        var trimmedEmail = email?.Trim();
        if (string.IsNullOrEmpty(trimmedEmail) || !EmailAddressValidator.IsValid(trimmedEmail))
        {
            normalizedEmail = string.Empty;
            return false;
        }

        normalizedEmail = NormalizeEmail(trimmedEmail);
        return true;
    }

    private static (PersonalDataExportActorKind kind, string name, string email) SanitizeActor(
        string actorName,
        string actorEmail,
        string participantEmail)
    {
        if (string.Equals(actorEmail, AuditPiiRedactor.RedactedValue, StringComparison.Ordinal)
            || string.Equals(actorName, AuditPiiRedactor.RedactedValue, StringComparison.Ordinal))
        {
            return (
                PersonalDataExportActorKind.Redacted,
                string.Equals(actorName, AuditPiiRedactor.RedactedValue, StringComparison.Ordinal)
                    ? AuditPiiRedactor.RedactedValue
                    : string.Empty,
                string.Equals(actorEmail, AuditPiiRedactor.RedactedValue, StringComparison.Ordinal)
                    ? AuditPiiRedactor.RedactedValue
                    : string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(actorEmail)
            && string.Equals(NormalizeEmail(actorEmail), NormalizeEmail(participantEmail),
                StringComparison.Ordinal))
        {
            return (PersonalDataExportActorKind.Participant, actorName, actorEmail);
        }

        if (string.IsNullOrEmpty(actorEmail)
            && string.Equals(actorName, "Verified participant", StringComparison.Ordinal))
        {
            return (PersonalDataExportActorKind.VerifiedParticipant, string.Empty, string.Empty);
        }

        if (string.IsNullOrEmpty(actorEmail)
            && string.Equals(actorName, "System", StringComparison.Ordinal))
        {
            return (PersonalDataExportActorKind.System, string.Empty, string.Empty);
        }

        return (PersonalDataExportActorKind.Staff, string.Empty, string.Empty);
    }

    private static string? SanitizeData(string? data, string participantEmail)
    {
        if (string.IsNullOrWhiteSpace(data))
            return data;

        try
        {
            var node = JsonNode.Parse(data);
            if (node is not JsonObject and not JsonArray)
                return null;

            SanitizeNode(node, participantEmail);
            return node.ToJsonString();
        }
        catch (JsonException)
        {
            // Audit payloads normally contain JSON objects. If a legacy/malformed value cannot be
            // inspected safely, omit its details rather than copying arbitrary text to an export.
            return null;
        }
    }

    private static void SanitizeNode(JsonNode? node, string participantEmail)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(property => property.Key).ToArray())
                {
                    if (ThirdPartyFields.Contains(key) || SensitiveFields.Contains(key))
                    {
                        obj.Remove(key);
                        continue;
                    }

                    if (obj[key] is JsonValue value
                        && value.TryGetValue<string>(out var text)
                        && text is not null)
                    {
                        var safeText = RemoveOtherEmails(text, participantEmail);
                        if (!string.Equals(safeText, text, StringComparison.Ordinal))
                            obj[key] = safeText;
                    }
                    else
                    {
                        SanitizeNode(obj[key], participantEmail);
                    }
                }

                break;

            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    if (array[index] is JsonValue value
                        && value.TryGetValue<string>(out var text)
                        && text is not null)
                    {
                        var safeText = RemoveOtherEmails(text, participantEmail);
                        if (!string.Equals(safeText, text, StringComparison.Ordinal))
                            array[index] = safeText;
                    }
                    else
                    {
                        SanitizeNode(array[index], participantEmail);
                    }
                }

                break;
        }
    }

    private static string RemoveOtherEmails(string value, string participantEmail)
    {
        var normalizedParticipantEmail = NormalizeEmail(participantEmail);
        return EmailAddressPattern.Replace(value, match =>
            string.Equals(
                NormalizeEmail(match.Value),
                normalizedParticipantEmail,
                StringComparison.Ordinal)
                ? match.Value
                : AuditPiiRedactor.RedactedValue);
    }

    private static bool IsOwner(string? ownerEmail, string participantEmail) =>
        ownerEmail is not null
        && string.Equals(ownerEmail, participantEmail, StringComparison.Ordinal);

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}
