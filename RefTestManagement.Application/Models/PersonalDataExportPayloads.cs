namespace Handball.Belgium.RefTestManagement.Application.Models;

/// <summary>
/// Minimal outbox payload for sending a personal-data export verification email.
/// It intentionally carries no recipient address or verification key.
/// </summary>
public sealed record PersonalDataExportChallengeEmailPayload(Guid RequestId) : IJobPayload;

/// <summary>
/// Minimal payload for delivering a verified personal-data export. The address, locale, source
/// records, audit events, and generated PDF are all reloaded or built in memory by the handler.
/// </summary>
public sealed record PersonalDataExportDeliveryEmailPayload(Guid RequestId) : IJobPayload;

/// <summary>Participant-associated RefTest values selected for a personal-data export.</summary>
public sealed record PersonalDataExportRefTestData(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    int NumberOfQuestions,
    int MaxTimeInMinutes,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime? ExpiredAt,
    int? QuestionScore,
    int? AnswerScore,
    int? AnswerTotal,
    double? Percentage,
    string? Language,
    string? PrivacyNoticeVersion,
    DateTime? PrivacyNoticeAcceptedAt,
    DateTime? ScheduledAt);

/// <summary>Identifies a participant, system, or redacted actor in an exported audit event.</summary>
public enum PersonalDataExportActorKind
{
    Participant,
    VerifiedParticipant,
    System,
    Staff,
    Redacted,
    Other
}

/// <summary>A retained audit event after export-specific third-party PII scrubbing.</summary>
public sealed record PersonalDataExportAuditEventData(
    string StreamId,
    long Version,
    string Type,
    DateTime Timestamp,
    PersonalDataExportActorKind ActorKind,
    string ActorName,
    string ActorEmail,
    string? Data,
    bool IsArchived,
    DateTime? RedactedAt);

/// <summary>
/// In-memory data passed to the export PDF renderer. It is never serialized into a job payload or
/// persisted by the export delivery flow.
/// </summary>
public sealed record PersonalDataExportDocumentData(
    string RecipientEmail,
    string Locale,
    IReadOnlyList<PersonalDataExportRefTestData> RefTests,
    IReadOnlyList<PersonalDataExportAuditEventData> AuditEvents);
