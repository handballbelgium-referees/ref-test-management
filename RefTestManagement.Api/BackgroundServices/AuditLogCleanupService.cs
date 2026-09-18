using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

public class AuditLogCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditLogCleanupService> _logger;
    private readonly AuditLogOptions _options;

    public AuditLogCleanupService(
        IServiceProvider serviceProvider,
        ILogger<AuditLogCleanupService> logger,
        AuditLogOptions options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServiceLoggerMessages.LogServiceStarting(_logger, nameof(AuditLogCleanupService));

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldAuditLogsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                ServiceLoggerMessages.LogCleanupError(_logger, ex, "AuditLogs");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(_options.CleanupIntervalHours), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        ServiceLoggerMessages.LogServiceStopping(_logger, nameof(AuditLogCleanupService));
    }

    private const int RedactionBatchSize = 500;

    private async Task CleanupOldAuditLogsAsync(CancellationToken cancellationToken)
    {
        ServiceLoggerMessages.LogCleanupStarting(_logger, "AuditLogs", _options.RetentionDays);

        using var scope = _serviceProvider.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RefTestManagementContext>>();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
        var archived = 0;

        // Archiving must strip the personal data, not just flag the row: past the retention
        // window there is no longer a lawful basis to keep the participant's name and email, and
        // nothing else ever removes them — these rows carry no FK to the RefTest, so erasing or
        // deleting a RefTest does not reach them.
        //
        // This reads and writes in batches rather than using ExecuteUpdateAsync: the payload is
        // JSON in a text column and there is no provider-portable way to rewrite it in SQL
        // across the four supported databases. The service runs daily, so the cost is bounded.
        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await context.AuditEvents
                .Where(a => a.Timestamp < cutoff && !a.IsArchived)
                .OrderBy(a => a.SeqId)
                .Take(RedactionBatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
                break;

            foreach (var auditEvent in batch)
            {
                // AuditEvent is init-only by design, so write through the change tracker.
                var entry = context.Entry(auditEvent);

                entry.Property(e => e.Data).CurrentValue = AuditPiiRedactor.RedactData(auditEvent.Data);

                // The accountability trail keeps what happened and when; who did it is personal
                // data in its own right and expires with the same retention window.
                entry.Property(e => e.ActorName).CurrentValue = AuditPiiRedactor.RedactedValue;
                entry.Property(e => e.ActorEmail).CurrentValue = AuditPiiRedactor.RedactedValue;

                entry.Property(e => e.IsArchived).CurrentValue = true;
            }

            await context.SaveChangesAsync(cancellationToken);
            archived += batch.Count;
        }

        ServiceLoggerMessages.LogCleanupCompleted(_logger, "AuditLogs", archived);
    }
}
