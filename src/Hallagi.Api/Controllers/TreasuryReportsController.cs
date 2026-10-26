using Hallagi.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hallagi.Api.Controllers;

public record OpenShiftDto(Guid BranchId, decimal OpeningCash);
public record CloseShiftDto(Guid BranchId, decimal CountedCash, string? Note);

[Route("api/treasury")]
public class TreasuryController(ShiftService shifts) : BaseApiController
{
    [HttpGet("state")]
    public async Task<IActionResult> State([FromQuery] Guid branchId, CancellationToken ct)
        => Ok(await shifts.GetStateAsync(CurrentUser, branchId, ct));

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenShiftDto dto, CancellationToken ct)
        => Ok(await shifts.OpenAsync(CurrentUser, dto.BranchId, dto.OpeningCash, ct));

    [HttpPost("close")]
    public async Task<IActionResult> Close([FromBody] CloseShiftDto dto, CancellationToken ct)
        => Ok(await shifts.CloseAsync(CurrentUser, dto.BranchId, dto.CountedCash, dto.Note, ct));

    [HttpGet("shifts")]
    public async Task<IActionResult> List([FromQuery] Guid? branchId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await shifts.ListAsync(CurrentUser, branchId, page, Math.Clamp(pageSize, 1, 100), ct));

    [HttpGet("shifts/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await shifts.GetOpenShiftStateAsync(id, ct));
}

[Route("api/reports")]
public class ReportsController(ReportsService reports) : BaseApiController
{
    [HttpGet("sales")]
    public async Task<IActionResult> Sales([FromQuery] Guid? branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? groupBy = "day", CancellationToken ct = default)
        => Ok(await reports.SalesReportAsync(CurrentUser, branchId,
            from ?? DateTime.Today.AddDays(-6), to ?? DateTime.Today.AddDays(1).AddTicks(-1), (groupBy ?? "day"), ct));

    [HttpGet("expenses")]
    public async Task<IActionResult> Expenses([FromQuery] Guid? branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken ct = default)
        => Ok(await reports.ExpensesByCategoryAsync(CurrentUser, branchId,
            from ?? DateTime.Today.AddDays(-6), to ?? DateTime.Today.AddDays(1).AddTicks(-1), ct));

    [HttpGet("barber-dues")]
    public async Task<IActionResult> BarberDues([FromQuery] Guid? branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken ct = default)
        => Ok(await reports.BarberDuesAsync(CurrentUser, branchId,
            from ?? DateTime.Today.AddDays(-6), to ?? DateTime.Today.AddDays(1), ct));

    [HttpGet("branches")]
    public async Task<IActionResult> Branches([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct = default)
        => Ok(await reports.BranchComparisonAsync(CurrentUser,
            from ?? DateTime.Today.AddDays(-6), to ?? DateTime.Today.AddDays(1).AddTicks(-1), ct));

    [HttpGet("tax")]
    public async Task<IActionResult> Tax([FromQuery] Guid? branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct = default)
        => Ok(await reports.TaxAndProductsAsync(CurrentUser, branchId,
            from ?? DateTime.Today.AddDays(-29), to ?? DateTime.Today.AddDays(1).AddTicks(-1), ct));

    [HttpGet("customers")]
    public async Task<IActionResult> Customers([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct = default)
        => Ok(await reports.CustomerRepeatAsync(CurrentUser,
            from ?? DateTime.Today.AddDays(-6), to ?? DateTime.Today.AddDays(1).AddTicks(-1), ct));
}
