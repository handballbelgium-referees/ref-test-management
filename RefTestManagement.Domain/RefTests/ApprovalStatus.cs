namespace Handball.Belgium.RefTestManagement.Domain.RefTests;

public enum ApprovalStatus
{
    /// <summary>Auto-approved (created by an admin) or explicitly approved by an admin.</summary>
    Approved = 0,

    /// <summary>Created by an instructor and waiting for admin approval.</summary>
    PendingApproval = 1,

    /// <summary>Rejected by an admin.</summary>
    Rejected = 2
}
