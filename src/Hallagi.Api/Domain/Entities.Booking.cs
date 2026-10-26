namespace Hallagi.Api.Domain;

// ============ Booking: appointments & walk-in queue ============

public class Appointment
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    /// <summary>Null = any available barber (assigned at check-in).</summary>
    public Guid? BarberId { get; set; }
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public string? CustomerPhone { get; set; }
    public DateTime StartAt { get; set; }                   // UTC
    public DateTime EndAt { get; set; }                     // UTC
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;
    public AppointmentSource Source { get; set; } = AppointmentSource.Staff;
    public string? Notes { get; set; }
    public Guid? SaleId { get; set; }
    public string? CancelReason { get; set; }
    /// <summary>Public reference shown to online customers (e.g. "H7K2Q9").</summary>
    public string Reference { get; set; } = default!;
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<AppointmentItem> Items { get; set; } = new List<AppointmentItem>();
}

public class AppointmentItem
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }
    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; } = default!;
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
    public Appointment Appointment { get; set; } = default!;
}
