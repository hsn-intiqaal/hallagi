using System.Text.Json;
using Hallagi.Api.Data;
using Hallagi.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hallagi.Api.Services;

public class UsersService(AppDbContext db, AuthService auth, SubscriptionService subscriptions)
{
    public async Task<object> ListAsync(CurrentUser user, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.UserManage))
            throw ApiException.Forbidden("ليس لديك صلاحية لإدارة المستخدمين");

        var members = await db.OrganizationMembers.AsNoTracking()
            .Where(m => m.OrganizationId == user.OrganizationId)
            .ToListAsync(ct);

        var profiles = await db.Profiles.AsNoTracking()
            .Where(p => members.Select(m => m.ProfileId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var branchNames = await db.Branches.AsNoTracking()
            .Where(b => b.OrganizationId == user.OrganizationId)
            .ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        var access = await db.MemberBranchAccess.AsNoTracking().ToListAsync(ct);
        var activeOwners = members.Count(m => m.Role == UserRole.Owner && m.Status == MemberStatus.Active);

        var items = members.Select(m =>
        {
            var profile = profiles.GetValueOrDefault(m.ProfileId);
            var branches = access.Where(a => a.MemberId == m.Id)
                .Select(a => branchNames.GetValueOrDefault(a.BranchId) ?? "-").ToList();
            return new
            {
                m.Id,
                m.ProfileId,
                fullName = profile?.FullName ?? "-",
                email = profile?.Email ?? "-",
                phone = profile?.Phone,
                role = m.Role.ToString(),
                roleText = RoleText(m.Role),
                status = m.Status.ToString(),
                statusText = m.Status == MemberStatus.Active ? "مفعّل" : "معطّل",
                branches,
                branchIds = access.Where(a => a.MemberId == m.Id).Select(a => a.BranchId).ToList(),
                permissions = m.PermissionsJson is null ? null : Permissions.ParseOverrides(m.PermissionsJson),
                lastActiveOwners = activeOwners
            };
        }).OrderBy(x => x.fullName).ToList();

        return new { items, activeOwners };
    }

    public async Task InviteAsync(CurrentUser user, InviteUserRequest req, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.UserManage))
            throw ApiException.Forbidden("ليس لديك صلاحية لإدارة المستخدمين");
        if (string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.Email))
            throw ApiException.Bad("missing_fields", "الاسم والبريد الإلكتروني مطلوبان");
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            throw ApiException.Bad("weak_password", "كلمة المرور يجب أن تكون 6 أحرف على الأقل");
        if (req.Role == UserRole.Owner && (req.BranchIds.Count == 0))
            throw ApiException.Bad("owner_needs_branch", "المالك يجب أن يكون لديه فرع واحد على الأقل");

        await subscriptions.EnsureUserLimitAsync(user.OrganizationId, ct);

        var normalized = req.Email.Trim().ToLowerInvariant();
        if (await db.Profiles.AnyAsync(p => p.Email == normalized, ct))
            throw ApiException.Conflict("email_taken", "البريد الإلكتروني مستخدم بالفعل");

        foreach (var br in req.BranchIds)
        {
            var exists = await db.Branches.AnyAsync(b => b.Id == br && b.OrganizationId == user.OrganizationId, ct);
            if (!exists) throw ApiException.Bad("invalid_branch", "أحد الفروع غير موجود");
        }

        var now = DateTime.UtcNow;
        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            FullName = req.FullName.Trim(),
            Email = normalized,
            Phone = req.Phone?.Trim(),
            PasswordHash = auth.HashPassword(req.Password),
            Status = EntityStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Profiles.Add(profile);

        var member = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ProfileId = profile.Id,
            Role = req.Role,
            Status = MemberStatus.Active,
            PermissionsJson = req.Permissions is { Count: > 0 } ? JsonSerializer.Serialize(req.Permissions) : null,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.OrganizationMembers.Add(member);

        foreach (var br in req.BranchIds.Distinct())
            db.MemberBranchAccess.Add(new MemberBranchAccess { Id = Guid.NewGuid(), MemberId = member.Id, BranchId = br });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "user.invited",
            EntityType = nameof(OrganizationMember),
            EntityId = member.Id.ToString(),
            Details = $"email={normalized}; role={req.Role}",
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CurrentUser user, Guid memberId, UpdateUserRequest req, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.UserManage))
            throw ApiException.Forbidden("ليس لديك صلاحية لإدارة المستخدمين");

        var member = await db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == user.OrganizationId, ct)
            ?? throw ApiException.NotFound("المستخدم غير موجود");

        // Prevent locking the organization out of its last active owner
        var owners = await db.OrganizationMembers.CountAsync(m => m.OrganizationId == user.OrganizationId
            && m.Role == UserRole.Owner && m.Status == MemberStatus.Active, ct);

        var willDemote = member is { Role: UserRole.Owner, Status: MemberStatus.Active }
            && (req.Role != UserRole.Owner || req.Status != MemberStatus.Active);
        if (willDemote && owners <= 1)
            throw ApiException.Conflict("last_owner", "لا يمكن تعطيل أو تخفيض دور آخر مالك فعّال للمنشأة");
        if (req.Role != UserRole.Owner && req.BranchIds.Count == 0)
            throw ApiException.Bad("branch_required", "اختر فرعًا واحدًا على الأقل لهذا المستخدم");

        if (!string.IsNullOrWhiteSpace(req.FullName))
        {
            var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == member.ProfileId, ct);
            if (profile is not null) profile.FullName = req.FullName.Trim();
        }

        member.Role = req.Role;
        member.Status = req.Status;
        member.PermissionsJson = req.Permissions is { Count: > 0 } ? JsonSerializer.Serialize(req.Permissions) : null;
        member.UpdatedAt = DateTime.UtcNow;

        var existing = await db.MemberBranchAccess.Where(a => a.MemberId == memberId).ToListAsync(ct);
        db.MemberBranchAccess.RemoveRange(existing);
        foreach (var br in req.BranchIds.Distinct())
            db.MemberBranchAccess.Add(new MemberBranchAccess { Id = Guid.NewGuid(), MemberId = member.Id, BranchId = br });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "user.updated",
            EntityType = nameof(OrganizationMember),
            EntityId = member.Id.ToString(),
            Details = $"role={req.Role}; status={req.Status}; branches={req.BranchIds.Count}",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task ResetPasswordAsync(CurrentUser user, Guid memberId, string newPassword, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.UserManage))
            throw ApiException.Forbidden("ليس لديك صلاحية لإدارة المستخدمين");
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            throw ApiException.Bad("weak_password", "كلمة المرور يجب أن تكون 6 أحرف على الأقل");

        var member = await db.OrganizationMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == user.OrganizationId, ct)
            ?? throw ApiException.NotFound("المستخدم غير موجود");
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == member.ProfileId, ct)
            ?? throw ApiException.NotFound("الحساب غير موجود");

        profile.PasswordHash = auth.HashPassword(newPassword);
        profile.UpdatedAt = DateTime.UtcNow;
        foreach (var t in db.RefreshTokens.Where(t => t.ProfileId == profile.Id && t.RevokedAt == null)) t.RevokedAt = DateTime.UtcNow;

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "user.password_reset",
            EntityType = nameof(Profile),
            EntityId = profile.Id.ToString(),
            Details = "تمت إعادة تعيين كلمة المرور",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }

    public static string RoleText(UserRole role) => role switch
    {
        UserRole.Owner => "مالك",
        UserRole.BranchManager => "مدير فرع",
        UserRole.Cashier => "كاشير",
        _ => "حلاق"
    };
}

public record InviteUserRequest(string FullName, string Email, string? Phone, string Password, UserRole Role, List<Guid> BranchIds, Dictionary<string, bool>? Permissions);
public record UpdateUserRequest(string? FullName, UserRole Role, MemberStatus Status, List<Guid> BranchIds, Dictionary<string, bool>? Permissions);
