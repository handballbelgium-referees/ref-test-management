using System.Security.Cryptography;
using System.Text.Json;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Microsoft.AspNetCore.DataProtection;

namespace Handball.Belgium.RefTestManagement.Api.Services;

public interface IRefTestSessionTokenService
{
    /// <summary>Creates an expiring credential for the RefTest's current invitation digest.</summary>
    /// <param name="refTest">The RefTest the credential will authorize.</param>
    /// <returns>A protected participant session credential.</returns>
    string Create(RefTest refTest);

    /// <summary>Unprotects a credential and extracts its bound RefTest and digest.</summary>
    /// <param name="sessionToken">The participant session credential.</param>
    /// <param name="claims">The protected credential claims, if the credential is well formed.</param>
    /// <returns><see langword="true"/> when the credential can be unprotected.</returns>
    bool TryUnprotect(string? sessionToken, out RefTestSessionTokenClaims? claims);

    /// <summary>Checks credential expiry against the RefTest's current state and deadline.</summary>
    /// <param name="claims">The unprotected credential claims.</param>
    /// <param name="refTest">The RefTest bound to the credential.</param>
    /// <returns><see langword="true"/> when the credential is currently valid for the RefTest.</returns>
    bool IsValidFor(RefTestSessionTokenClaims claims, RefTest refTest);
}

public sealed record RefTestSessionTokenClaims(
    Guid RefTestId,
    string InvitationTokenHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? StartBy);

/// <summary>
/// Protects short-lived participant session credentials bound to a RefTest and its current
/// invitation-token digest.
/// </summary>
public sealed class RefTestSessionTokenService : IRefTestSessionTokenService
{
    public const string TokenPrefix = "rts1.";

    private const int MaxTokenLength = 2048;
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(12);
    private readonly IDataProtector _protector;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the participant session-token service.</summary>
    /// <param name="provider">The application data-protection provider.</param>
    /// <param name="timeProvider">The clock used to determine credential expiration.</param>
    public RefTestSessionTokenService(IDataProtectionProvider provider, TimeProvider timeProvider)
    {
        _protector = provider.CreateProtector("RefTestManagement.RefTestSessionToken.v1");
        _timeProvider = timeProvider;
    }

    public string Create(RefTest refTest)
    {
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.Add(DefaultLifetime);

        if (refTest.Status == RefTestStatus.InProgress && refTest.StartedAt.HasValue)
        {
            var testDeadline = new DateTimeOffset(
                    DateTime.SpecifyKind(refTest.StartedAt.Value, DateTimeKind.Utc))
                .AddMinutes(refTest.MaxTimeInMinutes)
                .AddHours(1);

            if (testDeadline > expiresAt)
                expiresAt = testDeadline;
        }

        DateTimeOffset? startBy = refTest.Status switch
        {
            RefTestStatus.Pending => expiresAt,
            RefTestStatus.InProgress => now,
            _ => null
        };
        var payload = new SessionTokenPayload(refTest.Id, refTest.Token, expiresAt, startBy);
        var protectedPayload = _protector.Protect(JsonSerializer.Serialize(payload));
        return TokenPrefix + protectedPayload;
    }

    public bool TryUnprotect(string? sessionToken, out RefTestSessionTokenClaims? claims)
    {
        claims = null;

        if (!HasSessionTokenFormat(sessionToken))
            return false;

        SessionTokenPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SessionTokenPayload>(
                _protector.Unprotect(sessionToken![TokenPrefix.Length..]));
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }

        if (payload is null
            || payload.RefTestId == Guid.Empty
            || payload.InvitationTokenHash is not { Length: 64 }
            || payload.ExpiresAt == default
            || payload.StartBy.HasValue && payload.StartBy.Value == default)
        {
            return false;
        }

        claims = new RefTestSessionTokenClaims(
            payload.RefTestId,
            payload.InvitationTokenHash,
            payload.ExpiresAt,
            payload.StartBy);
        return true;
    }

    public bool IsValidFor(RefTestSessionTokenClaims claims, RefTest refTest)
    {
        if (refTest.IsAnonymized
            || claims.RefTestId != refTest.Id
            || !string.Equals(claims.InvitationTokenHash, refTest.Token, StringComparison.Ordinal))
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        if (claims.ExpiresAt > now)
            return true;

        if (refTest.Status != RefTestStatus.InProgress
            || !refTest.StartedAt.HasValue
            || claims.StartBy is { } startByValue && startByValue == default)
        {
            return false;
        }

        var startedAt = new DateTimeOffset(
            DateTime.SpecifyKind(refTest.StartedAt.Value, DateTimeKind.Utc));
        // Older protected payloads did not include StartBy; their original expiry is the safest
        // compatible cutoff for when the test must have started.
        var startBy = claims.StartBy ?? claims.ExpiresAt;
        if (startedAt > startBy)
            return false;

        // An in-progress test may outlive the credential's initial expiry. Recompute its deadline
        // from the persisted RefTest so administrative time extensions are honored as well.
        var testDeadline = startedAt
            .AddMinutes(refTest.MaxTimeInMinutes)
            .AddHours(1);
        return now < testDeadline;
    }

    public static bool HasSessionTokenFormat(string? token) =>
        token is not null
        && token.Length > TokenPrefix.Length
        && token.Length <= MaxTokenLength
        && token.StartsWith(TokenPrefix, StringComparison.Ordinal)
        && token[TokenPrefix.Length..].All(static character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private sealed record SessionTokenPayload(
        Guid RefTestId,
        string InvitationTokenHash,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? StartBy);
}
