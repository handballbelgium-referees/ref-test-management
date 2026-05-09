namespace Handball.Belgium.RefTestManagement.Security;

/// <summary>
/// Task-based permission constants. Each constant maps 1:1 to a protected GraphQL operation.
/// Wildcards (e.g. <see cref="RefTests.All"/>) grant all permissions in the namespace.
/// <see cref="Superadmin"/> bypasses all permission checks.
/// </summary>
public static class Permissions
{
    /// <summary>Grants unrestricted access to all operations, bypassing all permission checks.</summary>
    public const string Superadmin = "superadmin";

    public static class RefTests
    {
        // Mutations
        public const string Create = "ref-tests:create";
        public const string Delete = "ref-tests:delete";
        public const string UpdateDetails = "ref-tests:update-details";
        public const string UpdateConfiguration = "ref-tests:update-configuration";
        public const string UpdateNotifications = "ref-tests:update-notifications";
        public const string ExtendTime = "ref-tests:extend-time";
        public const string RegenerateToken = "ref-tests:regenerate-token";
        public const string Reset = "ref-tests:reset";
        public const string Revive = "ref-tests:revive";
        public const string SendInvitations = "ref-tests:send-invitations";
        public const string SendResults = "ref-tests:send-results";
        public const string SendReport = "ref-tests:send-report";

        // Queries & subscriptions
        public const string ViewList = "ref-tests:view-list";
        public const string ViewDetail = "ref-tests:view-detail";
        public const string ViewDetailQuestions = "ref-tests:view-detail-questions";
        public const string ViewTitles = "ref-tests:view-titles";

        /// <summary>Wildcard — grants all ref-tests:* permissions.</summary>
        public const string All = "ref-tests:*";
    }

    public static class Questions
    {
        public const string Search = "questions:search";
        public const string View = "questions:view";

        /// <summary>Wildcard — grants all questions:* permissions.</summary>
        public const string All = "questions:*";
    }

    /// <summary>
    /// All individual (non-wildcard) permissions. Used to register authorization policies.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        RefTests.Create,
        RefTests.Delete,
        RefTests.UpdateDetails,
        RefTests.UpdateConfiguration,
        RefTests.UpdateNotifications,
        RefTests.ExtendTime,
        RefTests.RegenerateToken,
        RefTests.Reset,
        RefTests.Revive,
        RefTests.SendInvitations,
        RefTests.SendResults,
        RefTests.SendReport,
        RefTests.ViewList,
        RefTests.ViewDetail,
        RefTests.ViewDetailQuestions,
        RefTests.ViewTitles,
        Questions.Search,
        Questions.View,
    ];
}
