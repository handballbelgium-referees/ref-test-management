using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Api.Extensions;
using Handball.Belgium.RefTestManagement.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handball.Belgium.RefTestManagement.Api.Controllers;

[Route("[controller]")]
public class AccountController(IPermissionSnapshotService permissionSnapshotService) : Controller
{
    [HttpGet("Login")]
    public Task Login(string returnUrl = "/")
    {
        if (!Url.IsLocalUrl(returnUrl)) returnUrl = "/";

        return HttpContext.ChallengeAsync("Auth0", new AuthenticationProperties { RedirectUri = returnUrl });
    }
    
    [Authorize]
    [HttpGet("Permissions")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetPermissions(CancellationToken cancellationToken)
    {
        var permissions = await permissionSnapshotService.GetCurrentPermissionsAsync(User, cancellationToken);
        if (permissions is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        return Ok(permissions.OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    [Authorize]
    [HttpGet("Logout")]
    public async Task Logout(string returnUrl = "/")
    {
        if (!Url.IsLocalUrl(returnUrl)) returnUrl = "/";

        await HttpContext.SignOutAsync("Auth0", new AuthenticationProperties { RedirectUri = returnUrl });
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpGet("User")]
    public IActionResult GetUser()
    {
        var user = new
        {
            Name = User.GetDisplayName(),
            Email = User.GetEmail() is { Length: > 0 } e ? e : null,
            Picture = User.FindFirst("picture")?.Value,
            Sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
        };

        return Ok(user);
    }
    
    [HttpGet("IsAuthenticated")]
    public ActionResult IsAuthenticated()
    {
        return Ok(User.Identity is { IsAuthenticated: true });
    }
}
