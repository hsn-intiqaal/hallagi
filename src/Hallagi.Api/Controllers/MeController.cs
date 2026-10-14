using Hallagi.Api.Data;
using Hallagi.Api.Domain;
using Hallagi.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hallagi.Api.Controllers;

[Route("api/me")]
public class MeController(AppDbContext db, CatalogService catalog, DashboardService dash) : BaseApiController
{
    /// <summary>Role-aware summary used right after login to pick the landing page.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct = default)
    {
        var user = CurrentUser;
        var linkedBarber = await db.Barbers.AsNoTracking()
            .Include(b => b.BranchAssignments)
            .FirstOrDefaultAsync(b => b.OrganizationId == user.OrganizationId && b.LinkedProfileId == user.ProfileId, ct);

        object? barber = null;
        if (linkedBarber is not null)
        {
            if (!Permissions.Can(user, Permissions.BarberView))
            {
                // A barber may always read HIS OWN record; other data stays restricted
                barber = await catalog.GetOwnBarberAsync(user, linkedBarber.Id, Permissions.Can(user, Permissions.BarberDuesView), ct);
            }
            else
            {
                barber = await catalog.GetBarberAsync(user, linkedBarber.Id, DateTime.Today.AddDays(-30), null, ct);
            }
        }

        List<Guid> branchIds;
        if (user.Role == UserRole.Barber && linkedBarber is not null)
        {
            // A barber's branches come from his barber assignments (he has no member rows)
            branchIds = await db.BarberBranchAssignments.AsNoTracking()
                .Where(a => a.BarberId == linkedBarber.Id)
                .Select(a => a.BranchId)
                .Distinct()
                .ToListAsync(ct);
        }
        else
        {
            branchIds = await dash.ResolveBranchIdsAsync(user, null, ct);
        }

        return Ok(new
        {
            role = user.Role.ToString(),
            roleText = UsersService.RoleText(user.Role),
            branchIds,
            barber,
            canViewDues = Permissions.Can(user, Permissions.BarberDuesView)
        });
    }
}
