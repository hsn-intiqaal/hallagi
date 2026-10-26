namespace Hallagi.Domain.Entities;

public class Customer
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string FullName { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    /// <summary>Current loyalty balance (sum of LoyaltyTransactions).</summary>
    public int LoyaltyPoints { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
