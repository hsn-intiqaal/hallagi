using System.Text.Json;
using System.Text.RegularExpressions;
using Hallagi.Api.Data;
using Hallagi.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hallagi.Api.Services;

/// <summary>Validation + normalization for tenant branding (colors and images).</summary>
public static class Branding
{
    private static readonly Regex HexColor = new("^#([0-9a-fA-F]{6})$", RegexOptions.Compiled);
    public const int MaxImageChars = 1_500_000;

    /// <summary>Returns a valid #RRGGBB color, or the fallback when the value is missing/invalid.</summary>
    public static string NormalizeColor(string? value, string fallback)
        => !string.IsNullOrWhiteSpace(value) && HexColor.IsMatch(value.Trim()) ? value.Trim() : fallback;

    /// <summary>Accepts only inline image data URLs within the size cap; everything else is dropped.</summary>
    public static string? NormalizeImage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (v.Length > MaxImageChars)
            throw ApiException.Bad("image_too_large", "حجم الصورة كبير جدًا (الحد الأقصى 1.5 ميجابايت)");
        return v.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) ? v : null;
    }
}

/// <summary>
/// Platform administration (حساب مالك المنصة): manages every tenant, its
/// subscription, branches, and owner accounts. No membership is created for the
/// platform owner himself.
/// </summary>
public class PlatformService(AppDbContext db, AuthService auth, SubscriptionService subscriptions)
{
    public async Task<object> GetStatsAsync(CancellationToken ct = default)
    {
        var orgs = await db.Organizations.AsNoTracking().ToListAsync(ct);
        var orgIds = orgs.Select(o => o.Id).ToList();

        return new
        {
            organizationsCount = orgs.Count,
            branchesCount = await db.Branches.CountAsync(b => orgIds.Contains(b.OrganizationId), ct),
            usersCount = await db.Profiles.CountAsync(p => !p.IsSuperAdmin, ct),
            barbersCount = await db.Barbers.CountAsync(b => orgIds.Contains(b.OrganizationId), ct),
            salesCount = await db.Sales.CountAsync(s => orgIds.Contains(s.OrganizationId), ct),
            salesTotal = await db.Sales.Where(s => orgIds.Contains(s.OrganizationId) && s.Status == SaleStatus.Completed)
                .SumAsync(s => (decimal?)s.TotalAmount, ct) ?? 0m,
            activeSubscriptions = await db.Subscriptions.CountAsync(s => orgIds.Contains(s.OrganizationId) && s.Status == "Active", ct)
        };
    }

    public async Task<object> ListOrganizationsAsync(string? search, CancellationToken ct = default)
    {
        await subscriptions.EnsureSeededAsync(ct);
        var query = db.Organizations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(o => o.Name.Contains(s));
        }

        var orgs = await query.OrderBy(o => o.Name).ToListAsync(ct);

