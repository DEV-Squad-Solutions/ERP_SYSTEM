using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeOpeningBalances;

public sealed record EmployeeOpeningBalanceResponse(
    int Id,
    int CompanyId,
    int FiscalYearId,
    string FiscalYearName,
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
    byte[] RowVersion)
{
    public bool IsCarriedForward { get; init; }

    public bool IsReadOnly { get; init; }

    public JournalEntrySourceType? SourceType { get; init; }

    public int? JournalEntryId { get; init; }

    public int? JournalEntryLineId { get; init; }

    public int? SourceFiscalYearId { get; init; }

    public string? SourceFiscalYearName { get; init; }
}
