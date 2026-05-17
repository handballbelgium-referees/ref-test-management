using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Api.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handball.Belgium.RefTestManagement.Api.Controllers;

[Route("[controller]")]
public class AccountController : Controller
{
    [HttpGet("Login")]
    public Task Login(string returnUrl = "/")
    {
        return HttpContext.ChallengeAsync("Auth0", new AuthenticationProperties { RedirectUri = returnUrl });
    }
    
    [Authorize]
    [HttpGet("Permissions")]
    public IActionResult GetPermissions()
    {
        var permissions = User
            .FindAll("permissions")
            .Select(c => c.Value)
            .ToArray();

        return Ok(permissions);
    }

    [Authorize]
    [HttpGet("Logout")]
    public async Task Logout(string returnUrl = "/")
    {
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