        var items = new List<object>();
        foreach (var org in orgs)
        {
            var sub = await db.Subscriptions.AsNoTracking().Include(s => s.Plan).FirstOrDefaultAsync(s => s.OrganizationId == org.Id, ct);
            var owner = await (from m in db.OrganizationMembers
                               join p in db.Profiles on m.ProfileId equals p.Id
                               where m.OrganizationId == org.Id && m.Role == UserRole.Owner && m.Status == MemberStatus.Active
                               select new { p.FullName, p.Email }).FirstOrDefaultAsync(ct);

            items.Add(new
            {
                org.Id,
                org.Name,
                org.Currency,
                org.TimeZone,
                org.LogoUrl,
                org.PrimaryColor,
                org.SecondaryColor,
                org.CoverImageUrl,
                branchesCount = await db.Branches.CountAsync(b => b.OrganizationId == org.Id, ct),
                usersCount = await db.OrganizationMembers.CountAsync(m => m.OrganizationId == org.Id, ct),
                barbersCount = await db.Barbers.CountAsync(b => b.OrganizationId == org.Id, ct),
                salesTotal = await db.Sales.Where(s => s.OrganizationId == org.Id && s.Status == SaleStatus.Completed)
                    .SumAsync(s => (decimal?)s.TotalAmount, ct) ?? 0m,
                ownerName = owner?.FullName,
                ownerEmail = owner?.Email,
                planName = sub?.Plan?.Name,
                planId = sub?.PlanId,
                subscriptionStatus = sub?.Status,
                createdAt = org.CreatedAt
            });
        }
        return new { items };
    }

    /// <summary>Creates a tenant with its first branch, owner account, and subscription.</summary>
    public async Task<Guid> CreateOrganizationAsync(CreateOrganizationRequest req, CurrentUser admin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw ApiException.Bad("name_required", "اسم المنشأة مطلوب");
        if (string.IsNullOrWhiteSpace(req.City)) throw ApiException.Bad("city_required", "المدينة مطلوبة");
        if (string.IsNullOrWhiteSpace(req.OwnerEmail)) throw ApiException.Bad("email_required", "بريد المالك مطلوب");
        if (string.IsNullOrWhiteSpace(req.OwnerPassword) || req.OwnerPassword.Length < 6)
            throw ApiException.Bad("weak_password", "كلمة مرور المالك يجب أن تكون 6 أحرف على الأقل");

        var normalized = req.OwnerEmail.Trim().ToLowerInvariant();
        if (await db.Profiles.AnyAsync(p => p.Email == normalized, ct))
            throw ApiException.Conflict("email_taken", "البريد الإلكتروني مستخدم بالفعل");

        var planId = req.PlanId;
        if (planId is null)
        {
            planId = await db.SubscriptionPlans.AsNoTracking().OrderBy(p => p.MonthlyPrice).Select(p => p.Id).FirstAsync(ct);
        }
        else if (!await db.SubscriptionPlans.AnyAsync(p => p.Id == planId, ct))
        {
            throw ApiException.Bad("invalid_plan", "الخطة غير موجودة");
        }

        var now = DateTime.UtcNow;
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = req.Name.Trim(),
            Currency = "SAR",
            TimeZone = "Asia/Riyadh",
            LogoUrl = Branding.NormalizeImage(req.LogoUrl),
            CoverImageUrl = Branding.NormalizeImage(req.CoverImageUrl),
            PrimaryColor = Branding.NormalizeColor(req.PrimaryColor, "#2563EB"),
            SecondaryColor = Branding.NormalizeColor(req.SecondaryColor, "#173653"),
            Slug = await Slugs.RandomUniqueAsync(db, ct),
            CreatedAt = now,
            UpdatedAt = now,
            Settings = new OrganizationSettings { EnableCash = true, EnableCard = true }
        };
        db.Organizations.Add(org);

        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Name = (req.BranchName?.Trim().Length > 0 ? req.BranchName!.Trim() : "الفرع الرئيسي"),
            City = req.City.Trim(),
            Address = req.Address?.Trim(),
            Status = EntityStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            Drawer = new CashDrawer { OrganizationId = org.Id, CreatedAt = now }
        };
        db.Branches.Add(branch);

        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            FullName = req.OwnerName?.Trim().Length > 0 ? req.OwnerName!.Trim() : "مالك المنشأة",
            Email = normalized,
            Phone = req.OwnerPhone?.Trim(),
            PasswordHash = auth.HashPassword(req.OwnerPassword),
            Status = EntityStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Profiles.Add(profile);

        var member = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            ProfileId = profile.Id,
            Role = UserRole.Owner,
            Status = MemberStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.OrganizationMembers.Add(member);

        db.MemberBranchAccess.Add(new MemberBranchAccess { Id = Guid.NewGuid(), MemberId = member.Id, BranchId = branch.Id });

        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            PlanId = planId.Value,
            Status = "Active",
            PeriodStart = DateOnly.FromDateTime(DateTime.UtcNow),
            PeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            CreatedAt = now,
            UpdatedAt = now
        });

        // basic service catalog so the new tenant can sell immediately
        foreach (var (name, price) in new[]
        {
            ("حلاقة شعر", 25m), ("حلاقة ذقن", 15m), ("شعر وذقن", 35m),
            ("تنظيف بشرة", 30m), ("صبغة شعر", 50m), ("حلاقة أطفال", 20m)
        })
        {
            db.Services.Add(new Service
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                Name = name,
                BasePrice = price,
                Status = EntityStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            ActorId = admin.ProfileId,
            Action = "platform.organization_created",
            EntityType = nameof(Organization),
            EntityId = org.Id.ToString(),
            Details = $"owner={normalized}; plan={planId}",
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
        return org.Id;
    }

    public async Task ChangePlanAsync(Guid orgId, Guid planId, CurrentUser admin, CancellationToken ct = default)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct)
            ?? throw ApiException.NotFound("المنشأة غير موجودة");
        await subscriptions.ChangePlanAsync(orgId, planId, admin, ct);

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ActorId = admin.ProfileId,
            Action = "platform.plan_changed",
            EntityType = nameof(Subscription),
            EntityId = orgId.ToString(),
            Details = $"plan={planId}",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task AddBranchAsync(Guid orgId, string name, string city, CurrentUser admin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw ApiException.Bad("name_required", "اسم الفرع مطلوب");
        if (string.IsNullOrWhiteSpace(city)) throw ApiException.Bad("city_required", "المدينة مطلوبة");

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct)
            ?? throw ApiException.NotFound("المنشأة غير موجودة");

        var now = DateTime.UtcNow;
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Name = name.Trim(),
            City = city.Trim(),
            Status = EntityStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            Drawer = new CashDrawer { OrganizationId = orgId, CreatedAt = now }
        };
        db.Branches.Add(branch);

        // give the tenant owner access to the new branch
        var ownerMember = await db.OrganizationMembers.FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.Role == UserRole.Owner, ct);
        if (ownerMember is not null)
            db.MemberBranchAccess.Add(new MemberBranchAccess { Id = Guid.NewGuid(), MemberId = ownerMember.Id, BranchId = branch.Id });

        // duplicate the org services with base prices for the new branch
        var services = await db.Services.Where(s => s.OrganizationId == orgId).ToListAsync(ct);
        foreach (var svc in services)
            db.BranchServicePrices.Add(new BranchServicePrice { Id = Guid.NewGuid(), ServiceId = svc.Id, BranchId = branch.Id, Price = svc.BasePrice });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ActorId = admin.ProfileId,
            Action = "platform.branch_created",
            EntityType = nameof(Branch),
            EntityId = branch.Id.ToString(),
            Details = $"{branch.Name} — {branch.City}",
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task SetSubscriptionStatusAsync(Guid orgId, string status, CurrentUser admin, CancellationToken ct = default)
    {
        var sub = await db.Subscriptions.FirstOrDefaultAsync(s => s.OrganizationId == orgId, ct)
            ?? throw ApiException.NotFound("لا يوجد اشتراك لهذه المنشأة");
        if (status is not ("Active" or "Suspended" or "Cancelled"))
            throw ApiException.Bad("invalid_status", "حالة غير صحيحة");

        sub.Status = status;
        sub.UpdatedAt = DateTime.UtcNow;

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ActorId = admin.ProfileId,
            Action = "platform.subscription_status",
            EntityType = nameof(Subscription),
            EntityId = sub.Id.ToString(),
            Details = $"status={status}",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task ResetOwnerPasswordAsync(Guid orgId, string newPassword, CurrentUser admin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            throw ApiException.Bad("weak_password", "كلمة المرور يجب أن تكون 6 أحرف على الأقل");

        var owner = await (from m in db.OrganizationMembers
                           join p in db.Profiles on m.ProfileId equals p.Id
                           where m.OrganizationId == orgId && m.Role == UserRole.Owner
                           select p).FirstOrDefaultAsync(ct)
            ?? throw ApiException.NotFound("لا يوجد مالك لهذه المنشأة");

        owner.PasswordHash = auth.HashPassword(newPassword);
        foreach (var t in db.RefreshTokens.Where(t => t.ProfileId == owner.Id && t.RevokedAt == null)) t.RevokedAt = DateTime.UtcNow;
        owner.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}

public record CreateOrganizationRequest(
    string Name,
    string City,
    string? BranchName,
    string? Address,
    string OwnerName,
    string OwnerEmail,
    string? OwnerPhone,
    string OwnerPassword,
    Guid? PlanId,
    string? LogoUrl = null,
    string? CoverImageUrl = null,
    string? PrimaryColor = null,
    string? SecondaryColor = null);
