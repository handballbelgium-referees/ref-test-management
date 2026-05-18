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
        return context.AuditEvents
            .Where(a => !a.IsArchived)
            .OrderByDescending(a => a.SeqId)
            .Select(a => new AuditLogDto
            {
                SeqId = a.SeqId,
                Id = a.Id,
                StreamId = a.StreamId,
                Version = a.Version,
                Data = a.Data,
                Type = a.Type,
                Timestamp = a.Timestamp,
                ActorName = a.ActorName,
                ActorEmail = a.ActorEmail,
                Headers = a.Headers,
                IsArchived = a.IsArchived,
                RefTestExists = context.RefTests.Any(rt => rt.Id.ToString() == a.StreamId)
            });
    }
}
