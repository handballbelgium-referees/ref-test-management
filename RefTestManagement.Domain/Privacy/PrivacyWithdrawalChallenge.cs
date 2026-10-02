using System.Security.Cryptography;
using Handball.Belgium.RefTestManagement.Domain.Events;
using Handball.Belgium.RefTestManagement.Domain.Privacy.Events;
using Handball.Belgium.RefTestManagement.Domain.Security;

namespace Handball.Belgium.RefTestManagement.Domain.Privacy;

/// <summary>A short-lived, one-time mailbox challenge for a bulk consent withdrawal.</summary>
/// <remarks>
/// The address and delivery copy are retained only while a challenge is pending. The challenge
/// itself is stored as a hash, and the normalized-address hash is replaced with an opaque value
/// when the challenge is consumed, expires, or is invalidated by an erasure.
/// </remarks>
public sealed class PrivacyWithdrawalChallenge : IHasDomainEvents, IHasVerifiedParticipantActor
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private PrivacyWithdrawalChallenge()
    {
        Email = string.Empty;
        NormalizedEmailHash = string.Empty;
    }

    private PrivacyWithdrawalChallenge(
        string email,
        string normalizedEmailHash,
        string challengeKey,
        string protectedDeliveryKey,
        DateTime createdAt,
        DateTime expiresAt,
        int matchingRefTestCount)
    {
        Id = Guid.NewGuid();
        Email = email;
        NormalizedEmailHash = normalizedEmailHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        IssueChallenge(challengeKey, protectedDeliveryKey, matchingRefTestCount);
    }

    public Guid Id { get; private set; }
    public long Version { get; private set; } = 1;
    public string Email { get; private set; }
    public string NormalizedEmailHash { get; private set; }
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

    /// <inheritdoc />
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Normalizes an address using provider-independent Unicode and whitespace rules.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    /// <summary>Returns the stable lookup value for a normalized address.</summary>
    public static string HashNormalizedEmail(string normalizedEmail) => TokenService.Hash(normalizedEmail);

    /// <summary>Computes the SHA-256 lookup value for a high-entropy challenge key.</summary>
    public static string HashKey(string challengeKey) => TokenService.Hash(challengeKey);

    /// <summary>Creates a new challenge and records only the count of matching participant records.</summary>
    public static PrivacyWithdrawalChallenge Create(
        string email,
        string normalizedEmailHash,
        string challengeKey,
        string protectedDeliveryKey,
        DateTime createdAt,
        DateTime expiresAt,
        int matchingRefTestCount)
    {
        ValidateChallengeValues(email, normalizedEmailHash, challengeKey, protectedDeliveryKey, createdAt, expiresAt);
        if (matchingRefTestCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(matchingRefTestCount));

        var challenge = new PrivacyWithdrawalChallenge(
            email,
            normalizedEmailHash,
            challengeKey,
            protectedDeliveryKey,
            createdAt,
            expiresAt,
            matchingRefTestCount);

        return challenge;
    }

    /// <summary>Whether another request should be suppressed while this challenge remains pending.</summary>
    public bool IsPendingAt(DateTime now) =>
        VerifiedAt is null
        && ExpiresAt > now
        && KeyHash is not null
        && !string.IsNullOrEmpty(Email);

    /// <summary>
    /// Reuses an expired row for a new request, avoiding a second active unique address key while
    /// retaining only one non-identifying lifecycle stream for this mailbox.
    /// </summary>
    public void Renew(
        string email,
        string normalizedEmailHash,
        string challengeKey,
        string protectedDeliveryKey,
        DateTime createdAt,
        DateTime expiresAt,
        int matchingRefTestCount)
    {
        if (IsPendingAt(createdAt))
            throw new InvalidOperationException("A pending privacy withdrawal challenge cannot be renewed.");
        if (VerifiedAt is not null)
            throw new InvalidOperationException("A consumed privacy withdrawal challenge cannot be renewed.");
        if (ExpiresAt > createdAt)
            throw new InvalidOperationException("Only an expired privacy withdrawal challenge can be renewed.");
        if (matchingRefTestCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(matchingRefTestCount));

        ValidateChallengeValues(email, normalizedEmailHash, challengeKey, protectedDeliveryKey, createdAt, expiresAt);

        Email = email;
        NormalizedEmailHash = normalizedEmailHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        IssueChallenge(challengeKey, protectedDeliveryKey, matchingRefTestCount);
    }

    /// <summary>Whether a challenge-email job may attempt delivery at the supplied time.</summary>
    public bool CanDeliverAt(DateTime now) =>
        VerifiedAt is null
        && ExpiresAt > now
        && KeyHash is not null
        && ProtectedDeliveryKey is not null
        && ChallengeEmailSentAt is null;

    /// <summary>Records accepted provider delivery and clears the protected raw key.</summary>
    public bool MarkChallengeEmailDelivered(DateTime deliveredAt)
    {
        if (!CanDeliverAt(deliveredAt))
            return false;

        DeliveryAttemptCount++;
        LastDeliveryAttemptAt = deliveredAt;
        ChallengeEmailSentAt = deliveredAt;
        ProtectedDeliveryKey = null;
        _domainEvents.Add(new PrivacyWithdrawalChallengeEmailDeliveredEvent(DeliveryAttemptCount)
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
        _domainEvents.Add(new PrivacyWithdrawalChallengeEmailDeliveryFailedEvent(DeliveryAttemptCount)
        {
            OccurredAt = attemptedAt
        });
        return true;
    }

    /// <summary>
    /// Atomically consumes a valid challenge. A concurrency token on this entity makes only one
    /// competing confirmation persist the associated withdrawal batch.
    /// </summary>
    public bool TryConfirm(string challengeKey, DateTime confirmedAt)
    {
        if (VerifiedAt is not null
            || ExpiresAt <= confirmedAt
            || KeyHash is null
            || !MatchesKey(challengeKey))
            return false;

        VerifiedAt = confirmedAt;
        ClearChallengeData();
        return true;
    }

    /// <summary>Clears the recipient and key material when an unverified challenge expires.</summary>
    public bool ClearExpiredChallenge(DateTime now)
    {
        if (VerifiedAt is not null || ExpiresAt > now)
            return false;

        return ClearChallengeData();
    }

    /// <summary>Invalidates a pending challenge when an associated RefTest is erased.</summary>
    public bool ClearForPrivacyErasure() => ClearChallengeData();

    /// <inheritdoc />
    public void ClearDomainEvents() => _domainEvents.Clear();

    private void IssueChallenge(string challengeKey, string protectedDeliveryKey, int matchingRefTestCount)
    {
        KeyHash = HashKey(challengeKey);
        ProtectedDeliveryKey = protectedDeliveryKey;
        ChallengeEmailSentAt = null;
        LastDeliveryAttemptAt = null;
        DeliveryAttemptCount = 0;
        VerifiedAt = null;
        _domainEvents.Add(new PrivacyWithdrawalChallengeCreatedEvent(matchingRefTestCount)
        {
            OccurredAt = CreatedAt
        });
    }

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

        var suppliedHash = TokenService.HashBytes(challengeKey);
        return expectedHash.Length == suppliedHash.Length
               && CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }

    private bool ClearChallengeData()
    {
        var changed = Email.Length > 0
                      || !string.Equals(NormalizedEmailHash, InactiveEmailHash(), StringComparison.Ordinal)
                      || KeyHash is not null
                      || ProtectedDeliveryKey is not null
                      || ChallengeEmailSentAt is not null
                      || LastDeliveryAttemptAt is not null
                      || DeliveryAttemptCount != 0;

        if (!changed)
            return false;

        Email = string.Empty;
        NormalizedEmailHash = InactiveEmailHash();
        KeyHash = null;
        ProtectedDeliveryKey = null;
        ChallengeEmailSentAt = null;
        LastDeliveryAttemptAt = null;
        DeliveryAttemptCount = 0;
        return true;
    }

    private string InactiveEmailHash() => $"inactive-{Id:N}";

    private static void ValidateChallengeValues(
        string email,
        string normalizedEmailHash,
        string challengeKey,
        string protectedDeliveryKey,
        DateTime createdAt,
        DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("An email address is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(normalizedEmailHash))
            throw new ArgumentException("A normalized address lookup value is required.", nameof(normalizedEmailHash));
        if (string.IsNullOrWhiteSpace(challengeKey))
            throw new ArgumentException("A challenge key is required.", nameof(challengeKey));
        if (string.IsNullOrWhiteSpace(protectedDeliveryKey))
            throw new ArgumentException("Protected delivery state is required.", nameof(protectedDeliveryKey));
        if (expiresAt <= createdAt)
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Expiry must follow creation.");
    }
}
