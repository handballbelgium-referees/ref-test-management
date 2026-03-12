using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Permissions.AuditLog;

internal sealed class AuditLogService<TContext> : IAuditLogService
    where TContext : DbContext, IAuditLogContext
{
    private readonly IDbContextFactory<TContext> _factory;

    public AuditLogService(IDbContextFactory<TContext> factory) => _factory = factory;

    public async Task LogAsync(
        string action,
        string userEmail,
        string? userName = null,
        string? resourceId = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var entry = AuditLog.Create(action, userEmail, userName, resourceId, details);
        context.AuditLogs.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
    }
}
