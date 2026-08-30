using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeMovements;

public sealed record EmployeeMovementResponse(
    int Id,
    int CompanyId,
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    EmployeeMovementType Type,
    DateOnly MovementDate,
    CurrencyCode Currency,
    decimal Amount,
    decimal Debit,
    decimal Credit,
    decimal ExchangeRate,
    decimal BaseDebit,
    decimal BaseCredit,
    int? CashVoucherId,
    string? CashVoucherNumber,
    string? Notes,
    DateTime CreatedOn);
