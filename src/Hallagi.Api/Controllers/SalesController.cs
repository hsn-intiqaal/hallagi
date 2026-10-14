using Hallagi.Api.Data;
using Hallagi.Api.Domain;
using Hallagi.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hallagi.Api.Controllers;

public record CreateSaleDto(
    Guid BranchId,
    Guid BarberId,
    Guid? CustomerId,
    PaymentMethod PaymentMethod,
    List<CreateSaleItem> Items,
    string? IdempotencyKey);

public record CancelSaleDto(string Reason);

[Route("api/[controller]")]
public class SalesController(SalesService sales) : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSaleDto dto, CancellationToken ct)
    {
        var req = new CreateSaleRequest(dto.BranchId, dto.BarberId, dto.CustomerId, dto.PaymentMethod, dto.Items, dto.IdempotencyKey);
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
        if (!Permissions.Can(CurrentUser, Permissions.ReportView))
            throw ApiException.Forbidden("ليس لديك صلاحية لتصدير التقارير");
        var csv = await reports.SalesCsvAsync(CurrentUser, branchId,
            from ?? DateTime.Today.AddDays(-30), to ?? DateTime.Today.AddDays(1), ct);
        return File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray(),
            "text/csv; charset=utf-8", "sales.csv");
    }
}

/// <summary>POS bootstrap data: active services with prices, barbers, recent customers, payment settings.</summary>
[Route("api/pos")]
public class PosController(AppDbContext db) : BaseApiController
{
    [HttpGet("data")]
    public async Task<IActionResult> Data([FromQuery] Guid branchId, CancellationToken ct)
    {
        var user = CurrentUser;
        if (user.Role == UserRole.Barber)
        {
            // a barber operates at his assigned branches only
            var barberBranches = await db.BarberBranchAssignments.AsNoTracking()
                .Where(a => db.Barbers.Any(b => b.Id == a.BarberId && b.LinkedProfileId == user.ProfileId && b.OrganizationId == user.OrganizationId))
                .Select(a => a.BranchId).ToListAsync(ct);
            if (!barberBranches.Contains(branchId))
                throw ApiException.Forbidden("لا تملك صلاحية على هذا الفرع");
        }
        else if (!Permissions.CanAccessBranch(user, branchId))
        {
            throw ApiException.Forbidden("لا تملك صلاحية على هذا الفرع");
        }

        var settings = await db.OrganizationSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrganizationId == user.OrganizationId, ct);

        var services = await db.Services.AsNoTracking()
            .Where(s => s.OrganizationId == user.OrganizationId && s.Status == EntityStatus.Active)
            .OrderBy(s => s.Name).ToListAsync(ct);

        var branchPrices = await db.BranchServicePrices.AsNoTracking()
            .Where(p => p.BranchId == branchId && services.Select(s => s.Id).Contains(p.ServiceId))
            .ToDictionaryAsync(p => p.ServiceId, p => p.Price, ct);

        var barbers = await db.Barbers.AsNoTracking()
            .Where(b => b.OrganizationId == user.OrganizationId && b.Status == EntityStatus.Active
                        && b.BranchAssignments.Any(a => a.BranchId == branchId))
            .OrderBy(b => b.FullName)
            .Select(b => new
            {
                b.Id,
                b.FullName,
                Initials = b.FullName.Substring(0, 1),
                BranchNames = b.BranchAssignments.Where(a => a.BranchId == branchId)
                    .Select(a => db.Branches.Where(br => br.Id == a.BranchId).Select(br => br.Name).First()).ToList()
            }).ToListAsync(ct);

        var customers = await db.Customers.AsNoTracking()
            .Where(c => c.OrganizationId == user.OrganizationId)
            .OrderBy(c => c.FullName).Take(50)
            .Select(c => new { c.Id, c.FullName, c.Phone }).ToListAsync(ct);

        return Ok(new
        {
            branch = await db.Branches.Where(b => b.Id == branchId).Select(b => new { b.Id, b.Name, b.City }).FirstAsync(ct),
            services = services.Select(s => new
            {
                s.Id,
                s.Name,
                s.Description,
                Price = branchPrices.TryGetValue(s.Id, out var p) ? p : s.BasePrice
            }),
            barbers,
            customers,
            payment = new
            {
                cashEnabled = settings?.EnableCash ?? true,
                cardEnabled = settings?.EnableCard ?? true,
                methods = new[]
                {
                    new { value = PaymentMethod.Cash.ToString(), label = "نقدي", enabled = settings?.EnableCash ?? true },
                    new { value = PaymentMethod.Card.ToString(), label = "بطاقة", enabled = settings?.EnableCard ?? true }
                }
            }
        });
    }
}
