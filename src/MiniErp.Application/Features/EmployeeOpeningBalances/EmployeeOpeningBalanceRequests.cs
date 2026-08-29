using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.EmployeeOpeningBalances;

public sealed record EmployeeOpeningBalanceRequest(
    int EmployeeId,
    DateOnly DocumentDate,
    CurrencyCode Currency,
    EmployeeBalanceType BalanceType,
    decimal Amount,
    string? Notes,
    decimal? ExchangeRate = null)
{
    public const int NotesMaximumLength = 1_000;
}

public sealed record EmployeeOpeningBalanceUpdateRequest(
    int EmployeeId,
    DateOnly DocumentDate,
    CurrencyCode Currency,
    EmployeeBalanceType BalanceType,
    decimal Amount,
    string? Notes,
    byte[]? RowVersion,
    decimal? ExchangeRate = null);
