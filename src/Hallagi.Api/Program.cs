using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Hallagi.Api.Data;
using Hallagi.Api.Middleware;
using Hallagi.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ===== Logging: structured JSON outside development (container/log-shipper friendly) =====
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
        o.UseUtcTimestamp = true;
    });
}

// ===== Configuration =====
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required");
var provider = builder.Configuration["Database:Provider"] ?? "SqlServer";

var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("Jwt").Bind(jwtSettings);
if (string.IsNullOrWhiteSpace(jwtSettings.Key))
    throw new InvalidOperationException("Jwt:Key is required (set Jwt__Key environment variable)");

// In production a strong, non-default key is mandatory
const string DevJwtKey = "dev-only-key-change-in-production-0123456789";
if (!builder.Environment.IsDevelopment() && (jwtSettings.Key.Length < 32 || jwtSettings.Key == DevJwtKey))
    throw new InvalidOperationException("Jwt:Key must be a unique secret of at least 32 characters in production");

// ===== Services =====
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        options.UseSqlite(connectionString);
    else
        options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3));
});

builder.Services.AddSingleton(jwtSettings);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SalesService>();
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<ShiftService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReportsService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<UsersService>();
builder.Services.AddScoped<BranchesService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<PlatformService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<SettlementService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CouponService>();
builder.Services.AddScoped<LoyaltyService>();
builder.Services.AddScoped<AppointmentService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });
builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200", "https://localhost:4200" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ===== Abuse protection: per-IP limits on sign-in and the anonymous booking API =====
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "{\"error\":{\"code\":\"rate_limited\",\"message\":\"محاولات كثيرة. انتظر دقيقة ثم حاول مرة أخرى\"}}", ct);
    };
    options.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("public", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: new[] { "ready" });
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Running behind a reverse proxy/ingress inside a private network
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddOpenApi();

var app = builder.Build();

// ===== Pipeline =====
app.UseForwardedHeaders();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
app.UseResponseCompression();
app.UseCors("AngularClient");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Liveness: process is up. Readiness: database reachable.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

// ===== Database: migrate always (configurable); demo seed only in development or when asked =====
var seedOnStartup = builder.Configuration.GetValue<bool>("Seed:OnStartup");
var migrateOnStartup = builder.Configuration.GetValue("Database:MigrateOnStartup", true);
if (migrateOnStartup || seedOnStartup || app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        if (app.Environment.IsDevelopment() || seedOnStartup)
        {
            logger.LogInformation("Applying migrations and seeding demo data...");
            await DbSeeder.SeedAsync(db, builder.Configuration);
            logger.LogInformation("Database ready. Demo password: {Password}", DbSeeder.DemoPassword);
        }
        else
        {
            logger.LogInformation("Applying migrations...");
            await DbSeeder.BootstrapAsync(db, builder.Configuration);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization failed");
        if (!app.Environment.IsDevelopment()) throw;
    }
}

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
