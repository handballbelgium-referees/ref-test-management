using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Queries;

[QueryType]
public static class AuditLogQueries
{
    [Authorize(Policy = Permissions.AuditLogs.View)]
    [UsePaging]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<AuditLogDto> GetAuditLogs(
        RefTestManagementContext context)
    {
        return context.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Action = a.Action,
                Changes = a.Changes,
                ActorName = a.ActorName,
                ActorEmail = a.ActorEmail,
                Timestamp = a.Timestamp
            });
    }
}
