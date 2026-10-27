using Hallagi.Application.Treasury;

namespace Hallagi.Application.Reports;

/// <summary>Home screen figures. Uses the same sale projection and definitions as <see cref="ReportsService"/>.</summary>
public class DashboardService(IAppDbContext db, ShiftService shifts, ReportsService reports, OrgClock clock)
{
    public async Task<object> GetAsync(CurrentUser user, Guid? branchId, string? period, CancellationToken ct = default)
    {
        var branchIds = await ResolveBranchIdsAsync(user, branchId, ct);
        var zone = await clock.ZoneAsync(user.OrganizationId, ct);
        var today = await clock.TodayAsync(user.OrganizationId, ct);
        var nowUtc = clock.UtcNow;

        var range = ResolvePeriod(period, today, zone);
        // compare like with like: the previous period up to the same elapsed point in time
        var elapsed = nowUtc - range.FromUtc;
        var previousRange = range.Previous();
        var previousCutoff = previousRange.FromUtc + elapsed;

        var sales = await reports.LoadSalesAsync(user.OrganizationId, branchIds, range, ct);
        var items = await reports.LoadItemsAsync(user.OrganizationId, branchIds, range, ct);
        var expenses = await reports.ExpensesTotalAsync(user.OrganizationId, branchIds, range, ct);
        var summary = ReportsService.Summarize(sales, items, expenses);
        var previousNet = (await reports.LoadSalesAsync(user.OrganizationId, branchIds, previousRange, ct))
            .Where(s => s.CreatedAt < previousCutoff).Sum(s => s.Net);

        var week = DateRange.Create(today.AddDays(-6), today, zone);
        var weekSales = range.From <= week.From ? sales.Where(s => s.CreatedAt >= week.FromUtc).ToList()
            : await reports.LoadSalesAsync(user.OrganizationId, branchIds, week, ct);

        // Drawer of the selected branch
        object? drawer = null;
        if (branchId.HasValue)
        {
            var openShift = await db.Shifts.AsNoTracking()
                .Where(s => s.BranchId == branchId.Value && s.Status == ShiftStatus.Open)
                .OrderByDescending(s => s.OpenedAt).FirstOrDefaultAsync(ct);
            if (openShift is not null)
            {
                var shiftSummary = await shifts.SummarizeAsync(openShift, ct);
                drawer = new { openShift.Id, openShift.OpeningCash, shiftSummary.ExpectedCash };
            }
        }

        var lastSaleIds = sales.OrderByDescending(s => s.CreatedAt).Take(6).Select(s => s.Id).ToList();
        var lastSales = await db.Sales.AsNoTracking()
            .Where(x => lastSaleIds.Contains(x.Id))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.ReceiptNo,
                x.CreatedAt,
                x.TotalAmount,
                PaymentText = x.PaymentMethod == PaymentMethod.Cash ? "نقدي" : "بطاقة",
                BranchName = db.Branches.Where(b => b.Id == x.BranchId).Select(b => b.Name).First(),
                BarberName = db.Barbers.Where(b => b.Id == x.BarberId).Select(b => b.FullName).First(),
                CustomerName = x.CustomerId != null
                    ? db.Customers.Where(c => c.Id == x.CustomerId).Select(c => c.FullName).First()
                    : "عميل نقدي"
            }).ToListAsync(ct);

        // Operational signals
        var staleBefore = nowUtc.AddHours(-14);
        var stale = await db.Shifts.AsNoTracking()
            .AnyAsync(s => s.OrganizationId == user.OrganizationId && s.Status == ShiftStatus.Open
                           && branchIds.Contains(s.BranchId) && s.OpenedAt < staleBefore, ct);
        var todayRange = DateRange.Create(today, today, zone);
        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => a.OrganizationId == user.OrganizationId && branchIds.Contains(a.BranchId)
                        && a.StartAt >= todayRange.FromUtc && a.StartAt < todayRange.ToUtc
                        && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
            .Select(a => new { a.Status, a.StartAt }).ToListAsync(ct);
        var lowStock = await db.ProductStocks.AsNoTracking()
            .CountAsync(s => branchIds.Contains(s.BranchId) && s.Quantity <= s.LowStockThreshold
                             && s.Product.Status == EntityStatus.Active && s.Product.OrganizationId == user.OrganizationId, ct);

        var branches = await db.Branches.AsNoTracking()
            .Where(b => b.OrganizationId == user.OrganizationId && b.Status == EntityStatus.Active)
            .Select(b => new { b.Id, b.Name, b.City }).ToListAsync(ct);

        return new
        {
            period = period ?? "today",
            from = range.From,
            to = range.To,
            today,
            branches,
            drawer,
            staleOpenShift = stale,
            metrics = new
            {
                totalSales = summary.Collected,
                netSales = summary.NetSales,
                vat = summary.Vat,
                discounts = summary.Discounts,
                servicesCount = summary.ServicesCount,
                salesCount = summary.SalesCount,
                averageTicket = summary.AverageTicket,
                expensesTotal = summary.Expenses,
                productCost = summary.ProductCost,
                netOperatingEstimate = summary.OperatingResult,
                netOperatingHint = "صافي المبيعات بدون الضريبة − تكلفة المنتجات − المصروفات",
                cashSales = summary.CashCollected,
                cardSales = summary.CardCollected
            },
            comparison = new
            {
                previousNetSales = previousNet,
                changePercent = previousNet > 0 ? Math.Round((summary.NetSales - previousNet) / previousNet * 100, 1) : (decimal?)null,
                changeText = ChangeText(previousNet, summary.NetSales, period)
            },
            signals = new
            {
                appointmentsToday = appointments.Count,
                appointmentsRemaining = appointments.Count(a => a.StartAt > nowUtc
                    && a.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed),
                waiting = appointments.Count(a => a.Status == AppointmentStatus.Waiting),
                lowStockProducts = lowStock
            },
            lastSales,
            topServices = items.Where(i => i.Type == SaleItemType.Service)
                .GroupBy(i => i.Name)
                .Select(g => new { serviceName = g.Key, quantity = g.Sum(x => x.Quantity), total = g.Sum(x => x.LineTotal) })
                .OrderByDescending(x => x.quantity).ThenByDescending(x => x.total).Take(5),
            chart = new
            {
                items = week.EachDay().Select(day => new
                {
                    day = day.ToString("yyyy-MM-dd"),
                    label = ReportsService.WeekdayAr(day.DayOfWeek),
                    isToday = day == today,
                    total = weekSales.Where(s => week.LocalDay(s.CreatedAt) == day).Sum(s => s.Total),
                    count = weekSales.Count(s => week.LocalDay(s.CreatedAt) == day)
                })
            }
        };
    }

    public async Task<List<Guid>> ResolveBranchIdsAsync(CurrentUser user, Guid? branchId, CancellationToken ct)
    {
        List<Guid> ids;
        if (branchId.HasValue)
        {
            if (!Permissions.CanAccessBranch(user, branchId.Value))
                throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
            ids = new List<Guid> { branchId.Value };
        }
        else if (Permissions.AccessesAllBranches(user))
        {
            ids = await db.Branches.Where(b => b.OrganizationId == user.OrganizationId && b.Status == EntityStatus.Active)
                .Select(b => b.Id).ToListAsync(ct);
        }
        else
        {
            ids = user.BranchIds.ToList();
            if (ids.Count == 0) ids.Add(Guid.Empty);
        }
        return ids;
    }

    /// <summary>today | week (last 7 days) | month (calendar month to date) — in the organization's local calendar.</summary>
    public static DateRange ResolvePeriod(string? period, DateOnly today, TimeZoneInfo zone) => period?.ToLowerInvariant() switch
    {
        "week" => DateRange.Create(today.AddDays(-6), today, zone),
        "month" => DateRange.Create(new DateOnly(today.Year, today.Month, 1), today, zone),
        _ => DateRange.Create(today, today, zone)
    };

    private static string ChangeText(decimal previous, decimal current, string? period)
    {
        var against = period?.ToLowerInvariant() switch
        {
            "week" => "الأسبوع السابق",
            "month" => "الشهر السابق",
            _ => "أمس في نفس الوقت"
        };
        if (previous == 0 && current == 0) return "لا توجد مبيعات في الفترتين";
        if (previous == 0) return $"لا توجد مبيعات {against} للمقارنة";
        var change = (current - previous) / previous * 100;
        if (change > 0) return $"أعلى بنسبة {Math.Round(change, 1):0.0}% من {against}";
        if (change < 0) return $"أقل بنسبة {Math.Abs(Math.Round(change, 1)):0.0}% من {against}";
        return $"مطابق لـ{against}";
    }
}
