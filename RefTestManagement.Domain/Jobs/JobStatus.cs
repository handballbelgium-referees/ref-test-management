namespace Handball.Belgium.RefTestManagement.Domain.Jobs;

/// <summary>
/// Represents the status of a background job
/// </summary>
public enum JobStatus
{
    Pending,
    Processing,
    Completed,
    Failed,

    // Must stay last: no explicit values are declared, so EF persists this enum by ordinal and
    // inserting a member anywhere else would silently reinterpret every existing row.
    Cancelled
}
