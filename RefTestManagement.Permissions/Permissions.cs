namespace Handball.Belgium.RefTestManagement.Permissions;

/// <summary>
/// All Auth0 permission scopes used in the application.
/// Assign these to roles in the Auth0 dashboard.
/// The access token will contain a <c>permissions</c> claim with the granted values.
/// </summary>
public static class Permission
{
    public static class RefTests
    {
        /// <summary>Read ref tests, titles and questions.</summary>
        public const string Read = "reftests:read";

        /// <summary>Create new ref tests. Instructors require admin approval after creation.</summary>
        public const string Create = "reftests:create";

        /// <summary>Delete ref tests.</summary>
        public const string Delete = "reftests:delete";

        /// <summary>Update participant details (name, email).</summary>
        public const string UpdateDetails = "reftests:update-details";

        /// <summary>Update the test configuration (title, questions, time).</summary>
        public const string UpdateConfiguration = "reftests:update-configuration";

        /// <summary>Extend the time on an in-progress test.</summary>
        public const string ExtendTime = "reftests:extend-time";

        /// <summary>Update automated notification settings.</summary>
        public const string UpdateNotifications = "reftests:update-notifications";

        /// <summary>Regenerate the access token for a pending test.</summary>
        public const string RegenerateToken = "reftests:regenerate-token";

        /// <summary>Soft or hard reset a completed or in-progress test.</summary>
        public const string Reset = "reftests:reset";

        /// <summary>Revive expired ref tests.</summary>
        public const string Revive = "reftests:revive";

        /// <summary>Send invitation emails.</summary>
        public const string SendInvitations = "reftests:send-invitations";

        /// <summary>Send result emails.</summary>
        public const string SendResults = "reftests:send-results";

        /// <summary>Send the aggregated report email.</summary>
        public const string SendReport = "reftests:send-report";

        /// <summary>
        /// Approve or reject ref tests created by instructors.
        /// Only users with this permission bypass the approval workflow when creating ref tests.
        /// </summary>
        public const string Approve = "reftests:approve";

        /// <summary>Read the audit log.</summary>
        public const string ReadAuditLog = "reftests:read-audit-log";
    }
}
