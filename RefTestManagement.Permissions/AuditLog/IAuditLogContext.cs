using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Permissions.AuditLog;

/// <summary>
/// Implement this interface on your DbContext so that <see cref="AuditLogExtensions.AddAuditLogService{TContext}"/>
/// can register the bundled <see cref="IAuditLogService"/> implementation without exposing it publicly.
/// </summary>
public interface IAuditLogContext
{
    DbSet<AuditLog> AuditLogs { get; }
}
