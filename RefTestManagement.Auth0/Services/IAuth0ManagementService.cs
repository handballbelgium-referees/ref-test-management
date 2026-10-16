using Handball.Belgium.RefTestManagement.Security;

namespace Handball.Belgium.RefTestManagement.Auth0.Services;

/// <summary>
/// Provides Auth0 Management API operations needed by the application:
/// approver discovery and permission synchronisation.
/// Permission synchronisation and per-user permission reads are inherited from
/// <see cref="IIdentityPermissionSync"/>; approver discovery from
/// <see cref="IIdentityUserDirectory"/>.
/// </summary>
public interface IAuth0ManagementService : IIdentityPermissionSync, IIdentityUserDirectory;
