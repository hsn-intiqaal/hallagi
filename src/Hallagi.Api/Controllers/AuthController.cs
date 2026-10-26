using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hallagi.Api.Controllers;

public record LoginRequest(string Email, string Password);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record RefreshRequest(string RefreshToken);

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService auth) : BaseApiController
{
    [HttpPost("login")]
    [AllowAnonymous]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Hallagi.Api.Extensions.ServiceCollectionExtensions.AuthRateLimit)]
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
                "account_disabled" => "هذا الحساب معطّل. تواصل مع الدعم",
                "no_active_membership" => "لا يوجد اشتراك فعّال لهذا الحساب",
                _ => "تعذّر تسجيل الدخول. حاول مرة أخرى"
            };
            return Unauthorized(new { error = new { code = error, message } });
        }
        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Hallagi.Api.Extensions.ServiceCollectionExtensions.AuthRateLimit)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var (ok, error, result) = await auth.RefreshAsync(req.RefreshToken, ct);
        if (!ok)
            return Unauthorized(new { error = new { code = error, message = "انتهت جلسة الدخول. سجّل الدخول مرة أخرى" } });
        return Ok(result);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req, CancellationToken ct)
    {
        await auth.LogoutAsync(req.RefreshToken, ct);
        return Ok(new { message = "تم تسجيل الخروج" });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me([FromServices] SessionService session, CancellationToken ct)
        => Ok(await session.GetMeAsync(CurrentUser, ct));

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
