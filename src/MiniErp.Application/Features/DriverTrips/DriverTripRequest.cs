using System.Text.Json.Serialization;

namespace MiniErp.Application.Features.DriverTrips;

public record DriverTripRequest(
    int DriverId,
    int FiscalYearId,
    DateOnly TripDate,
    int? BusinessPartnerId = null,
    string? ActualDriverName = null,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? Cost = null,
    string? Notes = null)
{
    public const decimal MaximumCost = 999999999999999.99m;
    public const int MaximumNotesLength = 1_000;
    public const int MaximumActualDriverNameLength = 200;
}

public sealed record DriverTripUpdateRequest(
    int DriverId,
    int FiscalYearId,
    DateOnly TripDate,
    int? BusinessPartnerId,
    string? ActualDriverName,
    decimal? Cost,
    string? Notes,
    byte[]? RowVersion)
    : DriverTripRequest(DriverId, FiscalYearId, TripDate, BusinessPartnerId, ActualDriverName, Cost, Notes);
