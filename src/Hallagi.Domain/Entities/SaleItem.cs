using Hallagi.Domain.Enums;

namespace Hallagi.Domain.Entities;

public class SaleItem
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public SaleItemType ItemType { get; set; } = SaleItemType.Service;
    public Guid? ServiceId { get; set; }
    public Guid? ProductId { get; set; }
    /// <summary>Display name snapshot (service or product).</summary>
    public string ServiceName { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public Sale Sale { get; set; } = default!;
}
