using Hallagi.Application.Catalog;
using Hallagi.Application.Reports;

namespace Hallagi.Application.Identity;

/// <summary>"Who am I": profile, organization branding, reachable branches and effective permissions.</summary>
public class SessionService(IAppDbContext db, CatalogService catalog, DashboardService dashboard)
{
    public async Task<object> GetMeAsync(CurrentUser user, CancellationToken ct = default)
    {
        var profile = await db.Profiles.AsNoTracking().FirstAsync(p => p.Id == user.ProfileId, ct);
        var profileView = new { profile.Id, profile.FullName, profile.Email, profile.Phone };

        // Platform owner: no organization context
        if (user.Role == UserRole.SuperAdmin)
        {
            return new
            {
                profile = profileView,
                role = UserRole.SuperAdmin.ToString(),
                organization = (object?)null,
                branchIds = Array.Empty<Guid>(),
                branches = Array.Empty<object>(),
                allBranches = true,
                isSuperAdmin = true,
                permissions = Permissions.EffectivePermissions(user)
            };
        }

        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == user.OrganizationId, ct);
        var branches = Permissions.AccessesAllBranches(user)
            ? await db.Branches.AsNoTracking().Where(b => b.OrganizationId == user.OrganizationId && b.Status == EntityStatus.Active)
                .Select(b => new { b.Id, b.Name, b.City }).ToListAsync(ct)
            : await (from a in db.MemberBranchAccess
                     join br in db.Branches on a.BranchId equals br.Id
                     where a.MemberId == user.MemberId && br.Status == EntityStatus.Active
                     select new { br.Id, br.Name, br.City }).ToListAsync(ct);

        return new
        {
            profile = profileView,
            role = user.Role.ToString(),
            organization = new { org.Id, org.Name, org.Currency, org.TimeZone, org.LogoUrl, org.PrimaryColor, org.SecondaryColor, org.CoverImageUrl, org.Slug },
            branchIds = user.BranchIds,
            branches,
            allBranches = Permissions.AccessesAllBranches(user),
            isSuperAdmin = false,
            permissions = Permissions.EffectivePermissions(user)
        };
    }

    /// <summary>Role-aware summary used right after login to pick the landing page.</summary>
    public async Task<object> GetSummaryAsync(CurrentUser user, CancellationToken ct = default)
    {
        var linkedBarber = await db.Barbers.AsNoTracking()
            .Include(b => b.BranchAssignments)
            .FirstOrDefaultAsync(b => b.OrganizationId == user.OrganizationId && b.LinkedProfileId == user.ProfileId, ct);

        object? barber = null;
        if (linkedBarber is not null)
        {
            barber = Permissions.Can(user, Permissions.BarberView)
                ? await catalog.GetBarberAsync(user, linkedBarber.Id, null, null, ct)
                // A barber may always read HIS OWN record; other data stays restricted
                : await catalog.GetOwnBarberAsync(user, linkedBarber.Id, Permissions.Can(user, Permissions.BarberDuesView), ct);
        }

        // A barber's branches come from his barber assignments (he has no member rows)
        var branchIds = user.Role == UserRole.Barber && linkedBarber is not null
            ? linkedBarber.BranchAssignments.Select(a => a.BranchId).Distinct().ToList()
            : await dashboard.ResolveBranchIdsAsync(user, null, ct);

        return new
        {
            role = user.Role.ToString(),
            roleText = UsersService.RoleText(user.Role),
            branchIds,
            barber,
            canViewDues = Permissions.Can(user, Permissions.BarberDuesView)
        };
    }
}
