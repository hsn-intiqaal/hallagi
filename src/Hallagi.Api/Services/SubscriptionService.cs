using Hallagi.Api.Data;
using Hallagi.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hallagi.Api.Services;

public class SubscriptionService(AppDbContext db)
{
    public async Task EnsureSeededAsync(CancellationToken ct = default)
    {
        if (await db.SubscriptionPlans.AnyAsync(ct)) return;

        var now = DateTime.UtcNow;
        db.SubscriptionPlans.AddRange(
            new SubscriptionPlan
            {
                Id = Guid.NewGuid(), Name = "تجريبي", Description = "فرع واحد ومستخدمان — للتجربة",
                MaxBranches = 1, MaxUsers = 2, MaxBarbers = 3,
                AllowAdvancedReports = false, AllowSettlements = false, MonthlyPrice = 0m, IsActive = true, CreatedAt = now
            },
            new SubscriptionPlan
            {
                Id = Guid.NewGuid(), Name = "أساسي", Description = "٣ فروع و٥ مستخدمين مع التقارير",
                MaxBranches = 3, MaxUsers = 5, MaxBarbers = 10,
                AllowAdvancedReports = true, AllowSettlements = false, MonthlyPrice = 149m, IsActive = true, CreatedAt = now
            },
            new SubscriptionPlan
            {
                Id = Guid.NewGuid(), Name = "احترافي", Description = "فروع ومستخدمون غير محدودين مع التسويات المالية",
                MaxBranches = 100, MaxUsers = 100, MaxBarbers = 200,
                AllowAdvancedReports = true, AllowSettlements = true, MonthlyPrice = 349m, IsActive = true, CreatedAt = now
            });
        await db.SaveChangesAsync(ct);
    }

    public async Task<object> GetCurrentAsync(Guid orgId, CancellationToken ct = default)
    {
        var sub = await db.Subscriptions.AsNoTracking()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.OrganizationId == orgId, ct);
        if (sub is null)
        {
            return new { hasSubscription = false };
        }
        return new
        {
            hasSubscription = true,
            status = sub.Status,
            periodStart = sub.PeriodStart,
            periodEnd = sub.PeriodEnd,
            trialEndsAt = sub.TrialEndsAt,
            plan = new
            {
                sub.Plan.Id,
                sub.Plan.Name,
                sub.Plan.Description,
                sub.Plan.MaxBranches,
                sub.Plan.MaxUsers,
                sub.Plan.MaxBarbers,
                sub.Plan.AllowAdvancedReports,
                sub.Plan.AllowSettlements,
                sub.Plan.MonthlyPrice
            }
        };
    }

    public async Task<List<SubscriptionPlan>> ListPlansAsync(CancellationToken ct = default)
        => await db.SubscriptionPlans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.MonthlyPrice).ToListAsync(ct);

    public async Task ChangePlanAsync(Guid orgId, Guid planId, CurrentUser user, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SettingsManage))
            throw ApiException.Forbidden("ليس لديك صلاحية لتغيير الخطة");

        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct)
            ?? throw ApiException.NotFound("الخطة غير موجودة");

        var sub = await db.Subscriptions.FirstOrDefaultAsync(s => s.OrganizationId == orgId, ct);
        if (sub is null)
        {
            sub = new Subscription
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                PlanId = plan.Id,
                Status = "Active",
                PeriodStart = DateOnly.FromDateTime(DateTime.UtcNow),
                PeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Subscriptions.Add(sub);
        }
        else
        {
            sub.PlanId = plan.Id;
            sub.Status = "Active";
            sub.PeriodStart = DateOnly.FromDateTime(DateTime.UtcNow);
            sub.PeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1));
            sub.UpdatedAt = DateTime.UtcNow;
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ActorId = user.ProfileId,
            Action = "subscription.changed",
            EntityType = nameof(Subscription),
            EntityId = sub.Id.ToString(),
            Details = $"plan={plan.Name}",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Enforces the plan's branch limit when creating a branch.</summary>
    public async Task EnsureBranchLimitAsync(Guid orgId, CancellationToken ct = default)
    {
        var sub = await db.Subscriptions.AsNoTracking().Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.OrganizationId == orgId, ct);
        if (sub?.Plan is null) return; // no subscription yet (dev/demo) -> unlimited

        var count = await db.Branches.CountAsync(b => b.OrganizationId == orgId, ct);
        if (count >= sub.Plan.MaxBranches)
            throw ApiException.Forbidden($"خطتك الحالية (${sub.Plan.Name}) تسمح بـ ${sub.Plan.MaxBranches} فروع فقط. رقِّ خطتك لإضافة فروع.");
    }

    public async Task EnsureUserLimitAsync(Guid orgId, CancellationToken ct = default)
    {
        var sub = await db.Subscriptions.AsNoTracking().Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.OrganizationId == orgId, ct);
        if (sub?.Plan is null) return;

        var count = await db.OrganizationMembers.CountAsync(m => m.OrganizationId == orgId, ct);
        if (count >= sub.Plan.MaxUsers)
            throw ApiException.Forbidden($"خطتك الحالية (${sub.Plan.Name}) تسمح بـ ${sub.Plan.MaxUsers} مستخدمين فقط.");
    }
}
