using System.Threading.RateLimiting;
using Handball.Belgium.RefTestManagement.Application.Configurations;

namespace Handball.Belgium.RefTestManagement.Api.Services;

/// <summary>
/// Applies independent fixed-window limits to the public request and confirmation operations.
/// </summary>
public interface IPersonalDataExportRateLimiter
{
    bool TryAcquireRequest(string clientAddress);
    bool TryAcquireConfirmation(string clientAddress);
}

public sealed class PersonalDataExportRateLimiter : IPersonalDataExportRateLimiter, IDisposable
{
    private readonly PartitionedRateLimiter<string> _requestLimiter;
    private readonly PartitionedRateLimiter<string> _confirmationLimiter;

    public PersonalDataExportRateLimiter(PersonalDataExportConfiguration configuration)
    {
        var window = TimeSpan.FromSeconds(configuration.RateLimitWindowSeconds);
        _requestLimiter = CreateLimiter(configuration.RequestRateLimitPermitLimit, window);
        _confirmationLimiter = CreateLimiter(configuration.ConfirmationRateLimitPermitLimit, window);
    }

    public bool TryAcquireRequest(string clientAddress) => TryAcquire(_requestLimiter, clientAddress);

    public bool TryAcquireConfirmation(string clientAddress) => TryAcquire(_confirmationLimiter, clientAddress);

    public void Dispose()
    {
        _requestLimiter.Dispose();
        _confirmationLimiter.Dispose();
    }

    private static PartitionedRateLimiter<string> CreateLimiter(int permitLimit, TimeSpan window) =>
        PartitionedRateLimiter.Create<string, string>(clientAddress =>
            RateLimitPartition.GetFixedWindowLimiter(
                clientAddress,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

    private static bool TryAcquire(PartitionedRateLimiter<string> limiter, string clientAddress)
    {
        using var lease = limiter.AttemptAcquire(clientAddress);
        return lease.IsAcquired;
    }
}
