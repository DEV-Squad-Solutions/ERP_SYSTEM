using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeTransactions;

public sealed record EmployeeTransactionRequest(
    int EmployeeId,
    EmployeeTransactionType Type,
    decimal Amount,
    DateOnly TransactionDate,
    string? Notes = null);
