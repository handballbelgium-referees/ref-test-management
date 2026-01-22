using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

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

public class JobEnqueueService(RefTestManagementContext context, ILogger<JobEnqueueService> logger)
    : IJobEnqueueService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task EnqueueInvitationEmailAsync(InvitationEmailPayload payload, DateTime? executeAfter = null, CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.InvitationEmail, payloadJson, executeAfter);
        
        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);

        ServiceLoggerMessages.LogEnqueuedInvitationEmail(logger, job.Id, payload.Email);
    }

    public async Task EnqueueResultEmailAsync(ResultEmailPayload payload, DateTime? executeAfter = null, CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.ResultEmail, payloadJson, executeAfter);
        
        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);

        ServiceLoggerMessages.LogEnqueuedResultEmail(logger, job.Id, payload.Email);
    }

    public async Task EnqueueReportEmailAsync(ReportEmailPayload payload, DateTime? executeAfter = null, CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.ReportEmail, payloadJson, executeAfter);
        
        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);

        ServiceLoggerMessages.LogEnqueuedReportEmail(logger, job.Id, payload.RecipientEmails.Length);
    }
}
