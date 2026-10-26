using Hallagi.Api.Middleware;
using Hallagi.Infrastructure.Persistence;
using Hallagi.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Hallagi.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();
        if (app.Environment.IsDevelopment()) app.MapOpenApi();
        else app.UseHsts();

        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseResponseCompression();
        app.UseCors(ServiceCollectionExtensions.CorsPolicy);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        // Liveness: the process is up. Readiness: the database is reachable.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
        return app;
    }

    /// <summary>
    /// Development / demo: migrate + rich demo data. Production: migrate + plans + the
    /// bootstrap platform owner from configuration only — never demo accounts.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        var config = app.Configuration;
        var seedDemo = app.Environment.IsDevelopment() || config.GetValue<bool>("Seed:OnStartup");
        if (!seedDemo && !config.GetValue("Database:MigrateOnStartup", true)) return;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            if (seedDemo)
            {
                logger.LogInformation("Applying migrations and seeding demo data...");
                await DbSeeder.SeedAsync(db, config);
                logger.LogInformation("Database ready. Demo password: {Password}", DbSeeder.DemoPassword);
            }
            else
            {
                logger.LogInformation("Applying migrations...");
                await DbSeeder.BootstrapAsync(db, config);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization failed");
            if (!app.Environment.IsDevelopment()) throw;
        }
    }
}
