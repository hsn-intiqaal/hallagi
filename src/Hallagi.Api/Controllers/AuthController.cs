using Hallagi.Api.Data;
using Hallagi.Api.Domain;
using Hallagi.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hallagi.Api.Controllers;

public record LoginRequest(string Email, string Password);
public record RegisterOwnerRequest(
    string FullName,
    string Email,
    string Password,
    string? Phone,
    string OrganizationName,
    string City,
    string BranchName);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService auth, AppDbContext db) : BaseApiController
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = new { code = "missing_fields", message = "أدخل البريد الإلكتروني وكلمة المرور" } });

        var (ok, error, result) = await auth.LoginAsync(req.Email, req.Password, ct);
        if (!ok)
        {
            var message = error switch
            {
                "invalid_credentials" => "البريد الإلكتروني أو كلمة المرور غير صحيحة",
                "account_disabled" => "هذا الحساب معطّل. تواصل مع مالك المنشأة",
                "no_active_membership" => "لا يوجد اشتراك فعّال لهذا الحساب",
                _ => "تعذّر تسجيل الدخول. حاول مرة أخرى"
            };
            return Unauthorized(new { error = new { code = error, message } });
        }
        return Ok(result);
    }

    [HttpPost("register-owner")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterOwner([FromBody] RegisterOwnerRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.OrganizationName) || string.IsNullOrWhiteSpace(req.City) ||
            string.IsNullOrWhiteSpace(req.BranchName))
            return BadRequest(new { error = new { code = "missing_fields", message = "أكمل الحقول المطلوبة: الاسم، البريد، اسم المحل، المدينة، الفرع" } });
        if (req.Password.Length < 6)
            return BadRequest(new { error = new { code = "weak_password", message = "كلمة المرور يجب أن تكون 6 أحرف على الأقل" } });

        var (ok, error, result) = await auth.RegisterOwnerAsync(
            req.FullName, req.Email, req.Password, req.Phone, req.OrganizationName, req.City, req.BranchName, ct);
        if (!ok)
        {
            var message = error switch
            {
                "email_taken" => "البريد الإلكتروني مستخدم بالفعل",
                _ => "تعذّر إنشاء الحساب. تحقق من البيانات"
            };
            return Conflict(new { error = new { code = error, message } });
        }
        return Ok(result);
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = CurrentUser;
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == user.ProfileId, ct);
        var member = await db.OrganizationMembers.FirstOrDefaultAsync(m => m.Id == user.MemberId, ct);
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == user.OrganizationId, ct);
        var branches = Permissions.AccessesAllBranches(user)
            ? await db.Branches.Where(b => b.OrganizationId == user.OrganizationId && b.Status == EntityStatus.Active)
                .Select(b => new { b.Id, b.Name, b.City }).ToListAsync(ct)
            : await (from a in db.MemberBranchAccess
                     join br in db.Branches on a.BranchId equals br.Id
                     where a.MemberId == user.MemberId && br.Status == EntityStatus.Active
                     select new { br.Id, br.Name, br.City }).ToListAsync(ct);

        return Ok(new
        {
            profile = new { profile!.Id, profile.FullName, profile.Email, profile.Phone },
            role = member!.Role.ToString(),
            organization = new { org!.Id, org.Name, org.Currency, org.TimeZone, org.LogoUrl },
            branchIds = user.BranchIds,
            branches,
            allBranches = Permissions.AccessesAllBranches(user),
            permissions = Permissions.EffectivePermissions(user)
        });
    }

    [HttpPost("change-password")]
    public IActionResult ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var (ok, error) = auth.ChangePassword(CurrentUser.ProfileId, req.CurrentPassword, req.NewPassword);
        if (!ok)
        {
            var message = error switch
            {
                "wrong_current_password" => "كلمة المرور الحالية غير صحيحة",
                "weak_password" => "كلمة المرور الجديدة يجب أن تكون 6 أحرف على الأقل",
                _ => "تعذّر تغيير كلمة المرور"
            };
            return BadRequest(new { error = new { code = error, message } });
        }
        return Ok(new { message = "تم تغيير كلمة المرور بنجاح" });
    }
}
