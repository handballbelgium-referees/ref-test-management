using Handball.Belgium.RefTestManagement.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Handball.Belgium.RefTestManagement.Api.Controllers;

[ApiController]
[Route("api/configuration")]
public sealed class IdentityConfigurationController(IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("identity-provider")]
    public IActionResult GetIdentityProvider()
    {
        var provider = IdentityProviderSelector.Resolve(configuration);
        return Ok(new { provider = IdentityProviderSelector.SchemeName(provider) });
    }
}
