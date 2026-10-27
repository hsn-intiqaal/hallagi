using Microsoft.AspNetCore.Mvc;

namespace Hallagi.Api.Controllers;

public record CreateSaleDto(
    Guid BranchId,
    Guid BarberId,
    Guid? CustomerId,
    PaymentMethod PaymentMethod,
    List<CreateSaleItem> Items,
    string? IdempotencyKey,
    string? CouponCode = null,
    decimal? ManualDiscount = null,
    int RedeemPoints = 0,
    Guid? AppointmentId = null);

public record CancelSaleDto(string Reason);

[Route("api/[controller]")]
public class SalesController(SalesService sales) : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSaleDto dto, CancellationToken ct)
    {
        var req = new CreateSaleRequest(dto.BranchId, dto.BarberId, dto.CustomerId, dto.PaymentMethod, dto.Items, dto.IdempotencyKey,
            dto.CouponCode, dto.ManualDiscount, dto.RedeemPoints, dto.AppointmentId);
        var result = await sales.CreateAsync(CurrentUser, req, ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? branchId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await sales.ListAsync(CurrentUser, branchId, from, to, search, page, Math.Clamp(pageSize, 1, 100), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await sales.GetAsync(CurrentUser, id, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelSaleDto dto, CancellationToken ct)
    {
        await sales.CancelAsync(CurrentUser, id, dto.Reason, ct);
        return Ok(new { message = "تم إلغاء العملية وحفظ سبب الإلغاء" });
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv([FromServices] ReportsService reports,
        [FromQuery] Guid? branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct = default)
    {
        var csv = await reports.SalesCsvAsync(CurrentUser, branchId, from, to, ct);
        return File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray(),
            "text/csv; charset=utf-8", "sales.csv");
    }
}

/// <summary>POS bootstrap data: active services with prices, barbers, recent customers, payment settings.</summary>
[Route("api/pos")]
public class PosController(PosService pos) : BaseApiController
{
    [HttpGet("data")]
    public async Task<IActionResult> Data([FromQuery] Guid branchId, CancellationToken ct)
        => Ok(await pos.GetDataAsync(CurrentUser, branchId, ct));
}
