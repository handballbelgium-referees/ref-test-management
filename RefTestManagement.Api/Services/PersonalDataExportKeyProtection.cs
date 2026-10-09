using Microsoft.AspNetCore.DataProtection;
using Handball.Belgium.RefTestManagement.Application.Abstractions;

namespace Handball.Belgium.RefTestManagement.Api.Services;

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
