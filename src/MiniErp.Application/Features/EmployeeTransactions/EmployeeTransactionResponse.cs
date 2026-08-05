using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeTransactions;

public sealed record EmployeeTransactionResponse(
    int Id,
    int CompanyId,
    int EmployeeId,
    string EmployeeName,
    EmployeeTransactionType Type,
    decimal Amount,
    DateOnly TransactionDate,
    string? Notes,
    bool IsProcessed);
