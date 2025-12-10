using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuizManagement.Api.Controllers;

public class AccountController : Controller
{
    public Task Login(string returnUrl = "/")
    {
        return HttpContext.ChallengeAsync("Auth0", new AuthenticationProperties { RedirectUri = returnUrl });
    }
    
    [Authorize]
    public async Task Logout()
    {
        await HttpContext.SignOutAsync("Auth0");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpGet("user")]
    public IActionResult GetUser()
    {
        var user = new
        {
            Name = User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst("name")?.Value
                ?? "Unknown",
            Email = User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst("email")?.Value,
            Picture = User.FindFirst("picture")?.Value,
            Sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
        };

        return Ok(user);
    }
}
