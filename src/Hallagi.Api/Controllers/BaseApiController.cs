using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hallagi.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    private ICurrentUserAccessor? _accessor;
    protected CurrentUser CurrentUser =>
        (_accessor ??= HttpContext.RequestServices.GetRequiredService<ICurrentUserAccessor>()).User;
}
