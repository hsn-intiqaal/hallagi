using Hallagi.Domain.Enums;

namespace Hallagi.Domain.Entities;

public class LoyaltyTransaction
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CustomerId { get; set; }
    public LoyaltyTxType Type { get; set; }
    public int Points { get; set; }                         // signed
    public int BalanceAfter { get; set; }
    public Guid? SaleId { get; set; }
    public string? Note { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
