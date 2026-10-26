namespace Hallagi.Application.Expenses;

public record CreateExpenseRequest(
    Guid BranchId,
    ExpenseCategory Category,
    decimal Amount,
    DateOnly ExpenseDate,
    string? Description,
    ExpenseSource Source);
