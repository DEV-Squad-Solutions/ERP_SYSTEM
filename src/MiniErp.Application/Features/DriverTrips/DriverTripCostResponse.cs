using System.Globalization;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.DriverTrips;

public sealed record DriverTripCostResponse(
    int DriverTripId,
    string TripNumber,
    DateOnly TripDate,
    int FiscalYearId,
    string FiscalYearName,
    int? InvoiceId,
    string? InvoiceNumber,
    int? BusinessPartnerId,
    string? BusinessPartnerName,
    int DriverId,
    string DriverName,
    decimal? Cost,
    string? CostNotes,
    byte[] RowVersion)
{
    public string? CountryName { get; init; }
    public string? ActualDriverName { get; init; }
    public bool IsStandalone => InvoiceId is null;
    public bool IsReadOnly { get; init; }
    public CurrencyCode BaseCurrency { get; init; }
    public string? CostText => Cost?.ToString("0.00", CultureInfo.InvariantCulture);
}

public sealed record DriverTripBulkCostUpdateResponse(
    IReadOnlyList<DriverTripCostResponse> Items);
