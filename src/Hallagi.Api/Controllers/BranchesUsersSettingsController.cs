using Hallagi.Api.Data;
using Hallagi.Api.Domain;
using Hallagi.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hallagi.Api.Controllers;

[Route("api/branches")]
public class BranchesController(BranchesService branches) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await branches.ListAsync(CurrentUser, ct);
        // Cashiers and barbers only see branches they may access
        if (!Permissions.AccessesAllBranches(CurrentUser))
        {
            return Ok(await FilterAsync(ct));
        }
        return Ok(result);
    }

    private async Task<object> FilterAsync(CancellationToken ct)
    {
        var allowed = CurrentUser.BranchIds;
        var items = await HttpContext.RequestServices.GetRequiredService<AppDbContext>().Branches.AsNoTracking()
            .Where(b => b.OrganizationId == CurrentUser.OrganizationId && allowed.Contains(b.Id))
            .Select(b => new
            {
                b.Id,
                b.Name,
                b.City,
                b.Address,
                b.Status,
                StatusText = b.Status == EntityStatus.Active ? "نشط" : "غير نشط"
            }).ToListAsync(ct);
        return new { items };
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BranchInput input, CancellationToken ct)
        => Ok(new { id = await branches.CreateAsync(CurrentUser, input, ct), message = "تمت إضافة الفرع بنجاح" });

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] BranchInput input, CancellationToken ct)
    {
        await branches.UpdateAsync(CurrentUser, id, input, ct);
        return Ok(new { message = "تم تحديث بيانات الفرع بنجاح" });
    }
}

[Route("api/users")]
public class UsersController(UsersService users) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await users.ListAsync(CurrentUser, ct));

    [HttpPost]
    public async Task<IActionResult> Invite([FromBody] InviteUserRequest req, CancellationToken ct)
    {
        await users.InviteAsync(CurrentUser, req, ct);
        return Ok(new { message = "تم إنشاء المستخدم بنجاح" });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct)
    {
        await users.UpdateAsync(CurrentUser, id, req, ct);
        return Ok(new { message = "تم تحديث المستخدم بنجاح" });
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordDto dto, CancellationToken ct)
    {
        await users.ResetPasswordAsync(CurrentUser, id, dto.NewPassword, ct);
        return Ok(new { message = "تمت إعادة تعيين كلمة المرور" });
    }

    [HttpGet("roles")]
    public IActionResult Roles() => Ok(new[]
    {
        new { value = UserRole.Owner.ToString(), label = UsersService.RoleText(UserRole.Owner) },
        new { value = UserRole.BranchManager.ToString(), label = UsersService.RoleText(UserRole.BranchManager) },
        new { value = UserRole.Cashier.ToString(), label = UsersService.RoleText(UserRole.Cashier) },
        new { value = UserRole.Barber.ToString(), label = UsersService.RoleText(UserRole.Barber) }
    });
}

public record ResetPasswordDto(string NewPassword);

[Route("api/settings")]
public class SettingsController(SettingsService settings) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await settings.GetAsync(CurrentUser, ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateSettingsRequest req, CancellationToken ct)
    {
        await settings.UpdateAsync(CurrentUser, req, ct);
        return Ok(new { message = "تم حفظ الإعدادات بنجاح" });
    }

    [HttpPost("onboarding")]
    public async Task<IActionResult> Onboarding([FromBody] OnboardingDto dto, CancellationToken ct)
    {
        await settings.BulkSeedAsync(CurrentUser, dto.ServiceNames, dto.Barbers, ct);
        return Ok(new { message = "تم حفظ بيانات الإعداد" });
    }
}

public record OnboardingDto(List<string> ServiceNames, List<BarberInput> Barbers);
