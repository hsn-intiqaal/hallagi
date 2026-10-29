namespace Hallagi.Domain.Entities;

public class BranchServicePrice
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public Guid BranchId { get; set; }
    public decimal Price { get; set; }
    public Service Service { get; set; } = default!;
}
