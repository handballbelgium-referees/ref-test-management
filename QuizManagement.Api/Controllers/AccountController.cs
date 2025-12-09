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

    public ActionResult IsAuthenticated()
    {
        return Ok(User.Identity is { IsAuthenticated: true });
    }
}