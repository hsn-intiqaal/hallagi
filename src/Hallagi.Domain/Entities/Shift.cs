using Hallagi.Domain.Enums;

namespace Hallagi.Domain.Entities;

public class Shift
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid DrawerId { get; set; }
    public Guid OpenedBy { get; set; }
    public decimal OpeningCash { get; set; }
    public DateTime OpenedAt { get; set; }
    public Guid? ClosedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? Difference { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
    public string? Note { get; set; }
    public bool AutoOpened { get; set; }
    public Guid? DeviceId { get; set; }               // POS device used to open the shift (optional)

    public ICollection<CashMovement> Movements { get; set; } = new List<CashMovement>();
}
