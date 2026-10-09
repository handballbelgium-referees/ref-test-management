namespace Handball.Belgium.RefTestManagement.Infrastructure.Jobs;

/// <summary>
/// Indicates that a report payload predates the required per-RefTest association.
/// </summary>
public sealed class LegacyReportPayloadException(string message) : JobPayloadException(message);
