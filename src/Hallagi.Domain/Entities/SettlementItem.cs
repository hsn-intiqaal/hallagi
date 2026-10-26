namespace Hallagi.Domain.Entities;

public class SettlementItem
{
    public Guid Id { get; set; }
    public Guid SettlementId { get; set; }
    public string Component { get; set; } = default!;       // "راتب", "عمولة", "سلفة", ...
    public string? SourceRef { get; set; }                  // traceability to the source records
    public decimal Amount { get; set; }
    public string? Explanation { get; set; }
    public BarberSettlement Settlement { get; set; } = default!;
}
