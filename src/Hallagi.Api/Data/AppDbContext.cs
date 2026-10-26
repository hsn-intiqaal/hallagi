using Hallagi.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hallagi.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
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

    // Financial precision: NUMERIC(10,2) everywhere, never float
    private const string Money = "decimal(10,2)";
    private const string Rate = "decimal(8,4)";

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Organization>(e =>
        {
            e.HasIndex(x => x.Name);
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Slug).HasMaxLength(60);
            e.HasIndex(x => x.Slug).IsUnique();
        });

        b.Entity<Branch>(e =>
        {
            e.HasOne(x => x.Organization).WithMany(x => x.Branches).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.OrganizationId, x.Name });
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.City).HasMaxLength(100);
        });

        b.Entity<Profile>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.FullName).HasMaxLength(150);
        });

        b.Entity<OrganizationMember>(e =>
        {
            e.HasOne(x => x.Organization).WithMany(x => x.Members).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Profile).WithMany(x => x.Memberships).HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.OrganizationId, x.ProfileId }).IsUnique();
        });

        b.Entity<MemberBranchAccess>(e =>
        {
            e.HasOne(x => x.Member).WithMany(x => x.BranchAccess).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.MemberId, x.BranchId }).IsUnique();
        });

        b.Entity<Service>(e =>
        {
            e.Property(x => x.BasePrice).HasColumnType(Money);
            e.HasIndex(x => new { x.OrganizationId, x.Name });
            e.Property(x => x.Name).HasMaxLength(150);
        });

        b.Entity<BranchServicePrice>(e =>
        {
            e.Property(x => x.Price).HasColumnType(Money);
            e.HasOne(x => x.Service).WithMany(x => x.BranchPrices).HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ServiceId, x.BranchId }).IsUnique();
        });

        b.Entity<Barber>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(150);
            e.HasIndex(x => x.OrganizationId);
        });

        b.Entity<BarberBranchAssignment>(e =>
        {
            e.HasOne(x => x.Barber).WithMany(x => x.BranchAssignments).HasForeignKey(x => x.BarberId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.BarberId, x.BranchId }).IsUnique();
        });

        b.Entity<BarberCompensation>(e =>
        {
            e.Property(x => x.MonthlySalary).HasColumnType(Money);
            e.Property(x => x.CommissionRate).HasColumnType(Rate);
            e.Property(x => x.ChairRentAmount).HasColumnType(Money);
            e.HasOne(x => x.Barber).WithMany(x => x.CompensationSettings).HasForeignKey(x => x.BarberId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.BarberId);
        });

        b.Entity<Customer>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(150);
            e.HasIndex(x => x.OrganizationId);
            e.HasIndex(x => new { x.OrganizationId, x.Phone });
        });

        b.Entity<OrganizationSettings>(e =>
        {
            e.HasKey(x => x.OrganizationId);
            e.Property(x => x.VatRate).HasColumnType(Rate);
            e.Property(x => x.MaxManualDiscountPercent).HasColumnType(Rate);
            e.Property(x => x.LoyaltyPointsPerSar).HasColumnType(Rate);
            e.Property(x => x.LoyaltyPointValue).HasColumnType(Rate);
            e.Property(x => x.VatNumber).HasMaxLength(15);
            e.Property(x => x.CommercialRegistration).HasMaxLength(20);
            e.Property(x => x.LegalName).HasMaxLength(200);
            e.HasOne(x => x.Organization).WithOne(x => x.Settings!).HasForeignKey<OrganizationSettings>(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Sale>(e =>
        {
            e.Property(x => x.TotalAmount).HasColumnType(Money);
            e.Property(x => x.GrossAmount).HasColumnType(Money);
            e.Property(x => x.DiscountAmount).HasColumnType(Money);
            e.Property(x => x.SubtotalAmount).HasColumnType(Money);
            e.Property(x => x.VatAmount).HasColumnType(Money);
            e.Property(x => x.CommissionBase).HasColumnType(Money);
            e.Property(x => x.VatRate).HasColumnType(Rate);
            e.Property(x => x.CouponCode).HasMaxLength(40);
            e.Property(x => x.InvoiceSellerName).HasMaxLength(200);
            e.Property(x => x.InvoiceVatNumber).HasMaxLength(15);
            e.HasIndex(x => new { x.OrganizationId, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => new { x.OrganizationId, x.ReceiptNo }).IsUnique();
            e.HasIndex(x => new { x.BranchId, x.CreatedAt });
            e.HasIndex(x => new { x.BarberId, x.CreatedAt });
        });

        b.Entity<SaleItem>(e =>
        {
            e.Property(x => x.UnitPrice).HasColumnType(Money);
            e.Property(x => x.LineTotal).HasColumnType(Money);
            e.HasOne(x => x.Sale).WithMany(x => x.Items).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.SaleId);
        });

        b.Entity<Expense>(e =>
        {
            e.Property(x => x.Amount).HasColumnType(Money);
            e.HasIndex(x => new { x.BranchId, x.ExpenseDate });
            e.Property(x => x.Description).HasMaxLength(500);
        });

        b.Entity<CashDrawer>(e =>
        {
            e.HasIndex(x => x.BranchId).IsUnique();
        });

        b.Entity<Shift>(e =>
        {
            e.Property(x => x.OpeningCash).HasColumnType(Money);
            e.Property(x => x.CountedCash).HasColumnType(Money);
            e.Property(x => x.ExpectedCash).HasColumnType(Money);
            e.Property(x => x.Difference).HasColumnType(Money);
            e.HasIndex(x => new { x.BranchId, x.Status });
        });

        b.Entity<CashMovement>(e =>
        {
            e.Property(x => x.Amount).HasColumnType(Money);
            e.HasOne(x => x.Shift).WithMany(x => x.Movements).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ShiftId);
        });

        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
        });

        // ===== SaaS & financial ledger =====
        b.Entity<SubscriptionPlan>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.MonthlyPrice).HasColumnType(Money);
        });

        b.Entity<Subscription>(e =>
        {
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.OrganizationId).IsUnique();
            e.Property(x => x.Status).HasMaxLength(20);
        });

        b.Entity<Device>(e =>
        {
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.OrganizationId, x.BranchId });
            e.Property(x => x.Name).HasMaxLength(100);
        });

        b.Entity<BarberFinancialTransaction>(e =>
        {
            e.Property(x => x.Amount).HasColumnType(Money);
            e.HasOne(x => x.Barber).WithMany().HasForeignKey(x => x.BarberId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.BarberId, x.TxDate });
            e.HasIndex(x => x.SourceRef);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.SourceType).HasMaxLength(40);
        });

        b.Entity<BarberAdvance>(e =>
        {
            e.Property(x => x.OriginalAmount).HasColumnType(Money);
            e.Property(x => x.RepaidAmount).HasColumnType(Money);
            e.HasOne(x => x.Barber).WithMany().HasForeignKey(x => x.BarberId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.BarberId, x.Status });
            e.Property(x => x.Status).HasMaxLength(20);
        });

        b.Entity<CashHandover>(e =>
        {
            e.Property(x => x.Amount).HasColumnType(Money);
            e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.BranchId, x.HandoverDate });
            e.Property(x => x.Status).HasMaxLength(20);
        });

        b.Entity<PayrollPeriod>(e =>
        {
            e.HasIndex(x => new { x.OrganizationId, x.StartDate, x.EndDate }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Status).HasMaxLength(20);
        });

        b.Entity<BarberSettlement>(e =>
        {
            e.Property(x => x.SalaryEarned).HasColumnType(Money);
            e.Property(x => x.CommissionEarned).HasColumnType(Money);
            e.Property(x => x.Bonus).HasColumnType(Money);
            e.Property(x => x.Allowances).HasColumnType(Money);
            e.Property(x => x.Reimbursements).HasColumnType(Money);
            e.Property(x => x.AdvancesRepaid).HasColumnType(Money);
            e.Property(x => x.ChairRent).HasColumnType(Money);
            e.Property(x => x.Deductions).HasColumnType(Money);
            e.Property(x => x.PreviousBalance).HasColumnType(Money);
            e.Property(x => x.AlreadyPaid).HasColumnType(Money);
            e.Property(x => x.NetPayable).HasColumnType(Money);
            e.HasOne(x => x.PayrollPeriod).WithMany().HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Barber).WithMany().HasForeignKey(x => x.BarberId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.OrganizationId, x.PayrollPeriodId, x.BarberId }).IsUnique();
            e.Property(x => x.Status).HasMaxLength(20);
        });

        b.Entity<SettlementItem>(e =>
        {
            e.Property(x => x.Amount).HasColumnType(Money);
            e.HasOne(x => x.Settlement).WithMany(x => x.Items).HasForeignKey(x => x.SettlementId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Component).HasMaxLength(60);
        });

        b.Entity<SettlementPayment>(e =>
        {
            e.Property(x => x.Amount).HasColumnType(Money);
            e.HasOne(x => x.Settlement).WithMany(x => x.Payments).HasForeignKey(x => x.SettlementId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Method).HasMaxLength(20);
        });

        // ===== Commerce =====
        b.Entity<Product>(e =>
        {
            e.Property(x => x.Price).HasColumnType(Money);
            e.Property(x => x.Cost).HasColumnType(Money);
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Sku).HasMaxLength(60);
            e.Property(x => x.Barcode).HasMaxLength(60);
            e.HasIndex(x => new { x.OrganizationId, x.Name });
            e.HasIndex(x => new { x.OrganizationId, x.Barcode });
        });

        b.Entity<ProductStock>(e =>
        {
            e.HasOne(x => x.Product).WithMany(x => x.Stock).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ProductId, x.BranchId }).IsUnique();
        });

        b.Entity<StockMovement>(e =>
        {
            e.Property(x => x.UnitCost).HasColumnType(Money);
            e.Property(x => x.Note).HasMaxLength(300);
            e.HasIndex(x => new { x.ProductId, x.BranchId, x.CreatedAt });
        });

        b.Entity<Coupon>(e =>
        {
            e.Property(x => x.Value).HasColumnType(Money);
            e.Property(x => x.MinSubtotal).HasColumnType(Money);
            e.Property(x => x.MaxDiscount).HasColumnType(Money);
            e.Property(x => x.Code).HasMaxLength(40);
            e.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
        });

        b.Entity<LoyaltyTransaction>(e =>
        {
            e.HasIndex(x => new { x.CustomerId, x.CreatedAt });
            e.Property(x => x.Note).HasMaxLength(300);
        });

        // ===== Booking =====
        b.Entity<Appointment>(e =>
        {
            e.Property(x => x.CustomerName).HasMaxLength(150);
            e.Property(x => x.CustomerPhone).HasMaxLength(30);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.Property(x => x.Reference).HasMaxLength(12);
            e.HasIndex(x => new { x.BranchId, x.StartAt });
            e.HasIndex(x => new { x.BarberId, x.StartAt });
            e.HasIndex(x => new { x.OrganizationId, x.Reference }).IsUnique();
        });

        b.Entity<AppointmentItem>(e =>
        {
            e.Property(x => x.Price).HasColumnType(Money);
            e.HasOne(x => x.Appointment).WithMany(x => x.Items).HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.Property(x => x.ReplacedByHash).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.ProfileId);
        });
    }
}
