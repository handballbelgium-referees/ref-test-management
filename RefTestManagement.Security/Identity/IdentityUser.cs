namespace Handball.Belgium.RefTestManagement.Security;

/// <summary>
/// A user of the configured identity provider, as needed for notifications.
/// </summary>
public record IdentityUser(string Name, string Email);
