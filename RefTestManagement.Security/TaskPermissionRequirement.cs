using Microsoft.AspNetCore.Authorization;

namespace Handball.Belgium.RefTestManagement.Security;

/// <summary>
/// Authorization requirement that demands a specific task-based permission.
/// </summary>
public sealed class TaskPermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
