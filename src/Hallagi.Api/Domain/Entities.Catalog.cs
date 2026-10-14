namespace Hallagi.Api.Domain;

// ============ Catalog: services, prices, barbers, customers ============

public class Service
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<BranchServicePrice> BranchPrices { get; set; } = new List<BranchServicePrice>();
}

public class BranchServicePrice
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public Guid BranchId { get; set; }
    public decimal Price { get; set; }
    public Service Service { get; set; } = default!;
}

public class Barber
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string FullName { get; set; } = default!;
    public string? Phone { get; set; }
    public DateOnly? StartDate { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Active;
    // Optional login account linked to this barber profile (enables the "my barber" role).
    public Guid? LinkedProfileId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<BarberBranchAssignment> BranchAssignments { get; set; } = new List<BarberBranchAssignment>();
    public ICollection<BarberCompensation> CompensationSettings { get; set; } = new List<BarberCompensation>();
}

public class BarberBranchAssignment
{
    public Guid Id { get; set; }
    public Guid BarberId { get; set; }
    public Guid BranchId { get; set; }
    public Barber Barber { get; set; } = default!;
}

public class BarberCompensation
{
    public Guid Id { get; set; }
    public Guid BarberId { get; set; }
    public CompensationType Type { get; set; }
    public decimal? MonthlySalary { get; set; }
    public decimal? CommissionRate { get; set; } // 0.30 = 30%
    public decimal? ChairRentAmount { get; set; }
    public string? Notes { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Active;
    public DateTime CreatedAt { get; set; }
    public Barber Barber { get; set; } = default!;
}

public class Customer
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string FullName { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class OrganizationSettings
{
    public Guid OrganizationId { get; set; }
    public bool EnableCash { get; set; } = true;
    public bool EnableCard { get; set; } = true;
    public string? ReceiptFooter { get; set; }
    public string? ReceiptPhone { get; set; }
    public Organization Organization { get; set; } = default!;
}
