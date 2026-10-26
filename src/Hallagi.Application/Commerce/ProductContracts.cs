namespace Hallagi.Application.Commerce;

public record ProductInput(string Name, string? Sku, string? Barcode, decimal Price, decimal Cost, bool Active);

public record StockAdjustInput(Guid BranchId, StockMovementType Type, int Quantity, decimal? UnitCost, string? Note, int? LowStockThreshold);
