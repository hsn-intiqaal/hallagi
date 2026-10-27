using Hallagi.Application.Tax;
using System.Globalization;
using System.Text;
using Hallagi.Application.Catalog;
using Hallagi.Application.Expenses;

namespace Hallagi.Application.Reports;

/// <summary>
/// Management reports. Every figure is derived from one projection of the period's completed sales,
/// bucketed by the organization's local calendar day, so all report sections always agree.
///
/// Revenue definitions (used consistently everywhere):
///   collected   = what customers paid (VAT inclusive) — reconciles with cash drawer + card terminal
///   net sales   = collected − VAT (VAT is owed to the authority, not revenue)
///   gross       = catalog value of items before discounts
///   operating result = net sales − cost of products sold − expenses
/// </summary>
public class ReportsService(IAppDbContext db, OrgClock clock)
{
    // ======================= Sales overview =======================

    public async Task<object> SalesReportAsync(CurrentUser user, Guid? branchId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        EnsureCanView(user);
        var range = await clock.RangeAsync(user.OrganizationId, from, to, 7, ct);
        var branchIds = await ScopeAsync(user, branchId, ct);

        var sales = await LoadSalesAsync(user.OrganizationId, branchIds, range, ct);
        var items = await LoadItemsAsync(user.OrganizationId, branchIds, range, ct);
        var expenses = await ExpensesTotalAsync(user.OrganizationId, branchIds, range, ct);
        var previous = await LoadSalesAsync(user.OrganizationId, branchIds, range.Previous(), ct);

        var summary = Summarize(sales, items, expenses);
        var previousNet = previous.Sum(s => s.Net);

        var barberNames = await db.Barbers.AsNoTracking().Where(b => b.OrganizationId == user.OrganizationId)
            .ToDictionaryAsync(b => b.Id, b => b.FullName, ct);

        return new
        {
            from = range.From,
            to = range.To,
            days = range.Days,
            summary,
            comparison = new
            {
                previousFrom = range.Previous().From,
                previousTo = range.Previous().To,
                previousNetSales = previousNet,
                changePercent = previousNet > 0 ? Math.Round((summary.NetSales - previousNet) / previousNet * 100, 1) : (decimal?)null
            },
            daily = range.EachDay().Select(day =>
            {
                var rows = sales.Where(s => range.LocalDay(s.CreatedAt) == day).ToList();
                return new
                {
                    day,
                    label = DayLabel(day, range.Days),
                    collected = rows.Sum(r => r.Total),
                    netSales = rows.Sum(r => r.Net),
                    count = rows.Count
                };
            }),
            byHour = Enumerable.Range(0, 24).Select(h => new
            {
                hour = h,
                count = sales.Count(s => OrgTime.ToLocal(s.CreatedAt, range.Zone).Hour == h),
                netSales = sales.Where(s => OrgTime.ToLocal(s.CreatedAt, range.Zone).Hour == h).Sum(s => s.Net)
            }).Where(x => x.count > 0),
            byWeekday = Enum.GetValues<DayOfWeek>().Select(d => new
            {
                weekday = (int)d,
                label = WeekdayAr(d),
                count = sales.Count(s => range.LocalDay(s.CreatedAt).DayOfWeek == d),
                netSales = sales.Where(s => range.LocalDay(s.CreatedAt).DayOfWeek == d).Sum(s => s.Net)
            }),
            byService = items.Where(i => i.Type == SaleItemType.Service)
                .GroupBy(i => i.Name)
                .Select(g => new { label = g.Key, quantity = g.Sum(x => x.Quantity), total = g.Sum(x => x.LineTotal) })
                .OrderByDescending(x => x.total).ToList(),
            byBarber = sales.GroupBy(s => s.BarberId)
                .Select(g => new
                {
                    barberId = g.Key,
                    label = barberNames.GetValueOrDefault(g.Key, "—"),
                    count = g.Count(),
                    netSales = g.Sum(x => x.Net),
                    commissionBase = g.Sum(x => x.CommissionBase),
                    averageTicket = Math.Round(g.Average(x => x.Total), 2)
                })
                .OrderByDescending(x => x.netSales).ToList()
        };
    }

