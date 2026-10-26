using Hallagi.Api.Domain;
using Hallagi.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hallagi.Api.Controllers;

[Route("api/products")]
public class ProductsController(ProductService products) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? branchId, [FromQuery] string? search, [FromQuery] bool lowStock = false, CancellationToken ct = default)
        => Ok(await products.ListAsync(CurrentUser, branchId, search, lowStock, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductInput input, CancellationToken ct)
        => Ok(new { id = await products.CreateAsync(CurrentUser, input, ct), message = "تمت إضافة المنتج بنجاح" });

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ProductInput input, CancellationToken ct)
    {
        await products.UpdateAsync(CurrentUser, id, input, ct);
        return Ok(new { message = "تم تحديث المنتج بنجاح" });
    }

    [HttpPost("{id:guid}/stock")]
    public async Task<IActionResult> Stock(Guid id, [FromBody] StockAdjustInput input, CancellationToken ct)
        => Ok(new { result = await products.AdjustStockAsync(CurrentUser, id, input, ct), message = "تم تحديث المخزون" });

    [HttpGet("{id:guid}/movements")]
    public async Task<IActionResult> Movements(Guid id, [FromQuery] Guid? branchId, CancellationToken ct)
        => Ok(await products.MovementsAsync(CurrentUser, id, branchId, ct));
}

[Route("api/coupons")]
public class CouponsController(CouponService coupons) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await coupons.ListAsync(CurrentUser, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CouponInput input, CancellationToken ct)
        => Ok(new { id = await coupons.CreateAsync(CurrentUser, input, ct), message = "تمت إضافة كود الخصم" });

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CouponInput input, CancellationToken ct)
    {
        await coupons.UpdateAsync(CurrentUser, id, input, ct);
        return Ok(new { message = "تم تحديث كود الخصم" });
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview([FromQuery] string code, [FromQuery] decimal gross, CancellationToken ct)
        => Ok(await coupons.PreviewAsync(CurrentUser, code, gross, ct));
}

public record LoyaltyAdjustDto(int Points, string? Note);

[Route("api/customers/{customerId:guid}/loyalty")]
public class LoyaltyController(LoyaltyService loyalty) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> History(Guid customerId, CancellationToken ct)
        => Ok(await loyalty.HistoryAsync(CurrentUser, customerId, ct));

    [HttpPost("adjust")]
    public async Task<IActionResult> Adjust(Guid customerId, [FromBody] LoyaltyAdjustDto dto, CancellationToken ct)
    {
        await loyalty.AdjustAsync(CurrentUser, customerId, dto.Points, dto.Note, ct);
        return Ok(new { message = "تم تعديل رصيد النقاط" });
    }
}

[Route("api/appointments")]
public class AppointmentsController(AppointmentService appointments) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Day([FromQuery] Guid branchId, [FromQuery] DateOnly? date, CancellationToken ct)
        => Ok(await appointments.DayAsync(CurrentUser, branchId, date, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await appointments.GetAsync(CurrentUser, id, ct));

    [HttpGet("slots")]
    public async Task<IActionResult> Slots([FromQuery] Guid branchId, [FromQuery] Guid? barberId, [FromQuery] DateOnly date,
        [FromQuery] List<Guid> serviceIds, [FromQuery] Guid? excludeId, CancellationToken ct)
        => Ok(await appointments.StaffSlotsAsync(CurrentUser, branchId, barberId, date, serviceIds, excludeId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AppointmentInput input, CancellationToken ct)
        => Ok(await appointments.CreateAsync(CurrentUser, input, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AppointmentInput input, CancellationToken ct)
    {
        await appointments.RescheduleAsync(CurrentUser, id, input, ct);
        return Ok(new { message = "تم تعديل الموعد" });
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, [FromBody] AppointmentStatusInput input, CancellationToken ct)
    {
        await appointments.SetStatusAsync(CurrentUser, id, input, ct);
        return Ok(new { message = AppointmentService.StatusText(input.Status) });
    }
}

public record PublicCancelDto(string Reference, string Phone);

/// <summary>Anonymous online booking endpoints (rate limited).</summary>
[ApiController]
[Route("api/public/{slug}")]
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("public")]
public class PublicBookingController(AppointmentService appointments) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Info(string slug, CancellationToken ct)
        => Ok(await appointments.PublicInfoAsync(slug, ct));

    [HttpGet("slots")]
    public async Task<IActionResult> Slots(string slug, [FromQuery] Guid branchId, [FromQuery] Guid? barberId, [FromQuery] DateOnly date,
        [FromQuery] List<Guid> serviceIds, CancellationToken ct)
        => Ok(await appointments.PublicSlotsAsync(slug, branchId, barberId, date, serviceIds, ct));

    [HttpPost("book")]
    public async Task<IActionResult> Book(string slug, [FromBody] PublicBookingInput input, CancellationToken ct)
        => Ok(await appointments.PublicBookAsync(slug, input, ct));

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(string slug, [FromBody] PublicCancelDto dto, CancellationToken ct)
    {
        await appointments.PublicCancelAsync(slug, dto.Reference, dto.Phone, ct);
        return Ok(new { message = "تم إلغاء الحجز" });
    }
}
