using Hallagi.Application.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Hallagi.Api.Controllers;

[Route("api/me")]
public class MeController(SessionService session) : BaseApiController
{
    /// <summary>Role-aware summary used right after login to pick the landing page.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct = default)
        => Ok(await session.GetSummaryAsync(CurrentUser, ct));
}
