namespace Hallagi.Api.Middleware;

/// <summary>Baseline browser hardening headers for every API response.</summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        // JSON API: nothing to render, so lock the document context down entirely
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        return next(context);
    }
}
