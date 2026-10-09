namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

/// <summary>
/// Runs <see cref="RunOnceAsync"/> on a fixed interval until the host stops. A failed pass is
/// handed to <see cref="LogFailure"/> and the loop keeps going; cancellation by the host ends the
/// loop quietly, wherever it is raised.
/// </summary>
public abstract class PollingBackgroundService : BackgroundService
{
    /// <summary>Delay before the first pass, so the host can finish starting.</summary>
    protected virtual TimeSpan StartupDelay => TimeSpan.Zero;

    /// <summary>Delay between the end of one pass and the start of the next.</summary>
    protected abstract TimeSpan Interval { get; }

    /// <summary>Does one pass of work.</summary>
    protected abstract Task RunOnceAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Logs a failed pass. Each service decides what may be logged, because the exception can quote
    /// the personal data the pass was reading or rewriting.
    /// </summary>
    protected abstract void LogFailure(Exception exception);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (StartupDelay > TimeSpan.Zero)
                await Task.Delay(StartupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    LogFailure(exception);
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }
}
