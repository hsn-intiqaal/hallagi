using Microsoft.AspNetCore.Mvc;

namespace Hallagi.Api.Controllers;

[Route("api/[controller]")]
public class DashboardController(DashboardService dashboard) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid? branchId, [FromQuery] string? period, CancellationToken ct)
        => Ok(await dashboard.GetAsync(CurrentUser, branchId, period ?? "today", ct));
}

[Route("api/expenses")]
public class ExpensesController(ExpenseService expenses) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await expenses.ListAsync(CurrentUser, branchId, from, to, page, Math.Clamp(pageSize, 1, 100), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExpenseRequest req, CancellationToken ct)
    {
        var id = await expenses.CreateAsync(CurrentUser, req, ct);
        return Ok(new { id, message = "تم تسجيل المصروف بنجاح" });
    }

    [HttpPost("{id:guid}/void")]
    public async Task<IActionResult> Void(Guid id, [FromBody] CancelRequest req, CancellationToken ct)
    {
        await expenses.VoidAsync(CurrentUser, id, req.Reason, ct);
        return Ok(new { message = "تم إلغاء المصروف وحفظ التصحيح في سجل التدقيق" });
    }
}

public record CancelRequest(string Reason);
