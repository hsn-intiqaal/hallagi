using Hallagi.Application.Common.Abstractions;
using Hallagi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hallagi.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<MemberBranchAccess> MemberBranchAccess => Set<MemberBranchAccess>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<BranchServicePrice> BranchServicePrices => Set<BranchServicePrice>();
    public DbSet<Barber> Barbers => Set<Barber>();
    public DbSet<BarberBranchAssignment> BarberBranchAssignments => Set<BarberBranchAssignment>();
    public DbSet<BarberCompensation> BarberCompensations => Set<BarberCompensation>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<OrganizationSettings> OrganizationSettings => Set<OrganizationSettings>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<CashDrawer> CashDrawers => Set<CashDrawer>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<BarberFinancialTransaction> BarberFinancialTransactions => Set<BarberFinancialTransaction>();
    public DbSet<BarberAdvance> BarberAdvances => Set<BarberAdvance>();
    public DbSet<CashHandover> CashHandovers => Set<CashHandover>();
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<BarberSettlement> BarberSettlements => Set<BarberSettlement>();
    public DbSet<SettlementItem> SettlementItems => Set<SettlementItem>();
    public DbSet<SettlementPayment> SettlementPayments => Set<SettlementPayment>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductStock> ProductStocks => Set<ProductStock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentItem> AppointmentItems => Set<AppointmentItem>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => Database.BeginTransactionAsync(ct);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
