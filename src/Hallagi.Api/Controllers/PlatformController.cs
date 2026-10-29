using Microsoft.AspNetCore.Mvc;

namespace Hallagi.Api.Controllers;

/// <summary>Platform owner console: manages every tenant, plan, and branch.</summary>
[Route("api/platform")]
public class PlatformController(PlatformService platform, SubscriptionService subscriptions) : BaseApiController
{
    private void EnsurePlatformOwner()
    {
        if (CurrentUser.Role != UserRole.SuperAdmin)
            throw AppException.Forbidden("هذه الصفحة مخصصة لمالك المنصة فقط");
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        EnsurePlatformOwner();
        return Ok(await platform.GetStatsAsync(ct));
    }

    [HttpGet("organizations")]
    public async Task<IActionResult> Organizations([FromQuery] string? search, CancellationToken ct = default)
    {
        EnsurePlatformOwner();
        return Ok(await platform.ListOrganizationsAsync(search, ct));
    }

    [HttpPost("organizations")]
    public async Task<IActionResult> CreateOrganization([FromBody] CreateOrganizationRequest req, CancellationToken ct)
    {
        EnsurePlatformOwner();
        var id = await platform.CreateOrganizationAsync(req, CurrentUser, ct);
        return Ok(new { id, message = "تم إنشاء المنشأة وحساب مالكها بنجاح" });
    }

    [HttpPost("organizations/{id:guid}/branches")]
    public async Task<IActionResult> AddBranch(Guid id, [FromBody] AddBranchDto dto, CancellationToken ct)
    {
        EnsurePlatformOwner();
        await platform.AddBranchAsync(id, dto.Name, dto.City, CurrentUser, ct);
        return Ok(new { message = "تمت إضافة الفرع بنجاح" });
    }

    [HttpPost("organizations/{id:guid}/plan")]
    public async Task<IActionResult> ChangePlan(Guid id, [FromBody] ChangePlanDto dto, CancellationToken ct)
    {
        EnsurePlatformOwner();
        await platform.ChangePlanAsync(id, dto.PlanId, CurrentUser, ct);
        return Ok(new { message = "تم تغيير خطة المنشأة" });
    }

    [HttpPost("organizations/{id:guid}/subscription-status")]
    public async Task<IActionResult> SubscriptionStatus(Guid id, [FromBody] SubscriptionStatusDto dto, CancellationToken ct)
    {
        EnsurePlatformOwner();
        await platform.SetSubscriptionStatusAsync(id, dto.Status, CurrentUser, ct);
        return Ok(new { message = "تم تحديث حالة الاشتراك" });
    }

    [HttpPost("organizations/{id:guid}/owner-password")]
    public async Task<IActionResult> ResetOwnerPassword(Guid id, [FromBody] PlatformResetPasswordDto dto, CancellationToken ct)
    {
        EnsurePlatformOwner();
        await platform.ResetOwnerPasswordAsync(id, dto.NewPassword, CurrentUser, ct);
        return Ok(new { message = "تمت إعادة تعيين كلمة مرور المالك" });
    }

    [HttpGet("plans")]
    public async Task<IActionResult> Plans(CancellationToken ct)
    {
        EnsurePlatformOwner();
        return Ok(await subscriptions.ListPlansAsync(ct));
    }
}

public record AddBranchDto(string Name, string City);
public record SubscriptionStatusDto(string Status);
public record PlatformResetPasswordDto(string NewPassword);
