using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Permissions.AuditLog;

[QueryType]
public static class AuditLogQueries
{
    [Authorize(Policy = Permission.RefTests.ReadAuditLog)]
    [UsePaging]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<AuditLog> GetAuditLogs([Service] IAuditLogContext context)
        => context.AuditLogs.AsNoTracking().OrderByDescending(x => x.PerformedAt);
}
