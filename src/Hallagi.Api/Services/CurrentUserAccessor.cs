using Hallagi.Api.Data;
using Hallagi.Api.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Hallagi.Api.Services;

public interface ICurrentUserAccessor
{
    CurrentUser User { get; }
    bool IsAuthenticated { get; }
}

/// <summary>Builds the CurrentUser from claims and re-validates against the database.</summary>
public class CurrentUserAccessor(IHttpContextAccessor accessor, AppDbContext db) : ICurrentUserAccessor
{
    public bool IsAuthenticated => accessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public CurrentUser User
    {
        get
        {
            var http = accessor.HttpContext ?? throw new UnauthorizedAccessException("no_http_context");
            var principal = http.User;

            var subClaim = principal.FindFirst("sub")?.Value
                ?? principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var memberClaim = principal.FindFirst("member")?.Value;
            var orgClaim = principal.FindFirst("org")?.Value;

            if (subClaim is null || !Guid.TryParse(subClaim, out var profileId))
                throw new UnauthorizedAccessException("invalid_token");

            var profile = db.Profiles.AsNoTracking().FirstOrDefault(p => p.Id == profileId)
                ?? throw new UnauthorizedAccessException("profile_not_found");

            // Platform owner: no organization membership
            if (profile.IsSuperAdmin)
            {
                if (principal.FindFirst("superadmin")?.Value != "true")
                    throw new UnauthorizedAccessException("invalid_token");
                return new CurrentUser(profileId, Guid.Empty, Guid.Empty, UserRole.SuperAdmin, true, Array.Empty<Guid>(), null);
            }

            if (profile.Status != EntityStatus.Active)
                throw new UnauthorizedAccessException("account_disabled");

            if (memberClaim is null || orgClaim is null ||
                !Guid.TryParse(memberClaim, out var memberId) ||
                !Guid.TryParse(orgClaim, out var orgId))
                throw new UnauthorizedAccessException("invalid_token");

            var member = db.OrganizationMembers.AsNoTracking()
                .FirstOrDefault(m => m.Id == memberId && m.ProfileId == profileId && m.OrganizationId == orgId)
                ?? throw new UnauthorizedAccessException("member_not_found");

            if (member.Status != MemberStatus.Active)
                throw new UnauthorizedAccessException("member_disabled");

            List<Guid> branchIds;
            if (member.Role == UserRole.Owner)
            {
                branchIds = db.Branches.Where(x => x.OrganizationId == orgId && x.Status == EntityStatus.Active)
                    .Select(x => x.Id).ToList();
            }
            else
            {
                branchIds = db.MemberBranchAccess.Where(a => a.MemberId == memberId)
                    .Select(a => a.BranchId).ToList();
            }

            return new CurrentUser(
                profileId, orgId, memberId, member.Role, member.Status == MemberStatus.Active,
                branchIds, Permissions.ParseOverrides(member.PermissionsJson));
        }
    }
}
