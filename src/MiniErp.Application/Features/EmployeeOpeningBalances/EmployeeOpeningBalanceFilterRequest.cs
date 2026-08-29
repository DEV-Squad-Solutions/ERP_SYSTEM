using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeOpeningBalances;

public sealed record EmployeeOpeningBalanceFilterRequest
{
    public string? DocumentNumber { get; init; }

    public int? EmployeeId { get; init; }

    public int? PayrollEntryId { get; init; }

    public CurrencyCode? Currency { get; init; }

    public EmployeeBalanceType? BalanceType { get; init; }

    public DateOnly? FromDate { get; init; }

    public DateOnly? ToDate { get; init; }

    public string? Search { get; init; }
}
