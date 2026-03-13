using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.AuditLog;

internal interface IAuditLogContext
{
    DbSet<AuditLog> AuditLogs { get; }
}
