using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Hallagi.Application.Booking;

public record AppointmentInput(
    Guid BranchId,
    Guid? BarberId,
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    DateOnly? Date,
    string? Time,              // "HH:mm" in the organization's local time
    List<Guid> ServiceIds,
    string? Notes,
    bool WalkIn = false);

public record AppointmentStatusInput(AppointmentStatus Status, string? Reason, Guid? BarberId);

public record PublicBookingInput(
    Guid BranchId,
    Guid? BarberId,
    List<Guid> ServiceIds,
    DateOnly Date,
    string Time,
    string Name,
    string Phone,
    string? Notes);

public record AppointmentItemView(Guid ServiceId, string ServiceName, decimal Price, int DurationMinutes);

public record AppointmentView(
    Guid Id, string Reference, Guid BranchId, Guid? BarberId, string? BarberName,
    Guid? CustomerId, string CustomerName, string? CustomerPhone, string? WhatsApp,
    DateTime StartAt, DateTime EndAt, string Date, string StartTime, string EndTime, int DurationMinutes,
    AppointmentStatus Status, string StatusText, AppointmentSource Source, string SourceText,
    string? Notes, Guid? SaleId, string? CancelReason,
    List<AppointmentItemView> Items, decimal Total);
