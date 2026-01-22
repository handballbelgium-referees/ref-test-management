using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain;
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

public partial class JobEnqueueService(RefTestManagementContext context, ILogger<JobEnqueueService> logger)
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

        LogEnqueuedInvitationEmailJobJobIdForEmail(logger, job.Id, payload.Email);
    }

    public async Task EnqueueResultEmailAsync(ResultEmailPayload payload, DateTime? executeAfter = null, CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.ResultEmail, payloadJson, executeAfter);
        
        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);

        LogEnqueuedResultEmailJobJobIdForEmail(logger, job.Id, payload.Email);
    }

    public async Task EnqueueReportEmailAsync(ReportEmailPayload payload, DateTime? executeAfter = null, CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var job = Job.Create(JobType.ReportEmail, payloadJson, executeAfter);
        
        context.Jobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);

        LogEnqueuedReportEmailJobJobIdForRecipientCountRecipients(logger, job.Id, payload.RecipientEmails.Length);
    }

    [LoggerMessage(LogLevel.Information, "Enqueued invitation email job {jobId} for {email}")]
    static partial void LogEnqueuedInvitationEmailJobJobIdForEmail(ILogger<JobEnqueueService> logger, Guid jobId, string email);

    [LoggerMessage(LogLevel.Information, "Enqueued result email job {jobId} for {email}")]
    static partial void LogEnqueuedResultEmailJobJobIdForEmail(ILogger<JobEnqueueService> logger, Guid jobId, string email);

    [LoggerMessage(LogLevel.Information, "Enqueued report email job {jobId} for {recipientCount} recipients")]
    static partial void LogEnqueuedReportEmailJobJobIdForRecipientCountRecipients(ILogger<JobEnqueueService> logger, Guid jobId, int recipientCount);
}
