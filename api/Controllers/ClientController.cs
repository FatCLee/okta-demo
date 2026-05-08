using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Okta")]
public sealed class ClientController : ControllerBase
{
    [HttpGet("session")]
    public ActionResult<object> GetSessionSummary()
    {
        var scopes = User.FindFirstValue("scp")
            ?? string.Join(", ", User.FindAll("scp").Select(claim => claim.Value));

        return Ok(new
        {
            subject = User.FindFirstValue("sub"),
            email = User.FindFirstValue("email") ?? User.FindFirstValue(ClaimTypes.Email),
            scopes,
            authenticationType = User.Identity?.AuthenticationType ?? "Bearer"
        });
    }
}
