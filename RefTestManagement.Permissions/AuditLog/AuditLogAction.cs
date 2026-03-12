namespace Handball.Belgium.RefTestManagement.Permissions.AuditLog;

/// <summary>Well-known action identifiers written to the audit log.</summary>
public static class AuditLogAction
{
    public static class RefTest
    {
        public const string Create = "RefTest.Create";
        public const string Delete = "RefTest.Delete";
        public const string UpdateDetails = "RefTest.UpdateDetails";
        public const string UpdateConfiguration = "RefTest.UpdateConfiguration";
        public const string ExtendTime = "RefTest.ExtendTime";
        public const string UpdateNotifications = "RefTest.UpdateNotifications";
        public const string RegenerateToken = "RefTest.RegenerateToken";
        public const string Reset = "RefTest.Reset";
        public const string Revive = "RefTest.Revive";
        public const string SendInvitations = "RefTest.SendInvitations";
        public const string SendResults = "RefTest.SendResults";
        public const string SendReport = "RefTest.SendReport";
        public const string Approve = "RefTest.Approve";
        public const string Reject = "RefTest.Reject";
    }
}
