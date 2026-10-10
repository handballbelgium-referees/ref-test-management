using Handball.Belgium.RefTestManagement.Application.Models;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

public interface IReportEmailJobQueue
{
    Task EnqueueAsync(ReportEmailPayload payload, CancellationToken cancellationToken);
}
