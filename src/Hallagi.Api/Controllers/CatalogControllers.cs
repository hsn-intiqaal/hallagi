using Microsoft.AspNetCore.Mvc;

namespace Hallagi.Api.Controllers;

[Route("api/services")]
public class ServicesController(CatalogService catalog) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? branchId, [FromQuery] string? search, CancellationToken ct = default)
        => Ok(await catalog.ListServicesAsync(CurrentUser, branchId, search, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ServiceInput input, CancellationToken ct)
        => Ok(new { id = await catalog.CreateServiceAsync(CurrentUser, input, ct), message = "تمت إضافة الخدمة بنجاح" });

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ServiceInput input, CancellationToken ct)
    {
        await catalog.UpdateServiceAsync(CurrentUser, id, input, ct);
        return Ok(new { message = "تم تحديث الخدمة بنجاح" });
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, [FromBody] SetStatusDto dto, CancellationToken ct)
    {
        await catalog.SetServiceStatusAsync(CurrentUser, id, dto.Active, ct);
        return Ok(new { message = dto.Active ? "تم تنشيط الخدمة" : "تم إيقاف الخدمة في عمليات البيع الجديدة" });
    }

    [HttpPost("{id:guid}/branch-price")]
    public async Task<IActionResult> BranchPrice(Guid id, [FromBody] BranchPriceDto dto, CancellationToken ct)
    {
        await catalog.SetBranchPriceAsync(CurrentUser, id, dto.BranchId, dto.Price, ct);
        return Ok(new { message = "تم تحديث سعر الفرع" });
    }
}

public record SetStatusDto(bool Active);
public record BranchPriceDto(Guid BranchId, decimal? Price);

[Route("api/barbers")]
public class BarbersController(CatalogService catalog) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct = default)
        => Ok(await catalog.ListBarbersAsync(CurrentUser, branchId, from, to, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct = default)
        => Ok(await catalog.GetBarberAsync(CurrentUser, id, from, to, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BarberInput input, CancellationToken ct)
        => Ok(new { id = await catalog.CreateBarberAsync(CurrentUser, input, ct), message = "تمت إضافة الحلاق بنجاح" });

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] BarberInput input, CancellationToken ct)
    {
        await catalog.UpdateBarberAsync(CurrentUser, id, input, ct);
        return Ok(new { message = "تم تحديث بيانات الحلاق بنجاح" });
    }
}

[Route("api/customers")]
public class CustomersController(CatalogService catalog) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await catalog.ListCustomersAsync(CurrentUser, search, page, Math.Clamp(pageSize, 1, 100), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
        => Ok(await catalog.GetCustomerAsync(CurrentUser, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomerInput input, CancellationToken ct)
    {
        var id = await catalog.CreateCustomerAsync(CurrentUser, input, ct);
        return Ok(new { id, message = "تمت إضافة العميل بنجاح" });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CustomerInput input, CancellationToken ct)
    {
        await catalog.UpdateCustomerAsync(CurrentUser, id, input, ct);
        return Ok(new { message = "تم تحديث بيانات العميل بنجاح" });
    }
}
