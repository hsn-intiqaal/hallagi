using Hallagi.Api.Data;
using Hallagi.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hallagi.Api.Services;

public class DashboardService(AppDbContext db, ShiftService shifts)
{
    public async Task<object> GetAsync(CurrentUser user, Guid? branchId, string period, CancellationToken ct = default)
    {
        var allowedBranchIds = await ResolveBranchIdsAsync(user, branchId, ct);

        var now = DateTime.UtcNow;
        var (from, to) = ResolvePeriod(period, now);
        var prevTo = from;
        var prevFrom = from.AddSeconds(-(to - from).TotalSeconds); // previous window of same length

        var current = await TotalsAsync(user.OrganizationId, allowedBranchIds, from, to, ct);
        var previous = await TotalsAsync(user.OrganizationId, allowedBranchIds, prevFrom, prevTo, ct);

        // Branch list for the selector
        var branches = await db.Branches.AsNoTracking()
            .Where(b => b.OrganizationId == user.OrganizationId && b.Status == EntityStatus.Active)
            .Select(b => new { b.Id, b.Name, b.City }).ToListAsync(ct);

        // Open shift / drawer expected cash for the selected scope
        object? drawer = null;
        Guid? openShiftBranch = branchId;
        if (openShiftBranch.HasValue)
        {
            var openShift = await db.Shifts.AsNoTracking()
                .Where(s => s.BranchId == openShiftBranch.Value && s.Status == ShiftStatus.Open)
                .OrderByDescending(s => s.OpenedAt).FirstOrDefaultAsync(ct);
            if (openShift is not null)
            {
                var summary = await shifts.SummarizeAsync(openShift, ct);
                drawer = new { openShift.Id, openShift.OpeningCash, summary.ExpectedCash };
            }
        }

        var lastSales = await db.Sales.AsNoTracking()
            .Where(x => x.OrganizationId == user.OrganizationId && allowedBranchIds.Contains(x.BranchId)
                        && x.CreatedAt >= from && x.CreatedAt <= to && x.Status == SaleStatus.Completed)
            .OrderByDescending(x => x.CreatedAt).Take(5)
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

        var topServices = await db.SaleItems.AsNoTracking()
            .Where(i => db.Sales.Any(s => s.Id == i.SaleId
                       && s.OrganizationId == user.OrganizationId
                       && allowedBranchIds.Contains(s.BranchId)
                       && s.Status == SaleStatus.Completed
                       && s.CreatedAt >= from && s.CreatedAt <= to))
            .GroupBy(i => i.ServiceName)
            .Select(g => new { ServiceName = g.Key, Quantity = g.Sum(x => x.Quantity), Total = g.Sum(x => x.LineTotal) })
            .OrderByDescending(x => x.Quantity).Take(5).ToListAsync(ct);

        // 7-day simple chart (completed sales only)
        var weekStart = DateTime.Today.AddDays(-6);
        var chart = await db.Sales.AsNoTracking()
            .Where(x => x.OrganizationId == user.OrganizationId && allowedBranchIds.Contains(x.BranchId)
                        && x.Status == SaleStatus.Completed && x.CreatedAt >= weekStart)
            .GroupBy(x => x.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Total = g.Sum(x => x.TotalAmount), Count = g.Count() })
            .OrderBy(x => x.Day).ToListAsync(ct);

        // A previous shift left open overnight → alert
        var staleBefore = DateTime.UtcNow.AddHours(-14);
        var stale = await db.Shifts.AsNoTracking()
            .AnyAsync(s => s.OrganizationId == user.OrganizationId && s.Status == ShiftStatus.Open
                           && allowedBranchIds.Contains(s.BranchId) && s.OpenedAt < staleBefore, ct);

