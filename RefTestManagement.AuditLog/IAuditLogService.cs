namespace Handball.Belgium.RefTestManagement.AuditLog;

internal interface IAuditLogService
{
    Task LogAsync(
        string action,
        string userEmail,
        string? userName = null,
        string? resourceId = null,
        string? details = null,
        CancellationToken cancellationToken = default);
}
