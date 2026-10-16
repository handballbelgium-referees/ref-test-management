using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Handball.Belgium.RefTestManagement.Security;

/// <summary>
/// Extends the default policy provider to handle <c>anyof:</c> policies at runtime.
/// </summary>
/// <remarks>
/// A policy named <c>anyof:perm1|perm2|perm3</c> is resolved to an
/// <see cref="AnyTaskPermissionRequirement"/> containing those permissions.
/// All other policy names fall through to <see cref="DefaultAuthorizationPolicyProvider"/>.
/// </remarks>
public sealed class TaskAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    /// <summary>The prefix used to identify "any-of" combined permission policies.</summary>
    private const string AnyOfPrefix = "anyof:";

    /// <summary>The prefix for policies that also require live confirmation from the identity provider.</summary>
    private const string LivePrefix = "live:";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(LivePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName[LivePrefix.Length..];
            return new AuthorizationPolicyBuilder()
                .AddRequirements(
                    new TaskPermissionRequirement(permission),
                    new LivePermissionRequirement(permission))
                .Build();
        }

        if (!policyName.StartsWith(AnyOfPrefix, StringComparison.OrdinalIgnoreCase))
            return await base.GetPolicyAsync(policyName);
        var permissions = policyName[AnyOfPrefix.Length..].Split('|');
        return new AuthorizationPolicyBuilder()
            .AddRequirements(new AnyTaskPermissionRequirement(permissions))
            .Build();
    }

    /// <summary>
    /// Builds a policy name that checks <paramref name="permission"/> in the cached claims and then against the identity provider.
    /// </summary>
    public static string Live(string permission) => LivePrefix + permission;

    /// <summary>
    /// Builds the policy name for a given set of permissions (OR semantics).
    /// Use this to pass to <c>.Authorize(...)</c> without hard-coding the convention.
    /// </summary>
    public static string AnyOf(params string[] permissions) =>
        AnyOfPrefix + string.Join('|', permissions);
}