        return new
        {
            period,
            from,
            to,
            branches,
            drawer,
            staleOpenShift = stale,
            metrics = new
            {
                totalSales = current.TotalSales,
                servicesCount = current.ServicesCount,
                expensesTotal = current.ExpensesTotal,
                netOperatingEstimate = current.TotalSales - current.ExpensesTotal,
                netOperatingHint = "صافي التشغيل التقديري من السجلات المسجلة (لا يشمل كل الالتزامات)",
                cashSales = current.CashSales,
                cardSales = current.CardSales,
                salesCount = current.SalesCount
            },
            comparison = new
            {
                previousTotalSales = previous.TotalSales,
                changePercent = previous.TotalSales > 0
                    ? Math.Round((current.TotalSales - previous.TotalSales) / previous.TotalSales * 100, 1)
                    : (decimal?)null,
                changeText = ChangeText(previous.TotalSales, current.TotalSales)
            },
            lastSales,
            topServices,
            chart = new
            {
                items = chart.Select(c => new
                {
                    day = c.Day.ToString("yyyy-MM-dd"),
                    label = WeekdayAr(c.Day),
                    total = c.Total,
                    count = c.Count
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
                throw ApiException.Forbidden("لا تملك صلاحية على هذا الفرع");
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

    private async Task<(decimal TotalSales, decimal CashSales, decimal CardSales, int ServicesCount, decimal ExpensesTotal, int SalesCount)>
        TotalsAsync(Guid orgId, List<Guid> branchIds, DateTime from, DateTime to, CancellationToken ct)
    {
        var sales = db.Sales.AsNoTracking().Where(x =>
            x.OrganizationId == orgId && branchIds.Contains(x.BranchId)
            && x.Status == SaleStatus.Completed && x.CreatedAt >= from && x.CreatedAt <= to);

        var total = await sales.SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0m;
        var cash = await sales.Where(x => x.PaymentMethod == PaymentMethod.Cash).SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0m;
        var card = await sales.Where(x => x.PaymentMethod == PaymentMethod.Card).SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0m;
        var count = await sales.CountAsync(ct);

        var servicesCount = await db.SaleItems.AsNoTracking()
            .Where(i => db.Sales.Any(s => s.Id == i.SaleId && s.OrganizationId == orgId
                       && branchIds.Contains(s.BranchId) && s.Status == SaleStatus.Completed
                       && s.CreatedAt >= from && s.CreatedAt <= to))
            .SumAsync(i => (int?)i.Quantity, ct) ?? 0;

        var expenses = await db.Expenses.AsNoTracking()
            .Where(x => x.OrganizationId == orgId && branchIds.Contains(x.BranchId)
                        && x.Status == ExpenseStatus.Active
                        && x.ExpenseDate >= DateOnly.FromDateTime(from.Date)
                        && x.ExpenseDate <= DateOnly.FromDateTime(to.Date))
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

        return (total, cash, card, servicesCount, expenses, count);
    }

    public static (DateTime From, DateTime To) ResolvePeriod(string? period, DateTime nowUtc)
    {
        var localToday = DateTime.Today;
        var todayStart = localToday.Date;
        var todayEnd = todayStart.AddDays(1).AddTicks(-1);
        return (period?.ToLowerInvariant()) switch
        {
            "week" => (todayStart.AddDays(-6), todayEnd),
            "month" => (new DateTime(localToday.Year, localToday.Month, 1), todayEnd),
            _ => (todayStart, todayEnd)
        };
    }

    private static string ChangeText(decimal previous, decimal current)
    {
        if (previous == 0 && current == 0) return "لا توجد مبيعات في الفترتين";
        if (previous == 0) return "زيادة عن الفترة السابقة";
        var change = (current - previous) / previous * 100;
        if (change > 0) return $"أعلى بنسبة {Math.Round(change, 1):0.0}% من الفترة السابقة";
        if (change < 0) return $"أقل بنسبة {Math.Abs(Math.Round(change, 1)):0.0}% من الفترة السابقة";
        return "مطابق للفترة السابقة";
    }

    private static string WeekdayAr(DateTime day) => day.DayOfWeek switch
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
