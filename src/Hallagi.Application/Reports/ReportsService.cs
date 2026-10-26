using Hallagi.Application.Expenses;
using Hallagi.Application.Catalog;
using System.Globalization;
using System.Text;

namespace Hallagi.Application.Reports;

public class ReportsService(IAppDbContext db)
{
    public async Task<object> SalesReportAsync(CurrentUser user, Guid? branchId, DateTime from, DateTime to, string groupBy, CancellationToken ct = default)
    {
        var branchIds = await ScopeAsync(user, branchId, ct);
        var sales = db.Sales.AsNoTracking().Where(x =>
            x.OrganizationId == user.OrganizationId && branchIds.Contains(x.BranchId)
            && x.Status == SaleStatus.Completed && x.CreatedAt >= from && x.CreatedAt <= to);

        var totalSales = await sales.SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0m;
        var cashSales = await sales.Where(x => x.PaymentMethod == PaymentMethod.Cash).SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0m;
        var cardSales = await sales.Where(x => x.PaymentMethod == PaymentMethod.Card).SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0m;
        var count = await sales.CountAsync(ct);

        var fromExpenses = DateOnly.FromDateTime(from.Date);
        var toExpenses = DateOnly.FromDateTime(to.Date);
        var expenses = await db.Expenses.AsNoTracking()
            .Where(x => x.OrganizationId == user.OrganizationId && branchIds.Contains(x.BranchId)
                        && x.Status == ExpenseStatus.Active
                        && x.ExpenseDate >= fromExpenses && x.ExpenseDate <= toExpenses)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

        List<object> groups = groupBy?.ToLowerInvariant() switch
        {
            "day" => await GroupDayAsync(sales, ct),
            "service" => await GroupServiceAsync(user, branchIds, from, to, ct),
            "barber" => await GroupBarberAsync(sales, ct),
            _ => await GroupDayAsync(sales, ct)
        };

        return new
        {
            from, to,
            summary = new
            {
                totalSales,
                cashSales,
                cardSales,
                salesCount = count,
                expenses,
                operatingResult = totalSales - expenses,
                hint = "النتيجة التقديرية من السجلات المسجلة فقط، ولا تُعتبر صافي ربح محاسبي"
            },
            groups,
            byService = await GroupServiceAsync(user, branchIds, from, to, ct)
        };
    }

