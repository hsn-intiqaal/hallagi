namespace Hallagi.Application.Catalog;

public record ServiceInput(string Name, string? Description, decimal BasePrice, bool Active, int? DurationMinutes = null);

public record CompensationInput(CompensationType Type, decimal? MonthlySalary, decimal? CommissionRate, decimal? ChairRentAmount, string? Notes, DateOnly EffectiveFrom);

public record BarberInput(string FullName, string? Phone, DateOnly? StartDate, bool Active, List<Guid> BranchIds, CompensationInput? Compensation);

public record CustomerInput(string FullName, string? Phone, string? Notes);
