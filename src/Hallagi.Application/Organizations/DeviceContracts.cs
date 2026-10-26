namespace Hallagi.Application.Organizations;

public record CreateDeviceRequest(Guid BranchId, string Name, string? DeviceCode);
