using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Hallagi.Api.Security;
using Hallagi.Infrastructure.Identity;
using Hallagi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;

namespace Hallagi.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public const string CorsPolicy = "AngularClient";
    public const string AuthRateLimit = "auth";
    public const string PublicRateLimit = "public";

    private const string DevJwtKey = "dev-only-key-change-in-production-0123456789";

    /// <summary>Reads and validates JWT settings; production refuses weak or default keys.</summary>
    public static JwtSettings GetJwtSettings(this IConfiguration configuration, IHostEnvironment env)
    {
        var jwt = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
        if (string.IsNullOrWhiteSpace(jwt.Key))
            throw new InvalidOperationException("Jwt:Key is required (set Jwt__Key environment variable)");
        if (!env.IsDevelopment() && (jwt.Key.Length < 32 || jwt.Key == DevJwtKey))
            throw new InvalidOperationException("Jwt:Key must be a unique secret of at least 32 characters in production");
        return jwt;
    }

    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });
        services.AddOpenApi();
        return services;
    }

    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, JwtSettings jwt, IHostEnvironment env)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = !env.IsDevelopment();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
            });
        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:4200", "https://localhost:4200" };
        services.AddCors(o => o.AddPolicy(CorsPolicy, p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        return services;
    }

    /// <summary>Per-IP limits on sign-in and the anonymous booking API (brute force / scraping).</summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
                await context.HttpContext.Response.WriteAsync(
                    "{\"error\":{\"code\":\"rate_limited\",\"message\":\"محاولات كثيرة. انتظر دقيقة ثم حاول مرة أخرى\"}}", ct);
            };
            options.AddPolicy(AuthRateLimit, http => PerIp(http, permitPerMinute: 10));
            options.AddPolicy(PublicRateLimit, http => PerIp(http, permitPerMinute: 60));
        });
        return services;
    }

    /// <summary>Health checks, compression and reverse-proxy awareness for container hosting.</summary>
    public static IServiceCollection AddApiHosting(this IServiceCollection services)
    {
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: new[] { "ready" });
        services.AddResponseCompression(o =>
        {
            o.EnableForHttps = true;
            o.Providers.Add<BrotliCompressionProvider>();
            o.Providers.Add<GzipCompressionProvider>();
        });
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Running behind a reverse proxy / ingress inside a private network
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });
        return services;
    }

    private static RateLimitPartition<string> PerIp(HttpContext http, int permitPerMinute)
        => RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
}
