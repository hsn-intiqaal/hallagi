using Microsoft.AspNetCore.Mvc;

namespace Hallagi.Api.Controllers;

[Route("api/subscription")]
public class SubscriptionController(SubscriptionService subscriptions) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Current(CancellationToken ct)
    {
        if (!Permissions.Can(CurrentUser, Permissions.SubscriptionView))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض الاشتراك");
        return Ok(await subscriptions.GetCurrentAsync(CurrentUser.OrganizationId, ct));
    }

    [HttpGet("plans")]
    public async Task<IActionResult> Plans(CancellationToken ct)
        => Ok(await subscriptions.ListPlansAsync(ct));

    [HttpPost("change-plan")]
    public async Task<IActionResult> ChangePlan([FromBody] ChangePlanDto dto, CancellationToken ct)
    {
        await subscriptions.ChangePlanAsync(CurrentUser.OrganizationId, dto.PlanId, CurrentUser, ct);
        return Ok(new { message = "تم تغيير الخطة بنجاح" });
    }
}

public record ChangePlanDto(Guid PlanId);

[Route("api/devices")]
public class DevicesController(DeviceService devices) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? branchId, CancellationToken ct)
        => Ok(await devices.ListAsync(CurrentUser, branchId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDeviceRequest req, CancellationToken ct)
        => Ok(new { id = await devices.CreateAsync(CurrentUser, req, ct), message = "تمت إضافة الجهاز بنجاح" });

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, [FromBody] SetStatusDto dto, CancellationToken ct)
    {
        await devices.SetStatusAsync(CurrentUser, id, dto.Active, ct);
        return Ok(new { message = dto.Active ? "تم تفعيل الجهاز" : "تم تعطيل الجهاز" });
    }
}

[Route("api/ledger")]
public class LedgerController(LedgerService ledger) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid barberId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await ledger.ListLedgerAsync(CurrentUser, barberId, from, to, ct));

    [HttpPost("entries")]
    public async Task<IActionResult> AddEntry([FromBody] LedgerEntryRequest req, CancellationToken ct)
        => Ok(new { id = await ledger.AddEntryAsync(CurrentUser, req, ct), message = "تم تسجيل الحركة المالية" });

    [HttpPost("entries/{id:guid}/reverse")]
    public async Task<IActionResult> Reverse(Guid id, [FromBody] ReasonDto dto, CancellationToken ct)
    {
        await ledger.ReverseAsync(CurrentUser, id, dto.Reason, ct);
        return Ok(new { message = "تم تسجيل التصحيح في السجل" });
    }

    [HttpGet("advances")]
    public async Task<IActionResult> Advances([FromQuery] Guid? barberId, CancellationToken ct)
        => Ok(await ledger.ListAdvancesAsync(CurrentUser, barberId, ct));

    [HttpPost("advances")]
    public async Task<IActionResult> CreateAdvance([FromBody] CreateAdvanceRequest req, CancellationToken ct)
        => Ok(new { id = await ledger.CreateAdvanceAsync(CurrentUser, req, ct), message = "تم تسجيل السلفة" });

    [HttpPost("advances/{id:guid}/repay")]
    public async Task<IActionResult> RepayAdvance(Guid id, [FromBody] AmountDto dto, CancellationToken ct)
    {
        await ledger.RepayAdvanceAsync(CurrentUser, id, dto.Amount, ct);
        return Ok(new { message = "تم تسجيل سداد السلفة" });
    }

    [HttpGet("handovers")]
    public async Task<IActionResult> Handovers([FromQuery] Guid? branchId, CancellationToken ct)
        => Ok(await ledger.ListHandoversAsync(CurrentUser, branchId, ct));

    [HttpPost("handovers")]
    public async Task<IActionResult> CreateHandover([FromBody] CreateHandoverRequest req, CancellationToken ct)
        => Ok(new { id = await ledger.CreateHandoverAsync(CurrentUser, req, ct), message = "تم تسجيل التسليم النقدي" });
}

public record ReasonDto(string Reason);
public record AmountDto(decimal Amount);

[Route("api/settlements")]
public class SettlementsController(SettlementService settlements) : BaseApiController
{
    [HttpGet("periods")]
    public async Task<IActionResult> Periods(CancellationToken ct)
        => Ok(await settlements.ListPeriodsAsync(CurrentUser, ct));

    [HttpPost("periods")]
    public async Task<IActionResult> CreatePeriod([FromBody] CreatePeriodDto dto, CancellationToken ct)
    {
        var id = await settlements.CreatePeriodAsync(CurrentUser, dto.Name, dto.StartDate, dto.EndDate, ct);
        return Ok(new { id, message = "تم إنشاء الفترة المالية" });
    }

    [HttpPost("periods/{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, [FromBody] PreviewDto? dto, CancellationToken ct)
        => Ok(await settlements.PreviewAsync(CurrentUser, id, dto?.BarberIds, ct));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? periodId, [FromQuery] string? status, CancellationToken ct)
        => Ok(await settlements.ListAsync(CurrentUser, periodId, status, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await settlements.GetAsync(CurrentUser, id, ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        await settlements.ApproveAsync(CurrentUser, id, ct);
        return Ok(new { message = "تم اعتماد التسوية وقفلها" });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] ReasonDto dto, CancellationToken ct)
    {
        await settlements.CancelAsync(CurrentUser, id, dto.Reason, ct);
        return Ok(new { message = "تم إلغاء التسوية" });
    }

    [HttpPost("{id:guid}/pay")]
    public async Task<IActionResult> Pay(Guid id, [FromBody] PayDto dto, CancellationToken ct)
    {
        await settlements.PayAsync(CurrentUser, id, dto.Amount, dto.Method, dto.PaymentDate, dto.Note, ct);
        return Ok(new { message = "تم تسجيل الدفعة" });
    }
}

public record CreatePeriodDto(string Name, DateOnly StartDate, DateOnly EndDate);
public record PreviewDto(List<Guid>? BarberIds);
public record PayDto(decimal Amount, string? Method, DateOnly? PaymentDate, string? Note);
