using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeOpeningBalances;

public sealed record EmployeeOpeningBalanceResponse(
    int Id,
    int CompanyId,
    int EmployeeId,
    string EmployeeName,
    string EmployeeCode,
    int? PayrollEntryId,
    string DocumentNumber,
    DateOnly DocumentDate,
    CurrencyCode Currency,
    CurrencyCode BaseCurrency,
    decimal ExchangeRate,
    EmployeeBalanceType BalanceType,
    decimal Amount,
    decimal BaseAmount,
    string? Notes,
    byte[] RowVersion);
