using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

public interface IRefTestSessionTokenService
{
    /// <summary>Creates an expiring credential bound to the RefTest's current invitation digest.</summary>
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

/// <summary>Claims protected in a participant session credential.</summary>
public sealed record RefTestSessionTokenClaims(
    Guid RefTestId,
    string InvitationTokenHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? StartBy);

public static class RefTestSessionTokenFormat
{
    /// <summary>The prefix identifying protected participant session credentials.</summary>
    public const string TokenPrefix = "rts1.";

    private const int MaxTokenLength = 2048;

    /// <summary>Checks the bounded wire format before attempting to unprotect a credential.</summary>
    public static bool HasSessionTokenFormat(string? token) =>
        token is not null
        && token.Length > TokenPrefix.Length
        && token.Length <= MaxTokenLength
        && token.StartsWith(TokenPrefix, StringComparison.Ordinal)
        && token[TokenPrefix.Length..].All(static character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
