namespace Hallagi.Domain.Entities;

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
