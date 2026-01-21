using Handball.Belgium.RefTestManagement.Application.Models;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

/// <summary>
/// Service for enqueuing background jobs
/// </summary>
public interface IJobEnqueueService
{
    Task EnqueueInvitationEmailAsync(InvitationEmailPayload payload, DateTime? executeAfter = null, CancellationToken cancellationToken = default);
    Task EnqueueResultEmailAsync(ResultEmailPayload payload, DateTime? executeAfter = null, CancellationToken cancellationToken = default);
    Task EnqueueReportEmailAsync(ReportEmailPayload payload, DateTime? executeAfter = null, CancellationToken cancellationToken = default);
}