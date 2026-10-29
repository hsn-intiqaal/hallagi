namespace Hallagi.Domain.Entities;

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
