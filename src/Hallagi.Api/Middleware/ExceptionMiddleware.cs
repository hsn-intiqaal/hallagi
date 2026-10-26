using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Hallagi.Api.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DbUpdateConcurrencyException)
        {
            await WriteAsync(context, 409, "concurrency", "تم تعديل البيانات من مستخدم آخر. حدّث الصفحة وحاول مرة أخرى");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // e.g. two cashiers grabbing the same receipt number at the same instant
            logger.LogWarning(ex, "Unique constraint violation");
            await WriteAsync(context, 409, "conflict", "تعارض في البيانات بسبب عملية متزامنة. حاول مرة أخرى");
        }
        catch (AppException ex)
        {
            context.Response.StatusCode = ex.Status;
            context.Response.ContentType = "application/json; charset=utf-8";
            var payload = JsonSerializer.Serialize(new
            {
                error = new { code = ex.Code, message = ex.ArabicMessage }
            }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            await context.Response.WriteAsync(payload);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized: {Message}", ex.Message);
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json; charset=utf-8";
            var payload = JsonSerializer.Serialize(new
            {
                error = new { code = "unauthorized", message = "انتهت جلسة الدخول. سجّل الدخول مرة أخرى" }
            }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            await context.Response.WriteAsync(payload);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error {TraceId}", context.TraceIdentifier);
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json; charset=utf-8";
            var payload = JsonSerializer.Serialize(new
            {
                error = new { code = "server_error", message = "حدث خطأ غير متوقع. حاول مرة أخرى", traceId = context.TraceIdentifier }
            }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            await context.Response.WriteAsync(payload);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? "";
        return msg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteAsync(HttpContext context, int status, string code, string message)
    {
        if (context.Response.HasStarted) return;
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = new { code, message } },
            new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }
}
