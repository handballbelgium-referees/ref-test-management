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

    private async Task CleanupOldAuditLogsAsync(CancellationToken cancellationToken)
    {
        ServiceLoggerMessages.LogCleanupStarting(_logger, "AuditLogs", _options.RetentionDays);

        using var scope = _serviceProvider.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RefTestManagementContext>>();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
        var archived = await context.AuditEvents
            .Where(a => a.Timestamp < cutoff && !a.IsArchived)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsArchived, true), cancellationToken);

        ServiceLoggerMessages.LogCleanupCompleted(_logger, "AuditLogs", archived);
    }
}
