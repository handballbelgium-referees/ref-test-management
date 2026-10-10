using Handball.Belgium.RefTestManagement.Application.Models;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

public sealed class ReportEmailJobQueue(IJobEnqueueService jobEnqueueService) : IReportEmailJobQueue
{
    public Task EnqueueAsync(ReportEmailPayload payload, CancellationToken cancellationToken) =>
        jobEnqueueService.EnqueueReportEmailAsync(payload, cancellationToken: cancellationToken);
}
