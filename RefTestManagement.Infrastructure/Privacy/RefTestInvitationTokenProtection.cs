using Handball.Belgium.RefTestManagement.Application.Services;
using Microsoft.AspNetCore.DataProtection;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Privacy;

/// <summary>Data-protects participant invitation tokens stored for delivery retries.</summary>
public sealed class RefTestInvitationTokenProtection : IRefTestInvitationTokenProtection
{
    private readonly IDataProtector _protector;

    public RefTestInvitationTokenProtection(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("RefTestManagement.RefTestInvitationToken.v1");
    }

    public string Protect(string token) => _protector.Protect(token);

    public string Unprotect(string protectedToken) => _protector.Unprotect(protectedToken);
}