    private async Task<List<object>> GroupDayAsync(IQueryable<Sale> sales, CancellationToken ct)
    {
        var rows = await sales.GroupBy(x => x.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Total = g.Sum(x => x.TotalAmount), Count = g.Count() })
            .OrderBy(x => x.Day).ToListAsync(ct);
        return rows.Select(r => (object)new { label = r.Day.ToString("yyyy-MM-dd"), r.Total, r.Count }).ToList();
    }

    private async Task<List<object>> GroupServiceAsync(CurrentUser user, List<Guid> branchIds, DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await db.SaleItems.AsNoTracking()
            .Where(i => db.Sales.Any(s => s.Id == i.SaleId && s.OrganizationId == user.OrganizationId
                       && branchIds.Contains(s.BranchId) && s.Status == SaleStatus.Completed
                       && s.CreatedAt >= from && s.CreatedAt <= to))
            .GroupBy(i => i.ServiceName)
            .Select(g => new { label = g.Key, Total = g.Sum(x => x.LineTotal), Count = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Total).ToListAsync(ct);
        return rows.Select(r => (object)new { r.label, r.Total, r.Count }).ToList();
    }

    private async Task<List<object>> GroupBarberAsync(IQueryable<Sale> sales, CancellationToken ct)
    {
        var rows = await sales.GroupBy(x => x.BarberId)
            .Select(g => new { BarberId = g.Key, Total = g.Sum(x => x.TotalAmount), Count = g.Count() })
            .OrderByDescending(x => x.Total).ToListAsync(ct);
        var names = await db.Barbers.AsNoTracking()
            .Where(b => rows.Select(r => r.BarberId).Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.FullName, ct);
        return rows.Select(r => (object)new { label = names.GetValueOrDefault(r.BarberId, "-"), r.Total, r.Count }).ToList();
    }

    public async Task<object> ExpensesByCategoryAsync(CurrentUser user, Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var branchIds = await ScopeAsync(user, branchId, ct);
        var rows = await db.Expenses.AsNoTracking()
            .Where(x => x.OrganizationId == user.OrganizationId && branchIds.Contains(x.BranchId)
                        && x.Status == ExpenseStatus.Active
                        && x.ExpenseDate >= DateOnly.FromDateTime(from.Date)
                        && x.ExpenseDate <= DateOnly.FromDateTime(to.Date))
            .GroupBy(x => x.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Amount), Count = g.Count() })
            .OrderByDescending(x => x.Total).ToListAsync(ct);

        var total = rows.Sum(r => r.Total);
        return new
        {
            from, to,
            total,
            items = rows.Select(r => new { Category = r.Category.ToString(), CategoryText = ExpenseService.CategoryText(r.Category), r.Total, r.Count, Share = total > 0 ? Math.Round(r.Total / total * 100, 1) : 0m })
        };
    }

    public async Task<object> BarberDuesAsync(CurrentUser user, Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        if (!Permissions.Can(user, Permissions.BarberDuesView))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض مستحقات الحلاقين");

        var branchIds = await ScopeAsync(user, branchId, ct);
        var barbers = await db.Barbers.AsNoTracking().Where(b => b.OrganizationId == user.OrganizationId).ToListAsync(ct);

        var items = new List<object>();
        foreach (var b in barbers)
        {
            var comp = await db.BarberCompensations.AsNoTracking()
                .Where(c => c.BarberId == b.Id && c.Status == EntityStatus.Active)
                .OrderByDescending(c => c.EffectiveFrom).FirstOrDefaultAsync(ct);

            var eligible = await db.Sales.AsNoTracking()
                .Where(s => s.BarberId == b.Id && s.OrganizationId == user.OrganizationId
                            && branchIds.Contains(s.BranchId) && s.Status == SaleStatus.Completed
                            && s.CreatedAt >= from && s.CreatedAt <= to)
                .SumAsync(s => (decimal?)s.CommissionBase, ct) ?? 0m;

            var commission = comp?.CommissionRate is > 0 ? Math.Round(eligible * comp.CommissionRate.Value, 2) : 0m;
            var assignedIds = b.BranchAssignments.Select(a => a.BranchId).ToList();
            var assigned = await db.Branches.Where(br => assignedIds.Contains(br.Id)).Select(br => br.Name).ToListAsync(ct);

            items.Add(new
            {
                barberId = b.Id,
                barberName = b.FullName,
                branchNames = assigned,
                eligibleSales = eligible,
                compensationType = comp?.Type.ToString(),
                compensationTypeText = comp is null ? "غير محدد" : CatalogService.CompensationTypeText(comp.Type),
                monthlySalary = comp?.MonthlySalary,
                commissionRate = comp?.CommissionRate,
                commissionText = comp?.CommissionRate.HasValue == true ? $"{comp.CommissionRate:P0}" : null,
                estimatedCommission = commission,
                chairRent = comp?.ChairRentAmount,
                note = comp?.Notes,
                explanation = comp?.CommissionRate is > 0
                    ? $"مبيعات الخدمات المؤهلة: {eligible:0.00} ر.س. × العمولة {comp.CommissionRate:P0} = العمولة التقديرية {commission:0.00} ر.س."
                    : comp is not null && comp.Type == CompensationType.FixedSalary
                        ? "الراتب ثابت بقيمة اتفاقية مستقلة عن المبيعات"
                        : comp is not null && comp.Type == CompensationType.ChairRental
                            ? "اتفاق تأجير كرسي مستقل عن الراتب والعمولة"
                            : null
            });
        }

        return new { from, to, items };
    }

    public async Task<object> BranchComparisonAsync(CurrentUser user, DateTime from, DateTime to, CancellationToken ct)
    {
        var branches = await db.Branches.AsNoTracking()
            .Where(b => b.OrganizationId == user.OrganizationId && b.Status == EntityStatus.Active).ToListAsync(ct);

        var items = new List<object>();
        foreach (var b in branches)
        {
            if (!Permissions.CanAccessBranch(user, b.Id)) continue;

            var sales = db.Sales.AsNoTracking().Where(x => x.BranchId == b.Id
                && x.OrganizationId == user.OrganizationId && x.Status == SaleStatus.Completed
                && x.CreatedAt >= from && x.CreatedAt <= to);
            var total = await sales.SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0m;
            var count = await sales.CountAsync(ct);

            var expenses = await db.Expenses.AsNoTracking()
                .Where(x => x.BranchId == b.Id && x.Status == ExpenseStatus.Active
                            && x.ExpenseDate >= DateOnly.FromDateTime(from.Date)
                            && x.ExpenseDate <= DateOnly.FromDateTime(to.Date))
                .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

            items.Add(new
            {
                branchId = b.Id,
                branchName = b.Name,
                city = b.City,
                totalSales = total,
                salesCount = count,
                expenses,
                operatingResult = total - expenses
            });
        }

        return new { from, to, items };
    }

    public async Task<object> CustomerRepeatAsync(CurrentUser user, DateTime from, DateTime to, CancellationToken ct)
    {
        var branchIds = await ScopeAsync(user, null, ct);
        var fromDate = from.Date;
        var toDate = to.Date;

        var rows = await db.Sales.AsNoTracking()
            .Where(s => s.OrganizationId == user.OrganizationId && branchIds.Contains(s.BranchId)
                        && s.Status == SaleStatus.Completed && s.CustomerId != null
                        && s.CreatedAt >= fromDate && s.CreatedAt <= toDate)
            .GroupBy(s => s.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, Visits = g.Count(), Total = g.Sum(x => x.TotalAmount) })
            .ToListAsync(ct);

        var names = await db.Customers.AsNoTracking()
            .Where(c => rows.Select(r => r.CustomerId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.FullName, ct);

        return new
        {
            from, to,
            repeatCustomers = rows.Count(r => r.Visits > 1),
            items = rows.OrderByDescending(r => r.Visits).Select(r => new
            {
                r.CustomerId,
                customerName = names.TryGetValue(r.CustomerId, out var n) ? n : "-",
                r.Visits,
                r.Total
            }).Take(100)
        };
    }

    public async Task<List<Guid>> ScopeAsync(CurrentUser user, Guid? branchId, CancellationToken ct)
    {
        if (branchId.HasValue)
        {
            if (!Permissions.CanAccessBranch(user, branchId.Value))
                throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
            return new List<Guid> { branchId.Value };
        }
        if (Permissions.AccessesAllBranches(user))
            return await db.Branches.Where(b => b.OrganizationId == user.OrganizationId && b.Status == EntityStatus.Active)
                .Select(b => b.Id).ToListAsync(ct);
        var ids = user.BranchIds.ToList();
        if (ids.Count == 0) ids.Add(Guid.Empty);
        return ids;
    }

    /// <summary>Safe CSV export of sales (only fields the user is allowed to see).</summary>
    public async Task<string> SalesCsvAsync(CurrentUser user, Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var branchIds = await ScopeAsync(user, branchId, ct);
        var rows = await db.Sales.AsNoTracking()
            .Where(x => x.OrganizationId == user.OrganizationId && branchIds.Contains(x.BranchId)
                        && x.Status == SaleStatus.Completed && x.CreatedAt >= from && x.CreatedAt <= to)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.ReceiptNo,
                x.CreatedAt,
                BranchName = db.Branches.Where(b => b.Id == x.BranchId).Select(b => b.Name).First(),
                BarberName = db.Barbers.Where(b => b.Id == x.BarberId).Select(b => b.FullName).First(),
                CustomerName = x.CustomerId != null ? db.Customers.Where(c => c.Id == x.CustomerId).Select(c => c.FullName).First() : "عميل نقدي",
                Payment = x.PaymentMethod == PaymentMethod.Cash ? "نقدي" : "بطاقة",
                x.TotalAmount
            }).ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("رقم العملية,التاريخ,الفرع,الحلاق,العميل,طريقة الدفع,الإجمالي");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                r.ReceiptNo,
                r.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                Esc(r.BranchName), Esc(r.BarberName), Esc(r.CustomerName), r.Payment,
                r.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture)));
        }
        return sb.ToString();

        static string Esc(string? s) => string.IsNullOrEmpty(s) ? "\"\"" : "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>VAT return helper + discounts + retail product performance for the period.</summary>
    public async Task<object> TaxAndProductsAsync(CurrentUser user, Guid? branchId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.ReportView))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض التقارير");
        var branchIds = await ScopeAsync(user, branchId, ct);
        var sales = db.Sales.AsNoTracking().Where(x =>
            x.OrganizationId == user.OrganizationId && branchIds.Contains(x.BranchId)
            && x.Status == SaleStatus.Completed && x.CreatedAt >= from && x.CreatedAt <= to);

        var totals = await sales.GroupBy(_ => 1).Select(g => new
        {
            Gross = g.Sum(x => x.GrossAmount),
            Discount = g.Sum(x => x.DiscountAmount),
            Taxable = g.Sum(x => x.SubtotalAmount),
            Vat = g.Sum(x => x.VatAmount),
            Total = g.Sum(x => x.TotalAmount),
            Invoices = g.Count(x => x.InvoiceVatNumber != null),
            CouponSales = g.Count(x => x.CouponId != null),
            PointsRedeemed = g.Sum(x => x.PointsRedeemed),
            PointsEarned = g.Sum(x => x.PointsEarned)
        }).FirstOrDefaultAsync(ct);

        var products = await db.SaleItems.AsNoTracking()
            .Where(i => i.ItemType == SaleItemType.Product && sales.Select(s => s.Id).Contains(i.SaleId))
            .GroupBy(i => new { i.ProductId, i.ServiceName })
            .Select(g => new { g.Key.ProductId, Name = g.Key.ServiceName, Quantity = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.LineTotal) })
            .ToListAsync(ct);
        var costs = await db.Products.AsNoTracking().Where(p => p.OrganizationId == user.OrganizationId)
            .ToDictionaryAsync(p => p.Id, p => p.Cost, ct);

        var coupons = await sales.Where(s => s.CouponCode != null)
            .GroupBy(s => s.CouponCode!)
            .Select(g => new { Code = g.Key, Uses = g.Count(), Discount = g.Sum(x => x.DiscountAmount) })
            .OrderByDescending(x => x.Uses).ToListAsync(ct);

        return new
        {
            from, to,
            tax = new
            {
                gross = totals?.Gross ?? 0m,
                discount = totals?.Discount ?? 0m,
                taxable = totals?.Taxable ?? 0m,
                vat = totals?.Vat ?? 0m,
                total = totals?.Total ?? 0m,
                invoices = totals?.Invoices ?? 0
            },
            loyalty = new { pointsEarned = totals?.PointsEarned ?? 0, pointsRedeemed = totals?.PointsRedeemed ?? 0 },
            coupons,
            products = products.OrderByDescending(p => p.Revenue).Select(p =>
            {
                var cost = p.ProductId is not null && costs.TryGetValue(p.ProductId.Value, out var c) ? c * p.Quantity : 0m;
                return new { p.Name, p.Quantity, p.Revenue, Cost = cost, Profit = p.Revenue - cost };
            })
        };
    }
}
