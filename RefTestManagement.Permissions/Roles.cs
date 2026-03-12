namespace Handball.Belgium.RefTestManagement.Permissions;

/// <summary>Auth0 role names configured in the Auth0 dashboard.</summary>
public static class Roles
{
    /// <summary>Full access; ref test creation is auto-approved.</summary>
    public const string Admin = "admin";

    /// <summary>Full access except approve; created ref tests require admin approval.</summary>
    public const string Instructor = "instructor";
}
