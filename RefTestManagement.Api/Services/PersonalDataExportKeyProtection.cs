using Microsoft.AspNetCore.DataProtection;

namespace Handball.Belgium.RefTestManagement.Api.Services;

/// <summary>Protects the short-lived challenge key needed by retryable email delivery.</summary>
public interface IPersonalDataExportKeyProtection
{
    string Protect(string key);
    string Unprotect(string protectedKey);
}

public sealed class PersonalDataExportKeyProtection : IPersonalDataExportKeyProtection
{
    private const string Purpose = "RefTestManagement.PersonalDataExportChallenge.v1";
    private readonly IDataProtector _protector;

    public PersonalDataExportKeyProtection(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string key) => _protector.Protect(key);

    public string Unprotect(string protectedKey) => _protector.Unprotect(protectedKey);
}
