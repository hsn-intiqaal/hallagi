namespace Hallagi.Domain.Entities;

public class SettlementPayment
{
    public Guid Id { get; set; }
    public Guid SettlementId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string? Method { get; set; }                     // Cash / Bank
    public string? Note { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public BarberSettlement Settlement { get; set; } = default!;
}
