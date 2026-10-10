namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

/// <summary>Protects invitation tokens persisted for retryable email delivery.</summary>
public interface IRefTestInvitationTokenProtection
{
    /// <summary>Protects a raw invitation token for storage.</summary>
    string Protect(string token);

    /// <summary>Unprotects an invitation token for email delivery.</summary>
    string Unprotect(string protectedToken);
}
