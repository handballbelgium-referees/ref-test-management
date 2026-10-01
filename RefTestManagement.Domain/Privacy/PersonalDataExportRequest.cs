using System.Security.Cryptography;
using System.Text;
using Handball.Belgium.RefTestManagement.Domain.Events;
using Handball.Belgium.RefTestManagement.Domain.Privacy.Events;

namespace Handball.Belgium.RefTestManagement.Domain.Privacy;

/// <summary>
/// A persisted, one-time mailbox-verification challenge for a personal-data export.
/// </summary>
/// <remarks>
/// The original challenge key is never stored here. <see cref="KeyHash"/> is used for
/// verification; <see cref="ProtectedDeliveryKey"/> is a short-lived, data-protected copy used
/// only while retrying delivery of the challenge email. The protected copy is cleared as soon as
/// the provider accepts the email, and all challenge state is cleared on confirmation or expiry.
/// </remarks>
public sealed class PersonalDataExportRequest : IHasDomainEvents, IHasVerifiedParticipantActor
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private PersonalDataExportRequest(
        string email,
        string locale,
        string keyHash,
        string protectedDeliveryKey,
        DateTime createdAt,
        DateTime expiresAt)
    {
        Id = Guid.NewGuid();
        Email = email;
        Locale = locale;
        KeyHash = keyHash;
        ProtectedDeliveryKey = protectedDeliveryKey;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    private PersonalDataExportRequest()
    {
        Email = string.Empty;
        Locale = "en";
        KeyHash = null;
        ProtectedDeliveryKey = null;
    }

    public Guid Id { get; private set; }
    public long Version { get; private set; } = 1;
    public string Email { get; private set; }
    public string Locale { get; private set; }
    public string? KeyHash { get; private set; }
    public string? ProtectedDeliveryKey { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ChallengeEmailSentAt { get; private set; }
    public DateTime? LastDeliveryAttemptAt { get; private set; }
    public int DeliveryAttemptCount { get; private set; }
    public DateTime? VerifiedAt { get; private set; }

    /// <inheritdoc />
    public bool IsVerifiedParticipantActor => VerifiedAt is not null;

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Creates a challenge while persisting only its hash and protected delivery copy.</summary>
    public static PersonalDataExportRequest Create(
        string email,
        string locale,
        string challengeKey,
        string protectedDeliveryKey,
        DateTime createdAt,
        DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("An email address is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(challengeKey))
            throw new ArgumentException("A challenge key is required.", nameof(challengeKey));
        if (string.IsNullOrWhiteSpace(protectedDeliveryKey))
            throw new ArgumentException("Protected delivery state is required.", nameof(protectedDeliveryKey));
        if (expiresAt <= createdAt)
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Expiry must follow creation.");

        return new PersonalDataExportRequest(
            email,
            locale,
            HashKey(challengeKey),
            protectedDeliveryKey,
            createdAt,
            expiresAt);
    }

    /// <summary>Computes the SHA-256 lookup value for a high-entropy challenge key.</summary>
    public static string HashKey(string challengeKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(challengeKey)));

    /// <summary>Whether an email job may still attempt delivery at the supplied time.</summary>
    public bool CanDeliverAt(DateTime now) =>
        VerifiedAt is null
        && ExpiresAt > now
        && KeyHash is not null
        && ProtectedDeliveryKey is not null
        && ChallengeEmailSentAt is null;

    /// <summary>
    /// Records an accepted provider delivery and immediately clears the protected raw key.
    /// Returns false if the challenge was already completed, expired, or delivered.
    /// </summary>
    public bool MarkChallengeEmailDelivered(DateTime deliveredAt)
    {
        if (!CanDeliverAt(deliveredAt))
            return false;

        DeliveryAttemptCount++;
        LastDeliveryAttemptAt = deliveredAt;
        ChallengeEmailSentAt = deliveredAt;
        ProtectedDeliveryKey = null;
        _domainEvents.Add(new PersonalDataExportChallengeEmailDeliveredEvent(DeliveryAttemptCount)
        {
            OccurredAt = deliveredAt
        });
        return true;
    }

    /// <summary>Records a retryable provider failure without discarding the protected key.</summary>
    public bool MarkChallengeEmailDeliveryFailed(DateTime attemptedAt)
    {
        if (!CanDeliverAt(attemptedAt))
            return false;

        DeliveryAttemptCount++;
        LastDeliveryAttemptAt = attemptedAt;
        _domainEvents.Add(new PersonalDataExportChallengeEmailDeliveryFailedEvent(DeliveryAttemptCount)
        {
            OccurredAt = attemptedAt
        });
        return true;
    }

    /// <summary>
    /// Claims the export delivery for one worker. A short lease prevents duplicate request-ID
    /// jobs from sending concurrently; a reclaimed job may take over after the background-job
    /// lease expires.
    /// </summary>
    public bool TryStartPersonalDataExportDelivery(
        DateTime startedAt,
        TimeSpan leaseDuration,
        bool isReclaimedJob)
    {
        if (VerifiedAt is null || string.IsNullOrEmpty(Email))
            return false;

        if (!isReclaimedJob
            && LastDeliveryAttemptAt is { } lastAttemptAt
            && lastAttemptAt > startedAt.Subtract(leaseDuration))
            return false;

        DeliveryAttemptCount++;
        LastDeliveryAttemptAt = startedAt;
        return true;
    }

    /// <summary>Records successful attachment delivery and clears request PII.</summary>
    public bool MarkPersonalDataExportDelivered(DateTime deliveredAt, int attachmentCount)
    {
        if (VerifiedAt is null || string.IsNullOrEmpty(Email))
            return false;
        if (attachmentCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(attachmentCount));

        _domainEvents.Add(new PersonalDataExportDeliveredEvent(
            Math.Max(1, DeliveryAttemptCount),
            attachmentCount)
        {
            OccurredAt = deliveredAt
        });
        ClearChallengeData(clearEmail: true);
        return true;
    }

    /// <summary>
    /// Records a failed attempt. Recipient state is retained for a retry and erased only when the
    /// failure is terminal.
    /// </summary>
    public bool MarkPersonalDataExportDeliveryFailed(
        DateTime attemptedAt,
        PersonalDataExportDeliveryFailureCode failureCode,
        bool isTerminal)
    {
        if (VerifiedAt is null || string.IsNullOrEmpty(Email))
            return false;

        _domainEvents.Add(new PersonalDataExportDeliveryFailedEvent(
            Math.Max(1, DeliveryAttemptCount),
            failureCode,
            isTerminal)
        {
            OccurredAt = attemptedAt
        });

        if (isTerminal)
            ClearChallengeData(clearEmail: true);
        else
            LastDeliveryAttemptAt = null;

        return true;
    }

    /// <summary>
    /// Atomically marks the request verified in the tracked entity, consumes the hash and clears
    /// challenge-only delivery data. A concurrency token on the entity makes only one competing
    /// confirmation write succeed.
    /// </summary>
    public bool TryConfirm(string challengeKey, DateTime confirmedAt)
    {
        if (VerifiedAt is not null || ExpiresAt <= confirmedAt || KeyHash is null || !MatchesKey(challengeKey))
            return false;

        var wasEmailAccepted = ChallengeEmailSentAt is not null;
        VerifiedAt = confirmedAt;
        KeyHash = null;
        ProtectedDeliveryKey = null;
        ChallengeEmailSentAt = null;
        LastDeliveryAttemptAt = null;
        DeliveryAttemptCount = 0;
        _domainEvents.Add(new PersonalDataExportRequestVerifiedEvent(wasEmailAccepted)
        {
            OccurredAt = confirmedAt
        });
        return true;
    }

    /// <summary>
    /// Removes all challenge and unverified-recipient data once an abandoned request expires.
    /// The request row remains as non-identifying lifecycle metadata.
    /// </summary>
    public bool ClearExpiredChallenge(DateTime now)
    {
        if (VerifiedAt is not null || ExpiresAt > now)
            return false;

        return ClearChallengeData(clearEmail: true);
    }

    /// <summary>Removes recipient and challenge data when the source participant record is erased.</summary>
    public bool ClearForPrivacyErasure() => ClearChallengeData(clearEmail: true);

    public void ClearDomainEvents() => _domainEvents.Clear();

    private bool MatchesKey(string challengeKey)
    {
        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(KeyHash!);
        }
        catch (FormatException)
        {
            return false;
        }

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(challengeKey));
        return expectedHash.Length == suppliedHash.Length
               && CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }

    private bool ClearChallengeData(bool clearEmail)
    {
        var changed = KeyHash is not null
                      || ProtectedDeliveryKey is not null
                      || ChallengeEmailSentAt is not null
                      || LastDeliveryAttemptAt is not null
                      || DeliveryAttemptCount != 0
                      || (clearEmail && Email.Length > 0)
                      || (clearEmail && Locale != "en");

        if (!changed)
            return false;

        KeyHash = null;
        ProtectedDeliveryKey = null;
        ChallengeEmailSentAt = null;
        LastDeliveryAttemptAt = null;
        DeliveryAttemptCount = 0;

        if (clearEmail)
        {
            Email = string.Empty;
            Locale = "en";
        }

        return true;
    }
}
