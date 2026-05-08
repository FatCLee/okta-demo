using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace api.Controllers;

[ApiController]
[Route("api/rm")]
[Authorize(AuthenticationSchemes = "Entra")]
public sealed class RmController : ControllerBase
{
    [HttpGet("session")]
    public ActionResult<object> GetSessionSummary()
    {
        var scopes = User.FindFirstValue("scp")
            ?? string.Join(", ", User.FindAll("scp").Select(claim => claim.Value));

        return Ok(new
        {
            subject = User.FindFirstValue("sub"),
            objectId = User.FindFirstValue("oid"),
            tenantId = User.FindFirstValue("tid"),
            name = User.FindFirstValue("name"),
            email = User.FindFirstValue("preferred_username")
                ?? User.FindFirstValue("email")
                ?? User.FindFirstValue(ClaimTypes.Email),
            scopes,
            authenticationType = User.Identity?.AuthenticationType ?? "Bearer"
        });
    }
}
