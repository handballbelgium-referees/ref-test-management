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
            SanitizeData(auditEvent.Data, participantEmail),
            auditEvent.IsArchived,
            auditEvent.RedactedAt);
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

    private static string RemoveOtherEmails(string value, string participantEmail) =>
        EmailAddressPattern.Replace(value, match =>
            string.Equals(match.Value, participantEmail, StringComparison.OrdinalIgnoreCase)
                ? match.Value
                : AuditPiiRedactor.RedactedValue);

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}
