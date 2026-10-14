using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hallagi.Api.Data;
using Hallagi.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Hallagi.Api.Services;

public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Hallagi";
    public string Audience { get; set; } = "Hallagi";
    public int ExpiryMinutes { get; set; } = 720;
}

public class AuthService(AppDbContext db, JwtSettings jwt)
{
    public async Task<(bool ok, string? error, object? result)> LoginAsync(string email, string password, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Email == normalized, ct);
        if (profile is null) return (false, "invalid_credentials", null);
        if (profile.Status != EntityStatus.Active) return (false, "account_disabled", null);

        if (!BCrypt.Net.BCrypt.Verify(password, profile.PasswordHash))
            return (false, "invalid_credentials", null);

        // Platform owner: no organization membership required
        if (profile.IsSuperAdmin)
        {
            profile.LastLoginAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            var token = IssueSuperAdminToken(profile);
            return (true, null, new
            {
                token,
                profile = new { profile.Id, profile.FullName, profile.Email, profile.Phone },
                member = new { id = (Guid?)null, role = UserRole.SuperAdmin.ToString(), organizationId = (Guid?)null, organizationName = "منصة حلاقي" },
                branches = Array.Empty<object>(),
                expiresInMinutes = jwt.ExpiryMinutes
            });
        }

        var member = await db.OrganizationMembers
            .Where(m => m.ProfileId == profile.Id && m.Status == MemberStatus.Active)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (member is null) return (false, "no_active_membership", null);

        profile.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var memberToken = IssueToken(profile, member);
        return (true, null, await BuildLoginResultAsync(profile, member, memberToken, ct));
    }

    /// <summary>
    /// Public self-service registration is disabled: new tenants are created only
    /// by the platform owner through the admin console (see PlatformService).
    /// </summary>
    public string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public (bool ok, string? error) ChangePassword(Guid profileId, string currentPassword, string newPassword)
    {
        var profile = db.Profiles.Find(profileId);
        if (profile is null) return (false, "not_found");
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, profile.PasswordHash))
            return (false, "wrong_current_password");
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return (false, "weak_password");
        profile.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        profile.UpdatedAt = DateTime.UtcNow;
        db.SaveChanges();
        return (true, null);
    }

    private string IssueToken(Profile profile, OrganizationMember member)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, profile.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("member", member.Id.ToString()),
            new("org", member.OrganizationId.ToString()),
            new(ClaimTypes.Role, member.Role.ToString()),
            new("name", profile.FullName),
            new("email", profile.Email)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(jwt.ExpiryMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Token for the platform owner: superadmin role, no organization context.</summary>
    private string IssueSuperAdminToken(Profile profile)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, profile.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("superadmin", "true"),
            new(ClaimTypes.Role, UserRole.SuperAdmin.ToString()),
            new("name", profile.FullName),
            new("email", profile.Email)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(jwt.ExpiryMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<object> BuildLoginResultAsync(Profile profile, OrganizationMember member, string token, CancellationToken ct)
    {
        var org = await db.Organizations.FindAsync(new object[] { member.OrganizationId }, ct);
        var branches = member.Role == UserRole.Owner
            ? await db.Branches.Where(x => x.OrganizationId == org!.Id && x.Status == EntityStatus.Active)
                .Select(x => new { x.Id, x.Name, x.City }).ToListAsync(ct)
            : await (from a in db.MemberBranchAccess
                     join br in db.Branches on a.BranchId equals br.Id
                     where a.MemberId == member.Id && br.Status == EntityStatus.Active
                     select new { br.Id, br.Name, br.City }).ToListAsync(ct);

        return new
        {
            token,
            profile = new { profile.Id, profile.FullName, profile.Email, profile.Phone },
            member = new { member.Id, Role = member.Role.ToString(), member.OrganizationId, OrganizationName = org?.Name },
            branches,
            expiresInMinutes = jwt.ExpiryMinutes
        };
    }
}
