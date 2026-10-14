using System.Text;
using System.Text.Json.Serialization;
using Hallagi.Api.Data;
using Hallagi.Api.Middleware;
using Hallagi.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ===== Configuration =====
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required");

var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("Jwt").Bind(jwtSettings);
if (string.IsNullOrWhiteSpace(jwtSettings.Key))
    throw new InvalidOperationException("Jwt:Key is required (set Jwt__Key environment variable)");

// In production a strong key is mandatory
if (!builder.Environment.IsDevelopment() && jwtSettings.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters in production");

// ===== Services =====
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

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
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<SettlementService>();

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
        options.RequireHttpsMetadata = false;
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

builder.Services.AddOpenApi();

var app = builder.Build();

// ===== Pipeline =====
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("AngularClient");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ===== Database migrate + seed (development/demo) =====
var seedOnStartup = builder.Configuration.GetValue<bool>("Seed:OnStartup");
if (app.Environment.IsDevelopment() || seedOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Applying migrations and seeding demo data...");
        await DbSeeder.SeedAsync(db, builder.Configuration);
        logger.LogInformation("Database ready. Demo password: {Password}", DbSeeder.DemoPassword);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database seeding failed");
    }
}

app.Run();
