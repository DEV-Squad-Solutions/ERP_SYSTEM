using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeTransactions;

public sealed record EmployeeTransactionFilterRequest(
    int? EmployeeId = null,
    EmployeeTransactionType? Type = null,
    DateOnly? TransactionDateFrom = null,
    DateOnly? TransactionDateTo = null,
    bool? IsProcessed = null,
    string? Search = null);
