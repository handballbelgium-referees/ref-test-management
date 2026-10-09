namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

/// <summary>Protects the short-lived challenge key needed by retryable email delivery.</summary>
public interface IPersonalDataExportKeyProtection
{
    string Protect(string key);
    string Unprotect(string protectedKey);
}
