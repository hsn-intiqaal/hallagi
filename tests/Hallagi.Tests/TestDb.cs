using Hallagi.Api.Data;
using Hallagi.Api.Domain;
using Hallagi.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Hallagi.Tests;

/// <summary>Shared SQLite-backed context for service-level tests.</summary>
public class TestDb : IDisposable
{
    public AppDbContext Db { get; }
    public Guid OrgId { get; }
    public Guid BranchA { get; }
    public Guid BranchB { get; }
    public Guid OwnerProfile { get; }
    public Guid CashierProfile { get; }
    public Guid BarberProfile { get; }
    public Guid OwnerMember { get; }
    public Guid CashierMember { get; }
    public Guid BarberMember { get; }
    public Guid BarberId { get; }
    public Guid SaleServiceId { get; }
    public Guid OtherServiceId { get; }

    public TestDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"DataSource=file:test_{Guid.NewGuid():N}?mode=memory&cache=shared")
            .EnableSensitiveDataLogging(false)
            .Options;
        Db = new AppDbContext(options);
        Db.Database.OpenConnection();
        Db.Database.EnsureCreated();

        var now = DateTime.UtcNow;
        OrgId = Guid.NewGuid();
        Db.Organizations.Add(new Organization { Id = OrgId, Name = "منشأة اختبار", CreatedAt = now, UpdatedAt = now });
        Db.OrganizationSettings.Add(new OrganizationSettings { OrganizationId = OrgId });

        BranchA = Guid.NewGuid();
        BranchB = Guid.NewGuid();
        foreach (var (id, name) in new[] { (BranchA, "فرع النرجس"), (BranchB, "فرع الياسمين") })
        {
            Db.Branches.Add(new Branch { Id = id, OrganizationId = OrgId, Name = name, City = "الرياض", CreatedAt = now, UpdatedAt = now, Drawer = new CashDrawer { OrganizationId = OrgId, CreatedAt = now } });
        }

        SaleServiceId = Guid.NewGuid();
        OtherServiceId = Guid.NewGuid();
        Db.Services.Add(new Service { Id = SaleServiceId, OrganizationId = OrgId, Name = "حلاقة شعر", BasePrice = 25m, CreatedAt = now, UpdatedAt = now });
        Db.Services.Add(new Service { Id = OtherServiceId, OrganizationId = OrgId, Name = "صبغة شعر", BasePrice = 50m, CreatedAt = now, UpdatedAt = now });

        BarberId = Guid.NewGuid();
        var barber = new Barber { Id = BarberId, OrganizationId = OrgId, FullName = "أحمد العتيبي", CreatedAt = now, UpdatedAt = now };
        barber.LinkedProfileId = BarberProfile; // set before save (hasn't been persisted yet)
        Db.Barbers.Add(barber);
        Db.BarberBranchAssignments.Add(new BarberBranchAssignment { Id = Guid.NewGuid(), BarberId = BarberId, BranchId = BranchA });

        Db.Customers.Add(new Customer { Id = Guid.NewGuid(), OrganizationId = OrgId, FullName = "عبدالله الدوسري", Phone = "0550000001", CreatedAt = now, UpdatedAt = now });

        OwnerProfile = Guid.NewGuid();
        CashierProfile = Guid.NewGuid();
        BarberProfile = Guid.NewGuid();
        foreach (var (id, name) in new[] { (OwnerProfile, "مالك"), (CashierProfile, "كاشير"), (BarberProfile, "حلاق") })
            Db.Profiles.Add(new Profile { Id = id, FullName = name, Email = $"{id}@test.sa", PasswordHash = "x", CreatedAt = now, UpdatedAt = now });

        // link the barber's login after profiles exist (needed for barber-operated POS)
        barber.LinkedProfileId = BarberProfile;

        OwnerMember = Guid.NewGuid();
        CashierMember = Guid.NewGuid();
        BarberMember = Guid.NewGuid();
        Db.OrganizationMembers.Add(new OrganizationMember { Id = OwnerMember, OrganizationId = OrgId, ProfileId = OwnerProfile, Role = UserRole.Owner, CreatedAt = now, UpdatedAt = now });
        Db.OrganizationMembers.Add(new OrganizationMember { Id = CashierMember, OrganizationId = OrgId, ProfileId = CashierProfile, Role = UserRole.Cashier, CreatedAt = now, UpdatedAt = now });
        Db.OrganizationMembers.Add(new OrganizationMember { Id = BarberMember, OrganizationId = OrgId, ProfileId = BarberProfile, Role = UserRole.Barber, CreatedAt = now, UpdatedAt = now });
        Db.MemberBranchAccess.Add(new MemberBranchAccess { Id = Guid.NewGuid(), MemberId = CashierMember, BranchId = BranchA });
        Db.SaveChanges();
    }

    public CurrentUser AsOwner() => new(OwnerProfile, OrgId, OwnerMember, UserRole.Owner, true, new[] { BranchA, BranchB }, null);
    public CurrentUser AsCashier() => new(CashierProfile, OrgId, CashierMember, UserRole.Cashier, true, new[] { BranchA }, null);
    public CurrentUser AsBarber() => new(BarberProfile, OrgId, BarberMember, UserRole.Barber, true, new[] { BranchA }, null);

    public void Dispose() => Db.Dispose();
}