    public async Task<object> ExpensesByCategoryAsync(CurrentUser user, Guid? branchId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        EnsureCanView(user);
        var range = await clock.RangeAsync(user.OrganizationId, from, to, 7, ct);
        var branchIds = await ScopeAsync(user, branchId, ct);
        var rows = await db.Expenses.AsNoTracking()
            .Where(x => x.OrganizationId == user.OrganizationId && branchIds.Contains(x.BranchId)
                        && x.Status == ExpenseStatus.Active && x.ExpenseDate >= range.From && x.ExpenseDate <= range.To)
            .Select(x => new { x.Category, x.Amount })
            .ToListAsync(ct);

        var total = rows.Sum(r => r.Amount);
        return new
        {
            from = range.From,
            to = range.To,
            total,
            items = rows.GroupBy(r => r.Category)
                .Select(g => new
                {
                    category = g.Key.ToString(),
                    categoryText = ExpenseService.CategoryText(g.Key),
                    total = g.Sum(x => x.Amount),
                    count = g.Count(),
                    share = total > 0 ? Math.Round(g.Sum(x => x.Amount) / total * 100, 1) : 0m
                })
                .OrderByDescending(x => x.total)
        };
    }

    public async Task<object> BarberDuesAsync(CurrentUser user, Guid? branchId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.BarberDuesView))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض مستحقات الحلاقين");

        var range = await clock.RangeAsync(user.OrganizationId, from, to, 7, ct);
        var branchIds = await ScopeAsync(user, branchId, ct);
        var sales = await LoadSalesAsync(user.OrganizationId, branchIds, range, ct);
        var branchNames = await db.Branches.AsNoTracking().Where(b => b.OrganizationId == user.OrganizationId)
            .ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        // barbers working in the scoped branches, plus anyone who sold there during the period
        var sellers = sales.Select(s => s.BarberId).ToHashSet();
        var barbers = await db.Barbers.AsNoTracking().Include(b => b.BranchAssignments)
            .Where(b => b.OrganizationId == user.OrganizationId
                        && (b.BranchAssignments.Any(a => branchIds.Contains(a.BranchId)) || sellers.Contains(b.Id)))
            .OrderBy(b => b.FullName)
            .ToListAsync(ct);
        var barberIds = barbers.Select(b => b.Id).ToList();
        var agreements = (await db.BarberCompensations.AsNoTracking()
                .Where(c => barberIds.Contains(c.BarberId) && c.Status == EntityStatus.Active && c.EffectiveFrom <= range.To)
                .ToListAsync(ct))
            .GroupBy(c => c.BarberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.EffectiveFrom).First());

        var items = barbers.Select(b =>
        {
            var mine = sales.Where(s => s.BarberId == b.Id).ToList();
            var eligible = mine.Sum(s => s.CommissionBase);
            agreements.TryGetValue(b.Id, out var comp);
            var commission = comp?.CommissionRate is > 0 ? Pricing.Round(eligible * comp.CommissionRate.Value) : 0m;
            return new
            {
                barberId = b.Id,
                barberName = b.FullName,
                active = b.Status == EntityStatus.Active,
                branchNames = b.BranchAssignments.Select(a => branchNames.GetValueOrDefault(a.BranchId, "—")).ToList(),
                salesCount = mine.Count,
                eligibleSales = eligible,
                compensationType = comp?.Type.ToString(),
                compensationTypeText = comp is null ? "غير محدد" : CatalogService.CompensationTypeText(comp.Type),
                monthlySalary = comp?.MonthlySalary,
                commissionRate = comp?.CommissionRate,
                estimatedCommission = commission,
                chairRent = comp?.ChairRentAmount,
                explanation = comp switch
                {
                    { CommissionRate: > 0 } => $"خدمات مؤهلة {eligible:0.00} ر.س (بدون الضريبة والمنتجات وبعد الخصم) × {comp.CommissionRate:P0} = {commission:0.00} ر.س",
                    { Type: CompensationType.FixedSalary } => "راتب ثابت مستقل عن المبيعات",
                    { Type: CompensationType.ChairRental } => "تأجير كرسي — الحلاق يحتفظ بإيراده ويدفع الإيجار",
                    _ => "لا يوجد اتفاق تعويض مسجل"
                }
            };
        }).ToList();

        return new { from = range.From, to = range.To, items, totalCommission = items.Sum(i => i.estimatedCommission) };
    }

    public async Task<object> BranchComparisonAsync(CurrentUser user, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        EnsureCanView(user);
        var range = await clock.RangeAsync(user.OrganizationId, from, to, 7, ct);
        var branchIds = await ScopeAsync(user, null, ct);
        var branches = await db.Branches.AsNoTracking().Where(b => branchIds.Contains(b.Id)).OrderBy(b => b.Name).ToListAsync(ct);
        var sales = await LoadSalesAsync(user.OrganizationId, branchIds, range, ct);
        var items = await LoadItemsAsync(user.OrganizationId, branchIds, range, ct);
        var expenses = await db.Expenses.AsNoTracking()
            .Where(x => branchIds.Contains(x.BranchId) && x.Status == ExpenseStatus.Active
                        && x.ExpenseDate >= range.From && x.ExpenseDate <= range.To)
            .Select(x => new { x.BranchId, x.Amount }).ToListAsync(ct);

        var rows = branches.Select(b =>
        {
            var branchSales = sales.Where(s => s.BranchId == b.Id).ToList();
            var ids = branchSales.Select(s => s.Id).ToHashSet();
            var s = Summarize(branchSales, items.Where(i => ids.Contains(i.SaleId)).ToList(),
                expenses.Where(e => e.BranchId == b.Id).Sum(e => e.Amount));
            return new
            {
                branchId = b.Id,
                branchName = b.Name,
                city = b.City,
                salesCount = s.SalesCount,
                collected = s.Collected,
                netSales = s.NetSales,
                averageTicket = s.AverageTicket,
                expenses = s.Expenses,
                productCost = s.ProductCost,
                operatingResult = s.OperatingResult
            };
        }).ToList();

        var totalNet = rows.Sum(r => r.netSales);
        return new
        {
            from = range.From,
            to = range.To,
            items = rows.Select(r => new
            {
                r.branchId, r.branchName, r.city, r.salesCount, r.collected, r.netSales, r.averageTicket,
                r.expenses, r.productCost, r.operatingResult,
                share = totalNet > 0 ? Math.Round(r.netSales / totalNet * 100, 1) : 0m
            })
        };
    }

    public async Task<object> CustomersAsync(CurrentUser user, Guid? branchId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        EnsureCanView(user);
        var range = await clock.RangeAsync(user.OrganizationId, from, to, 30, ct);
        var branchIds = await ScopeAsync(user, branchId, ct);
        var sales = await LoadSalesAsync(user.OrganizationId, branchIds, range, ct);

        var known = sales.Where(s => s.CustomerId != null).GroupBy(s => s.CustomerId!.Value).ToList();
        var customerIds = known.Select(g => g.Key).ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.FullName, c.Phone, c.CreatedAt }).ToDictionaryAsync(c => c.Id, ct);
        // first-ever visit decides "new" vs "returning" for the period
        var firstVisits = await db.Sales.AsNoTracking()
            .Where(s => s.OrganizationId == user.OrganizationId && s.Status == SaleStatus.Completed && s.CustomerId != null
                        && customerIds.Contains(s.CustomerId!.Value))
            .GroupBy(s => s.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, First = g.Min(x => x.CreatedAt) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.First, ct);

        var newCustomers = known.Count(g => firstVisits.GetValueOrDefault(g.Key) >= range.FromUtc);
        return new
        {
            from = range.From,
            to = range.To,
            summary = new
            {
                identifiedVisits = sales.Count(s => s.CustomerId != null),
                walkInVisits = sales.Count(s => s.CustomerId == null),
                uniqueCustomers = known.Count,
                newCustomers,
                returningCustomers = known.Count - newCustomers,
                repeatInPeriod = known.Count(g => g.Count() > 1)
            },
            items = known
                .Select(g => new
                {
                    customerId = g.Key,
                    customerName = customers.TryGetValue(g.Key, out var c) ? c.FullName : "—",
                    phone = customers.TryGetValue(g.Key, out var c2) ? c2.Phone : null,
                    visits = g.Count(),
                    total = g.Sum(x => x.Total),
                    lastVisit = g.Max(x => x.CreatedAt)
                })
                .OrderByDescending(x => x.visits).ThenByDescending(x => x.total)
                .Take(100)
        };
    }

    /// <summary>VAT return helper + discounts + loyalty + retail product performance for the period.</summary>
    public async Task<object> TaxAndProductsAsync(CurrentUser user, Guid? branchId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        EnsureCanView(user);
        var range = await clock.RangeAsync(user.OrganizationId, from, to, 30, ct);
        var branchIds = await ScopeAsync(user, branchId, ct);
        var sales = await LoadSalesAsync(user.OrganizationId, branchIds, range, ct);
        var items = await LoadItemsAsync(user.OrganizationId, branchIds, range, ct);
        var currentCosts = await db.Products.AsNoTracking().Where(p => p.OrganizationId == user.OrganizationId)
            .ToDictionaryAsync(p => p.Id, p => p.Cost, ct);

        return new
        {
            from = range.From,
            to = range.To,
            tax = new
            {
                gross = sales.Sum(s => s.Gross),
                discount = sales.Sum(s => s.Discount),
                taxable = sales.Where(s => s.TaxInvoice).Sum(s => s.Net),
                vat = sales.Sum(s => s.Vat),
                total = sales.Sum(s => s.Total),
                invoices = sales.Count(s => s.TaxInvoice),
                nonTaxable = sales.Where(s => !s.TaxInvoice).Sum(s => s.Total)
            },
            loyalty = new { pointsEarned = sales.Sum(s => s.PointsEarned), pointsRedeemed = sales.Sum(s => s.PointsRedeemed) },
            coupons = sales.Where(s => s.Coupon != null).GroupBy(s => s.Coupon!)
                .Select(g => new { code = g.Key, uses = g.Count(), discount = g.Sum(x => x.Discount) })
                .OrderByDescending(x => x.uses),
            products = items.Where(i => i.Type == SaleItemType.Product)
                .GroupBy(i => new { i.ProductId, i.Name })
                .Select(g =>
                {
                    var revenue = g.Sum(x => x.LineTotal);
                    var cost = g.Sum(x => x.Quantity * (x.UnitCost ?? (x.ProductId is { } id ? currentCosts.GetValueOrDefault(id) : 0m)));
                    return new { name = g.Key.Name, quantity = g.Sum(x => x.Quantity), revenue, cost, profit = revenue - cost };
                })
                .OrderByDescending(x => x.revenue)
        };
    }

    /// <summary>Safe CSV export of sales (local times, net/VAT split, only fields the user may see).</summary>
    public async Task<string> SalesCsvAsync(CurrentUser user, Guid? branchId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        EnsureCanView(user);
        var range = await clock.RangeAsync(user.OrganizationId, from, to, 30, ct);
        var branchIds = await ScopeAsync(user, branchId, ct);
        var rows = await db.Sales.AsNoTracking()
            .Where(x => x.OrganizationId == user.OrganizationId && branchIds.Contains(x.BranchId)
                        && x.Status == SaleStatus.Completed && x.CreatedAt >= range.FromUtc && x.CreatedAt < range.ToUtc)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.ReceiptNo,
                x.CreatedAt,
                BranchName = db.Branches.Where(b => b.Id == x.BranchId).Select(b => b.Name).First(),
                BarberName = db.Barbers.Where(b => b.Id == x.BarberId).Select(b => b.FullName).First(),
                CustomerName = x.CustomerId != null ? db.Customers.Where(c => c.Id == x.CustomerId).Select(c => c.FullName).First() : "عميل نقدي",
                Payment = x.PaymentMethod == PaymentMethod.Cash ? "نقدي" : "بطاقة",
                x.DiscountAmount,
                x.SubtotalAmount,
                x.VatAmount,
                x.TotalAmount
            }).ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("رقم العملية,التاريخ,الفرع,الحلاق,العميل,طريقة الدفع,الخصم,الصافي قبل الضريبة,الضريبة,الإجمالي");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                r.ReceiptNo,
                OrgTime.ToLocal(r.CreatedAt, range.Zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                Esc(r.BranchName), Esc(r.BarberName), Esc(r.CustomerName), r.Payment,
                Money(r.DiscountAmount), Money(r.SubtotalAmount), Money(r.VatAmount), Money(r.TotalAmount)));
        }
        return sb.ToString();

        static string Money(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        static string Esc(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            // neutralize spreadsheet formula injection
            if (s[0] is '=' or '+' or '-' or '@') s = "'" + s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }

    // ======================= Shared building blocks =======================

    public sealed record SaleRow(
        Guid Id, Guid BranchId, Guid BarberId, Guid? CustomerId, DateTime CreatedAt, PaymentMethod Payment,
        decimal Gross, decimal Discount, decimal Net, decimal Vat, decimal Total, decimal CommissionBase,
        string? Coupon, int PointsEarned, int PointsRedeemed, bool TaxInvoice);

    public sealed record ItemRow(Guid SaleId, SaleItemType Type, Guid? ProductId, string Name, int Quantity, decimal LineTotal, decimal? UnitCost);

    public sealed record Summary(
        int SalesCount, decimal Collected, decimal Gross, decimal Discounts, decimal NetSales, decimal Vat,
        decimal CashCollected, decimal CardCollected, decimal AverageTicket,
        decimal ServicesRevenue, decimal ProductsRevenue, decimal ProductCost, decimal Expenses, decimal OperatingResult, int ServicesCount);

    public static Summary Summarize(IReadOnlyCollection<SaleRow> sales, IReadOnlyCollection<ItemRow> items, decimal expenses)
    {
        var collected = sales.Sum(s => s.Total);
        var net = sales.Sum(s => s.Net);
        var productCost = items.Where(i => i.Type == SaleItemType.Product).Sum(i => i.Quantity * (i.UnitCost ?? 0m));
        return new Summary(
            SalesCount: sales.Count,
            Collected: collected,
            Gross: sales.Sum(s => s.Gross),
            Discounts: sales.Sum(s => s.Discount),
            NetSales: net,
            Vat: sales.Sum(s => s.Vat),
            CashCollected: sales.Where(s => s.Payment == PaymentMethod.Cash).Sum(s => s.Total),
            CardCollected: sales.Where(s => s.Payment == PaymentMethod.Card).Sum(s => s.Total),
            AverageTicket: sales.Count > 0 ? Pricing.Round(collected / sales.Count) : 0m,
            ServicesRevenue: items.Where(i => i.Type == SaleItemType.Service).Sum(i => i.LineTotal),
            ProductsRevenue: items.Where(i => i.Type == SaleItemType.Product).Sum(i => i.LineTotal),
            ProductCost: productCost,
            Expenses: expenses,
            OperatingResult: net - productCost - expenses,
            ServicesCount: items.Where(i => i.Type == SaleItemType.Service).Sum(i => i.Quantity));
    }

    public async Task<List<SaleRow>> LoadSalesAsync(Guid orgId, IReadOnlyCollection<Guid> branchIds, DateRange range, CancellationToken ct)
        => (await db.Sales.AsNoTracking()
                .Where(s => s.OrganizationId == orgId && branchIds.Contains(s.BranchId) && s.Status == SaleStatus.Completed
                            && s.CreatedAt >= range.FromUtc && s.CreatedAt < range.ToUtc)
                .Select(s => new
                {
                    s.Id, s.BranchId, s.BarberId, s.CustomerId, s.CreatedAt, s.PaymentMethod,
                    s.GrossAmount, s.DiscountAmount, s.SubtotalAmount, s.VatAmount, s.TotalAmount, s.CommissionBase,
                    s.CouponCode, s.PointsEarned, s.PointsRedeemed, s.InvoiceVatNumber
                })
                .ToListAsync(ct))
            .Select(s => new SaleRow(
                s.Id, s.BranchId, s.BarberId, s.CustomerId, DateTime.SpecifyKind(s.CreatedAt, DateTimeKind.Utc), s.PaymentMethod,
                // rows written before the VAT/discount breakdown existed carry only the total
                s.GrossAmount == 0 ? s.TotalAmount : s.GrossAmount,
                s.DiscountAmount,
                s.SubtotalAmount == 0 && s.VatAmount == 0 ? s.TotalAmount : s.SubtotalAmount,
                s.VatAmount, s.TotalAmount, s.CommissionBase,
                s.CouponCode, s.PointsEarned, s.PointsRedeemed, s.InvoiceVatNumber != null))
            .ToList();

    public async Task<List<ItemRow>> LoadItemsAsync(Guid orgId, IReadOnlyCollection<Guid> branchIds, DateRange range, CancellationToken ct)
        => await db.SaleItems.AsNoTracking()
            .Where(i => db.Sales.Any(s => s.Id == i.SaleId && s.OrganizationId == orgId && branchIds.Contains(s.BranchId)
                                          && s.Status == SaleStatus.Completed
                                          && s.CreatedAt >= range.FromUtc && s.CreatedAt < range.ToUtc))
            .Select(i => new ItemRow(i.SaleId, i.ItemType, i.ProductId, i.ServiceName, i.Quantity, i.LineTotal, i.UnitCost))
            .ToListAsync(ct);

    public async Task<decimal> ExpensesTotalAsync(Guid orgId, IReadOnlyCollection<Guid> branchIds, DateRange range, CancellationToken ct)
        => await db.Expenses.AsNoTracking()
            .Where(x => x.OrganizationId == orgId && branchIds.Contains(x.BranchId) && x.Status == ExpenseStatus.Active
                        && x.ExpenseDate >= range.From && x.ExpenseDate <= range.To)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

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

    private static void EnsureCanView(CurrentUser user)
    {
        if (!Permissions.Can(user, Permissions.ReportView))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض التقارير");
    }

    /// <summary>Short axis label: weekday for a week, day/month for longer ranges.</summary>
    public static string DayLabel(DateOnly day, int rangeDays)
        => rangeDays <= 7 ? WeekdayAr(day.DayOfWeek) : $"{day.Day}/{day.Month}";

    public static string WeekdayAr(DayOfWeek day) => day switch
    {
        DayOfWeek.Sunday => "الأحد",
        DayOfWeek.Monday => "الاثنين",
        DayOfWeek.Tuesday => "الثلاثاء",
        DayOfWeek.Wednesday => "الأربعاء",
        DayOfWeek.Thursday => "الخميس",
        DayOfWeek.Friday => "الجمعة",
        _ => "السبت"
    };
}
