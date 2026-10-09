using Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Persistence;

public sealed class PersonalDataExportRequestRepository(RefTestManagementContext context)
    : IPersonalDataExportRequestRepository
{
    public async Task ClearForEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var requests = await context.PersonalDataExportRequests
            .Where(request => request.Email.Trim().ToUpper() == normalizedEmail)
            .ToListAsync(cancellationToken);
        foreach (var request in requests)
            request.ClearForPrivacyErasure();
    }
}
