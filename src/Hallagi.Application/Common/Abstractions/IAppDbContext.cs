using Microsoft.EntityFrameworkCore.Storage;

namespace Hallagi.Application.Common.Abstractions;

/// <summary>
/// Persistence port used by the application services. Implemented by the EF Core context in
/// Infrastructure; tests plug in the same context on SQLite.
/// </summary>
public interface IAppDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Profile> Profiles { get; }
    DbSet<OrganizationMember> OrganizationMembers { get; }
    DbSet<MemberBranchAccess> MemberBranchAccess { get; }
    DbSet<Service> Services { get; }
    DbSet<BranchServicePrice> BranchServicePrices { get; }
    DbSet<Barber> Barbers { get; }
    DbSet<BarberBranchAssignment> BarberBranchAssignments { get; }
    DbSet<BarberCompensation> BarberCompensations { get; }
    DbSet<Customer> Customers { get; }
    DbSet<OrganizationSettings> OrganizationSettings { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleItem> SaleItems { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<CashDrawer> CashDrawers { get; }
    DbSet<Shift> Shifts { get; }
    DbSet<CashMovement> CashMovements { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<Device> Devices { get; }
    DbSet<BarberFinancialTransaction> BarberFinancialTransactions { get; }
    DbSet<BarberAdvance> BarberAdvances { get; }
    DbSet<CashHandover> CashHandovers { get; }
    DbSet<PayrollPeriod> PayrollPeriods { get; }
    DbSet<BarberSettlement> BarberSettlements { get; }
    DbSet<SettlementItem> SettlementItems { get; }
    DbSet<SettlementPayment> SettlementPayments { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductStock> ProductStocks { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<Coupon> Coupons { get; }
    DbSet<LoyaltyTransaction> LoyaltyTransactions { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<AppointmentItem> AppointmentItems { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    int SaveChanges();
}
