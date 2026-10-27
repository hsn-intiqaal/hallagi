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

/// <summary>
/// Management reports. <c>from</c>/<c>to</c> are the organization's local calendar days (inclusive);
/// any time part is ignored. Missing bounds default to the last 7 (or 30) days.
/// </summary>
[Route("api/reports")]
public class ReportsController(ReportsService reports) : BaseApiController
{
    [HttpGet("sales")]
    public async Task<IActionResult> Sales([FromQuery] Guid? branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await reports.SalesReportAsync(CurrentUser, branchId, from, to, ct));

    [HttpGet("expenses")]
    public async Task<IActionResult> Expenses([FromQuery] Guid? branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await reports.ExpensesByCategoryAsync(CurrentUser, branchId, from, to, ct));

    [HttpGet("barber-dues")]
    public async Task<IActionResult> BarberDues([FromQuery] Guid? branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await reports.BarberDuesAsync(CurrentUser, branchId, from, to, ct));

    [HttpGet("branches")]
    public async Task<IActionResult> Branches([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await reports.BranchComparisonAsync(CurrentUser, from, to, ct));

    [HttpGet("customers")]
    public async Task<IActionResult> Customers([FromQuery] Guid? branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await reports.CustomersAsync(CurrentUser, branchId, from, to, ct));

    [HttpGet("tax")]
    public async Task<IActionResult> Tax([FromQuery] Guid? branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await reports.TaxAndProductsAsync(CurrentUser, branchId, from, to, ct));
}
