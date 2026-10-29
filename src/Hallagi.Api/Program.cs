using Hallagi.Api.Extensions;
using Hallagi.Application;
using Hallagi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Structured JSON logs outside development (container / log-shipper friendly)
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.UseUtcTimestamp = true;
    });
}

var jwt = builder.Configuration.GetJwtSettings(builder.Environment);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration, jwt)
    .AddApiControllers()
    .AddApiAuthentication(jwt, builder.Environment)
    .AddApiCors(builder.Configuration)
    .AddApiRateLimiting(builder.Configuration)
    .AddApiHosting();

var app = builder.Build();

app.UseApiPipeline();
await app.InitializeDatabaseAsync();
app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
