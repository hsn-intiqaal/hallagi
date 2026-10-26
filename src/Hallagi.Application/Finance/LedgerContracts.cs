namespace Hallagi.Application.Finance;

public record LedgerEntryRequest(
    Guid BarberId,
    Guid? BranchId,
    BarberTxType Type,
    BarberTxDirection Direction,
    decimal Amount,
    DateOnly TxDate,
    string? Description,
    string? SourceRef,
    string? Notes);

public record CreateAdvanceRequest(Guid BarberId, Guid? BranchId, decimal Amount, DateOnly AdvanceDate, string? Reason);

public record CreateHandoverRequest(Guid BranchId, Guid FromUserId, Guid ToUserId, Guid? BarberId, Guid? ShiftId, decimal Amount, DateOnly HandoverDate, string? Note);
