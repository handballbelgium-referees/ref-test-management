namespace Handball.Belgium.RefTestManagement.Domain;

/// <summary>
/// Represents the status of a background job
/// </summary>
public enum JobStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
