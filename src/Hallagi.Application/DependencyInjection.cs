using Hallagi.Application.Booking;
using Hallagi.Application.Catalog;
using Hallagi.Application.Commerce;
using Hallagi.Application.Expenses;
using Hallagi.Application.Finance;
using Hallagi.Application.Identity;
using Hallagi.Application.Organizations;
using Hallagi.Application.Reports;
using Hallagi.Application.Sales;
using Hallagi.Application.Treasury;
using Microsoft.Extensions.DependencyInjection;

namespace Hallagi.Application;

public static class DependencyInjection
{
    /// <summary>Registers every use-case service (one scoped instance per request).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Time: UTC storage, organization-local calendar decisions
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<Common.Time.OrgClock>();

        // Identity & organization
        services.AddScoped<AuthService>();
        services.AddScoped<SessionService>();
        services.AddScoped<UsersService>();
        services.AddScoped<BranchesService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<SubscriptionService>();
        services.AddScoped<PlatformService>();
        services.AddScoped<DeviceService>();

        // Operations
        services.AddScoped<CatalogService>();
        services.AddScoped<PosService>();
        services.AddScoped<SalesService>();
        services.AddScoped<ExpenseService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<AppointmentService>();

        // Commerce
        services.AddScoped<ProductService>();
        services.AddScoped<CouponService>();
        services.AddScoped<LoyaltyService>();

        // Finance & reporting
        services.AddScoped<LedgerService>();
        services.AddScoped<SettlementService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ReportsService>();
        return services;
    }
}
