using System.Globalization;
using System.Text.Json.Serialization;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.DriverOpeningBalances;

public record DriverOpeningBalanceRequest(
    int DriverId,
    int FiscalYearId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal Amount,
    DriverOpeningBalanceType BalanceType,
    string? Notes)
{
    public const decimal MaximumAmount = 999_999_999_999_999.99m;
    public const int MaximumNotesLength = 1_000;
}

public sealed record DriverOpeningBalanceUpdateRequest(
    int DriverId,
    int FiscalYearId,
    decimal Amount,
    DriverOpeningBalanceType BalanceType,
    string? Notes,
    byte[]? RowVersion)
    : DriverOpeningBalanceRequest(DriverId, FiscalYearId, Amount, BalanceType, Notes);

public sealed record DriverOpeningBalanceResponse(
    int Id,
    int DriverId,
    string DriverName,
    int FiscalYearId,
    string FiscalYearName,
    string DocumentNumber,
    DateOnly DocumentDate,
    decimal Amount,
    CurrencyCode Currency,
    DriverOpeningBalanceType BalanceType,
    string? Notes,
    bool IsCarriedForward,
    bool IsReadOnly,
    byte[]? RowVersion)
{
    public string AmountText => Amount.ToString("0.00##", CultureInfo.InvariantCulture);
}

public interface IDriverOpeningBalanceService
{
    Task<Result<PagedResponse<DriverOpeningBalanceResponse>>> GetAllAsync(
        PaginationRequest pagination,
        int? fiscalYearId,
        int? driverId,
        CancellationToken cancellationToken = default);

    Task<Result<DriverOpeningBalanceResponse>> GetByIdAsync(
        int id, int? fiscalYearId, CancellationToken cancellationToken = default);

    Task<Result<DriverOpeningBalanceResponse>> AddAsync(
        DriverOpeningBalanceRequest request, CancellationToken cancellationToken = default);

    Task<Result<DriverOpeningBalanceResponse>> UpdateAsync(
        int id, DriverOpeningBalanceUpdateRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int id, byte[]? rowVersion, CancellationToken cancellationToken = default);
}
