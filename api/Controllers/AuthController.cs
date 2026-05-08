using api.Infrastructure;
using api.Models;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(DemoDataStore dataStore) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResult>> Login(LoginRequest request, CancellationToken cancellationToken) =>
        Ok(await dataStore.StartLoginAsync(request, cancellationToken));

    [HttpPost("redeem-invite")]
    public async Task<ActionResult<LoginResult>> RedeemInvite(RedeemInviteRequest request, CancellationToken cancellationToken) =>
        Ok(await dataStore.RedeemInviteAsync(request, cancellationToken));
}
