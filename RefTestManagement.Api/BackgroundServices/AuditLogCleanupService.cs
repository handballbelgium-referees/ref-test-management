using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Domain.RefTests.Events;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

public class AuditLogCleanupService(
    IServiceProvider serviceProvider,
    ILogger<AuditLogCleanupService> logger,
    AuditLogOptions options) : PollingBackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<AuditLogCleanupService> _logger = logger;
    private readonly AuditLogOptions _options = options;

    protected override TimeSpan StartupDelay => TimeSpan.FromSeconds(10);

    protected override TimeSpan Interval => TimeSpan.FromHours(_options.CleanupIntervalHours);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServiceLoggerMessages.LogServiceStarting(_logger, nameof(AuditLogCleanupService));
        await base.ExecuteAsync(stoppingToken);
        ServiceLoggerMessages.LogServiceStopping(_logger, nameof(AuditLogCleanupService));
    }

    protected override Task RunOnceAsync(CancellationToken cancellationToken) =>
        CleanupOldAuditLogsAsync(cancellationToken);

    // A database failure here is raised while reading and rewriting the audit Data column, which
    // holds the personal data this service exists to redact.
    protected override void LogFailure(Exception exception) =>
        ServiceLoggerMessages.LogCleanupError(_logger, LogRedaction.MaskEmails(exception), "AuditLogs");

    private const int RedactionBatchSize = 500;

    // The first run after deployment faces the entire historical backlog. Cap the work so it
    // cannot monopolise the daily window; the remainder is picked up on the next run.
    private const int MaxRedactionsPerRun = 50_000;

    private async Task CleanupOldAuditLogsAsync(CancellationToken cancellationToken)
    {
        ServiceLoggerMessages.LogCleanupStarting(_logger, "AuditLogs", _options.RetentionDays);

        using var scope = _serviceProvider.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RefTestManagementContext>>();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var archived = await RedactOldAuditLogsAsync(
            context,
            _options,
            DateTime.UtcNow,
            cancellationToken);

        if (archived >= MaxRedactionsPerRun)
            ServiceLoggerMessages.LogCleanupBatchLimitReached(_logger, "AuditLogs", archived);

        ServiceLoggerMessages.LogCleanupCompleted(_logger, "AuditLogs", archived);
    }

    internal static async Task<int> RedactOldAuditLogsAsync(
        RefTestManagementContext context,
        AuditLogOptions options,
        DateTime now,
        CancellationToken cancellationToken,
        int maxRedactionsPerRun = MaxRedactionsPerRun)
    {
        var cutoff = now.AddDays(-options.RetentionDays);
        var archived = 0;
        if (maxRedactionsPerRun <= 0)
            return archived;

        // Archiving must strip the personal data, not just flag the row: past the retention
        // window there is no longer a lawful basis to keep the participant's name and email, and
        // nothing else ever removes them — these rows carry no FK to the RefTest, so erasing or
        // deleting a RefTest does not reach them.
        //
        // This reads and writes in batches rather than using ExecuteUpdateAsync: the payload is
        // JSON in a text column and there is no provider-portable way to rewrite it in SQL
        // across the four supported databases. The service runs daily, so the cost is bounded.
        //
        // RedactedAt is the normal cursor, not IsArchived. The residual JSON predicate also
        // catches rejection reasons that survived an earlier RedactedAt stamp; replacing the
        // value with the marker makes that predicate advance without a durable cursor.
        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await context.AuditEvents
                .Where(a => a.Timestamp < cutoff
                            && (a.RedactedAt == null
                                || (a.Type == RefTestRejectedEvent.EventType
                                    && a.Data != null
                                    && a.Data.ToLower().Contains("\"reason\":\"")
                                    && !a.Data.ToLower().Contains("\"reason\":\"***\""))))
                .OrderBy(a => a.SeqId)
                .Take(Math.Min(RedactionBatchSize, maxRedactionsPerRun - archived))
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
                break;

            foreach (var auditEvent in batch)
            {
                // AuditEvent is init-only by design, so write through the change tracker.
                var entry = context.Entry(auditEvent);

                entry.Property(e => e.Data).CurrentValue =
                    AuditPiiRedactor.RedactData(auditEvent.Data, auditEvent.Type);

                // A previously redacted row needs only the residual payload repair. Preserve its
                // original actor, archive state, and RedactedAt timestamp.
                if (auditEvent.RedactedAt is not null)
                    continue;

                // The accountability trail keeps what happened and when; who did it is personal
                // data in its own right and expires with the same retention window.
                entry.Property(e => e.ActorName).CurrentValue = AuditPiiRedactor.RedactedValue;
                entry.Property(e => e.ActorEmail).CurrentValue = AuditPiiRedactor.RedactedValue;

                entry.Property(e => e.IsArchived).CurrentValue = true;
                entry.Property(e => e.RedactedAt).CurrentValue = now;
            }

            await context.SaveChangesAsync(cancellationToken);
            archived += batch.Count;

            // Without this the tracked entities from every batch accumulate for the whole run,
            // so DetectChanges slows down quadratically and memory grows with the backlog. The
            // first run after deployment processes the entire history, so it matters there most.
            context.ChangeTracker.Clear();

            if (archived >= maxRedactionsPerRun)
            {
                break;
            }
        }

        return archived;
    }
}
