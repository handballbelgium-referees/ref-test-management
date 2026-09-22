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

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(AnyOfPrefix, StringComparison.OrdinalIgnoreCase))
            return await base.GetPolicyAsync(policyName);
        var permissions = policyName[AnyOfPrefix.Length..].Split('|');
        return new AuthorizationPolicyBuilder()
            .AddRequirements(new AnyTaskPermissionRequirement(permissions))
            .Build();
    }

    /// <summary>
    /// Builds the policy name for a given set of permissions (OR semantics).
    /// Use this to pass to <c>.Authorize(...)</c> without hard-coding the convention.
    /// </summary>
    public static string AnyOf(params string[] permissions) =>
        AnyOfPrefix + string.Join('|', permissions);
}