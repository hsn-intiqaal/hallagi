using Hallagi.Api.Data;
using Hallagi.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hallagi.Api.Services;

public record CreateSaleItem(Guid ServiceId, int Quantity);
public record CreateSaleRequest(
    Guid BranchId,
    Guid BarberId,
    Guid? CustomerId,
    PaymentMethod PaymentMethod,
    List<CreateSaleItem> Items,
    string? IdempotencyKey);

public class SalesService(AppDbContext db)
{
    public async Task<object> CreateAsync(CurrentUser user, CreateSaleRequest req, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SaleCreate))
            throw ApiException.Forbidden("ليس لديك صلاحية لتسجيل المبيعات");

        // Barber-operated POS: a barber may only record sales for himself
        if (user.Role == UserRole.Barber)
        {
            var linkedBarberId = await db.Barbers.AsNoTracking()
                .Where(b => b.LinkedProfileId == user.ProfileId && b.OrganizationId == user.OrganizationId)
                .Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
            if (linkedBarberId is null || req.BarberId != linkedBarberId.Value)
                throw ApiException.Forbidden("يمكن للحلاق تسجيل المبيعات لنفسه فقط");

            // A barber may sell at any branch he is assigned to
            var allowedBarberBranches = await db.BarberBranchAssignments.AsNoTracking()
                .Where(a => a.BarberId == linkedBarberId.Value).Select(a => a.BranchId).ToListAsync(ct);
            if (!allowedBarberBranches.Contains(req.BranchId))
                throw ApiException.Forbidden("لا تملك صلاحية على هذا الفرع");
        }
        else if (!Permissions.CanAccessBranch(user, req.BranchId))
        {
            throw ApiException.Forbidden("لا تملك صلاحية على هذا الفرع");
        }

        if (req.Items is null || req.Items.Count == 0)
            throw ApiException.Bad("empty_cart", "أضف خدمة واحدة على الأقل لإتمام البيع");
        if (req.Items.Any(i => i.Quantity <= 0 || i.Quantity > 50))
            throw ApiException.Bad("invalid_quantity", "الكمية غير صحيحة");
        if (req.PaymentMethod != PaymentMethod.Cash && req.PaymentMethod != PaymentMethod.Card)
            throw ApiException.Bad("invalid_payment", "طريقة الدفع غير مدعومة");

        var settings = await db.OrganizationSettings.FindAsync(new object[] { user.OrganizationId }, ct);
        if (req.PaymentMethod == PaymentMethod.Cash && settings is { EnableCash: false })
            throw ApiException.Bad("payment_disabled", "طريقة الدفع النقدي معطلة في إعدادات المنشأة");
        if (req.PaymentMethod == PaymentMethod.Card && settings is { EnableCard: false })
            throw ApiException.Bad("payment_disabled", "طريقة الدفع بالبطاقة معطلة في إعدادات المنشأة");

        // Idempotent replay: same key returns the same stored sale
        if (!string.IsNullOrWhiteSpace(req.IdempotencyKey))
        {
            var existing = await db.Sales
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.OrganizationId == user.OrganizationId && x.IdempotencyKey == req.IdempotencyKey, ct);
            if (existing is not null)
                return new { replayed = true, sale = await MapSaleAsync(existing, ct) };
        }

        var branch = await db.Branches.FirstOrDefaultAsync(x => x.Id == req.BranchId && x.OrganizationId == user.OrganizationId, ct)
            ?? throw ApiException.Bad("invalid_branch", "الفرع غير موجود");

        var barber = await db.Barbers.FirstOrDefaultAsync(x => x.Id == req.BarberId && x.OrganizationId == user.OrganizationId, ct)
            ?? throw ApiException.Bad("invalid_barber", "الحلاق غير موجود");
        if (barber.Status != EntityStatus.Active)
            throw ApiException.Bad("barber_inactive", "حساب الحلاق غير نشط");
        var barberWorksHere = await db.BarberBranchAssignments.AnyAsync(a => a.BarberId == barber.Id && a.BranchId == branch.Id, ct);
        if (!barberWorksHere)
            throw ApiException.Bad("barber_not_in_branch", "الحلاق غير مسند لهذا الفرع");

        Customer? customer = null;
        if (req.CustomerId is not null)
        {
            customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == req.CustomerId && x.OrganizationId == user.OrganizationId, ct)
                ?? throw ApiException.Bad("invalid_customer", "العميل غير موجود");
        }

        // Server-side pricing from the trusted source (branch override, else base price)
        var serviceIds = req.Items.Select(i => i.ServiceId).Distinct().ToList();
        var services = await db.Services.Where(s => s.OrganizationId == user.OrganizationId && serviceIds.Contains(s.Id)).ToListAsync(ct);
        if (services.Count != serviceIds.Count)
            throw ApiException.Bad("invalid_service", "إحدى الخدمات غير موجودة");
        if (services.Any(s => s.Status != EntityStatus.Active))
            throw ApiException.Bad("service_inactive", "إحدى الخدمات غير نشطة");

        var branchPrices = await db.BranchServicePrices
            .Where(p => p.BranchId == branch.Id && serviceIds.Contains(p.ServiceId))
            .ToDictionaryAsync(p => p.ServiceId, p => p.Price, ct);

        decimal total = 0m;
        var items = new List<SaleItem>();
        foreach (var reqItem in req.Items)
        {
            var svc = services.First(s => s.Id == reqItem.ServiceId);
            var unitPrice = branchPrices.TryGetValue(svc.Id, out var bp) ? bp : svc.BasePrice;
            var lineTotal = unitPrice * reqItem.Quantity;
            total += lineTotal;
            items.Add(new SaleItem
            {
                Id = Guid.NewGuid(),
                ServiceId = svc.Id,
                ServiceName = svc.Name,
                UnitPrice = unitPrice,
                Quantity = reqItem.Quantity,
                LineTotal = lineTotal
            });
        }

        var now = DateTime.UtcNow;

        // Transactional: sale + items + cash movement + shift state all commit together
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var receiptNo = await NextReceiptNoAsync(user.OrganizationId, ct);
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            BranchId = branch.Id,
            BarberId = barber.Id,
            CustomerId = customer?.Id,
            CreatedBy = user.ProfileId,
            ReceiptNo = receiptNo,
            PaymentMethod = req.PaymentMethod,
            Status = SaleStatus.Completed,
            TotalAmount = total,
            IdempotencyKey = string.IsNullOrWhiteSpace(req.IdempotencyKey) ? null : req.IdempotencyKey,
            CreatedAt = now,
            UpdatedAt = now,
            Items = items
        };
        items.ForEach(i => i.SaleId = sale.Id);
        db.Sales.Add(sale);

        Guid? shiftId = null;
        if (req.PaymentMethod == PaymentMethod.Cash)
        {
            var shift = await EnsureOpenShiftAsync(user, branch, ct);
            shiftId = shift.Id;
            db.CashMovements.Add(new CashMovement
            {
                Id = Guid.NewGuid(),
                OrganizationId = user.OrganizationId,
                BranchId = branch.Id,
                ShiftId = shift.Id,
                Type = CashMovementType.Sale,
                Amount = total,
                SaleId = sale.Id,
                Description = $"بيع نقدي #{receiptNo}",
                CreatedBy = user.ProfileId,
                CreatedAt = now
            });
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "sale.created",
            EntityType = nameof(Sale),
            EntityId = sale.Id.ToString(),
            Details = $"branch={branch.Name}; barber={barber.FullName}; total={total}; payment={req.PaymentMethod}",
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var saved = await db.Sales.Include(x => x.Items).FirstAsync(x => x.Id == sale.Id, ct);
        return new { replayed = false, sale = await MapSaleAsync(saved, ct), shiftId };
    }

    public async Task CancelAsync(CurrentUser user, Guid saleId, string reason, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SaleVoid))
            throw ApiException.Forbidden("ليس لديك صلاحية لإلغاء العمليات");

        var sale = await db.Sales.Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == saleId && x.OrganizationId == user.OrganizationId, ct)
            ?? throw ApiException.NotFound("العملية غير موجودة");

        if (sale.Status == SaleStatus.Cancelled)
            throw ApiException.Conflict("already_cancelled", "العملية ملغاة مسبقًا");

        if (!Permissions.CanAccessBranch(user, sale.BranchId))
            throw ApiException.Forbidden("لا تملك صلاحية على هذا الفرع");

        if (string.IsNullOrWhiteSpace(reason))
            throw ApiException.Bad("reason_required", "اكتب سبب الإلغاء");

        var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        sale.Status = SaleStatus.Cancelled;
        sale.CancelledAt = now;
        sale.CancelReason = reason.Trim();
        sale.CancelledBy = user.ProfileId;
        sale.UpdatedAt = now;

        // Reverse cash movement only, keeping the original sale intact
        if (sale.PaymentMethod == PaymentMethod.Cash)
        {
            var openShift = await db.Shifts.Where(s => s.BranchId == sale.BranchId && s.Status == ShiftStatus.Open)
                .OrderByDescending(s => s.OpenedAt).FirstOrDefaultAsync(ct);
            if (openShift is not null)
            {
                db.CashMovements.Add(new CashMovement
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = user.OrganizationId,
                    BranchId = sale.BranchId,
                    ShiftId = openShift.Id,
                    Type = CashMovementType.SaleVoid,
                    Amount = sale.TotalAmount,
                    SaleId = sale.Id,
                    Description = $"إلغاء بيع #{sale.ReceiptNo}: {reason.Trim()}",
                    CreatedBy = user.ProfileId,
                    CreatedAt = now
                });
            }
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "sale.cancelled",
            EntityType = nameof(Sale),
            EntityId = sale.Id.ToString(),
            Details = $"reason={reason.Trim()}; amount={sale.TotalAmount}",
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<object> GetAsync(CurrentUser user, Guid saleId, CancellationToken ct = default)
    {
        var sale = await db.Sales.Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == saleId && x.OrganizationId == user.OrganizationId, ct)
            ?? throw ApiException.NotFound("العملية غير موجودة");
        if (user.Role == UserRole.Barber)
        {
            var linkedBarberId = await db.Barbers.AsNoTracking()
                .Where(b => b.LinkedProfileId == user.ProfileId && b.OrganizationId == user.OrganizationId)
                .Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
            if (sale.BarberId != linkedBarberId)
                throw ApiException.Forbidden("لا تملك صلاحية على هذه العملية");
        }
        else if (!Permissions.CanAccessBranch(user, sale.BranchId))
        {
            throw ApiException.Forbidden("لا تملك صلاحية على هذا الفرع");
        }
        return await MapSaleAsync(sale, ct);
    }

    public async Task<object> ListAsync(CurrentUser user, Guid? branchId, DateTime? from, DateTime? to, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SaleView))
            throw ApiException.Forbidden("ليس لديك صلاحية لعرض المبيعات");

        Guid? linkedBarberId = null;
        if (user.Role == UserRole.Barber)
        {
            linkedBarberId = await db.Barbers.AsNoTracking()
                .Where(b => b.LinkedProfileId == user.ProfileId && b.OrganizationId == user.OrganizationId)
                .Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
        }

        IQueryable<Sale> query;
        if (linkedBarberId is not null)
        {
            // A barber sees only his own sales
            query = db.Sales.AsNoTracking().Where(x => x.BarberId == linkedBarberId.Value);
        }
        else
        {
            var allowed = Permissions.AccessesAllBranches(user)
                ? db.Branches.Where(x => x.OrganizationId == user.OrganizationId).Select(x => x.Id)
                : user.BranchIds.AsQueryable();
            query = db.Sales.AsNoTracking()
                .Where(x => x.OrganizationId == user.OrganizationId && allowed.Contains(x.BranchId));
        }

        if (branchId.HasValue)
        {
            if (!Permissions.CanAccessBranch(user, branchId.Value))
                throw ApiException.Forbidden("لا تملك صلاحية على هذا الفرع");
            query = query.Where(x => x.BranchId == branchId.Value);
        }
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.CreatedAt <= to.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x =>
                x.ReceiptNo.ToString() == s ||
                (x.CustomerId != null && (
                    db.Customers.Any(c => c.Id == x.CustomerId && c.FullName.Contains(s)) ||
                    db.Customers.Any(c => c.Id == x.CustomerId && c.Phone != null && c.Phone.Contains(s)))));
        }

        var totalCount = await query.CountAsync(ct);
        var sales = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.ReceiptNo,
                x.CreatedAt,
                BranchName = db.Branches.Where(b => b.Id == x.BranchId).Select(b => b.Name).First(),
                BarberName = db.Barbers.Where(b => b.Id == x.BarberId).Select(b => b.FullName).First(),
                CustomerName = x.CustomerId != null
                    ? db.Customers.Where(c => c.Id == x.CustomerId).Select(c => c.FullName).First()
                    : "عميل نقدي / بدون تسجيل",
                x.PaymentMethod,
                PaymentText = x.PaymentMethod == PaymentMethod.Cash ? "نقدي" : "بطاقة",
                x.Status,
                StatusText = x.Status == SaleStatus.Completed ? "مكتملة" : "ملغاة",
                x.TotalAmount,
                ServicesText = string.Join(" + ", x.Items.Select(i => i.ServiceName + (i.Quantity > 1 ? $" ×{i.Quantity}" : "")))
            }).ToListAsync(ct);

        return new { totalCount, page, pageSize, items = sales };
    }

    private async Task<object> MapSaleAsync(Sale sale, CancellationToken ct)
    {
        var branchName = await db.Branches.Where(b => b.Id == sale.BranchId).Select(b => b.Name).FirstAsync(ct);
        var barberName = await db.Barbers.Where(b => b.Id == sale.BarberId).Select(b => b.FullName).FirstAsync(ct);
        var customerName = sale.CustomerId is null
            ? "عميل نقدي / بدون تسجيل"
            : await db.Customers.Where(c => c.Id == sale.CustomerId).Select(c => c.FullName).FirstAsync(ct);
        var customerPhone = sale.CustomerId is null
            ? null
            : await db.Customers.Where(c => c.Id == sale.CustomerId).Select(c => c.Phone).FirstAsync(ct);

        return new
        {
            sale.Id,
            sale.ReceiptNo,
            sale.CreatedAt,
            sale.BranchId,
            BranchName = branchName,
            sale.BarberId,
            BarberName = barberName,
            sale.CustomerId,
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            sale.PaymentMethod,
            PaymentText = sale.PaymentMethod == PaymentMethod.Cash ? "نقدي" : "بطاقة",
            sale.Status,
            StatusText = sale.Status == SaleStatus.Completed ? "مكتملة" : "ملغاة",
            sale.TotalAmount,
            Items = sale.Items.Select(i => new
            {
                i.ServiceId,
                i.ServiceName,
                i.UnitPrice,
                i.Quantity,
                i.LineTotal
            }).ToList()
        };
    }

    private async Task<int> NextReceiptNoAsync(Guid orgId, CancellationToken ct)
    {
        var max = await db.Sales.Where(x => x.OrganizationId == orgId).MaxAsync(x => (int?)x.ReceiptNo, ct) ?? 1000;
        return max + 1;
    }

    /// <summary>Ensures an open shift for cash sales; auto-opens with the drawer's known balance.</summary>
    private async Task<Shift> EnsureOpenShiftAsync(CurrentUser user, Branch branch, CancellationToken ct)
    {
        var open = await db.Shifts.FirstOrDefaultAsync(s => s.BranchId == branch.Id && s.Status == ShiftStatus.Open, ct);
        if (open is not null) return open;

        var drawer = await db.CashDrawers.FirstOrDefaultAsync(d => d.BranchId == branch.Id, ct);
        if (drawer is null)
        {
            drawer = new CashDrawer { Id = Guid.NewGuid(), OrganizationId = user.OrganizationId, BranchId = branch.Id, CreatedAt = DateTime.UtcNow };
            db.CashDrawers.Add(drawer);
            await db.SaveChangesAsync(ct);
        }

        // Opening amount = last closed shift's counted cash (money assumed still in drawer), else 0
        var lastCounted = await db.Shifts
            .Where(s => s.BranchId == branch.Id && s.Status == ShiftStatus.Closed)
            .OrderByDescending(s => s.ClosedAt)
            .Select(s => s.CountedCash)
            .FirstOrDefaultAsync(ct) ?? 0m;

        var now = DateTime.UtcNow;
        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            BranchId = branch.Id,
            DrawerId = drawer.Id,
            OpenedBy = user.ProfileId,
            OpeningCash = lastCounted,
            OpenedAt = now,
            Status = ShiftStatus.Open,
            AutoOpened = true
        };
        db.Shifts.Add(shift);
        await db.SaveChangesAsync(ct);
        return shift;
    }
}
