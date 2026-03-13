using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.AuditLog;

internal sealed class AuditLogService(IDbContextFactory<AuditLogContext> factory) : IAuditLogService
{
    public async Task LogAsync(
        string action,
        string userEmail,
        string? userName = null,
        string? resourceId = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var entry = AuditLog.Create(action, userEmail, userName, resourceId, details);
        context.AuditLogs.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
    }
}
