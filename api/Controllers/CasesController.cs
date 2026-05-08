using api.Infrastructure;
using api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CasesController(DemoDataStore dataStore) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CaseSummary>>> GetCases(CancellationToken cancellationToken) =>
        Ok(await dataStore.GetCasesAsync(cancellationToken));

    [HttpPost("invite")]
    [Authorize(AuthenticationSchemes = "Entra")]
    public async Task<ActionResult<InviteRecord>> SendInvite(InviteRequest request, CancellationToken cancellationToken) =>
        Ok(await dataStore.CreateInviteAsync(request, cancellationToken));
}
