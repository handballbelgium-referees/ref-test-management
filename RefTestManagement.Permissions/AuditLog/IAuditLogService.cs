namespace Handball.Belgium.RefTestManagement.Permissions.AuditLog;

public interface IAuditLogService
{
    Task LogAsync(
        string action,
        string userEmail,
        string? userName = null,
        string? resourceId = null,
        string? details = null,
        CancellationToken cancellationToken = default);
}
