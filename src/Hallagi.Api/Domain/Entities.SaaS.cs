namespace Hallagi.Api.Domain;

// ============ SaaS: subscriptions & entitlements ============

public class SubscriptionPlan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;            // "مجاني", "أساسي", "احترافي"
    public string? Description { get; set; }
    public int MaxBranches { get; set; } = 1;               // حد الفروع
    public int MaxUsers { get; set; } = 2;                  // حد المستخدمين
    public int MaxBarbers { get; set; } = 5;                // حد الحلاقين
    public bool AllowAdvancedReports { get; set; }          // صلاحية التقارير المتقدمة
    public bool AllowSettlements { get; set; }              // صلاحية التسويات المالية
    public decimal MonthlyPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class Subscription
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid PlanId { get; set; }
    public string Status { get; set; } = "Trial";           // Trial / Active / PastDue / Cancelled
    public DateOnly PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public DateOnly? TrialEndsAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization Organization { get; set; } = default!;
    public SubscriptionPlan Plan { get; set; } = default!;
}

// ============ POS devices ============

public class Device
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public string Name { get; set; } = default!;            // "كاشير 1"
    public string? DeviceCode { get; set; }                 // معرّف الجهاز الفيزيائي
    public string Status { get; set; } = "Active";          // Active / Disabled
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organization Organization { get; set; } = default!;
    public Branch Branch { get; set; } = default!;
}
