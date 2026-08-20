namespace MiniErp.Application.Features.DriverTrips;

public sealed record DriverTripCostResponse(
    int DriverTripId,
    string TripNumber,
    DateOnly TripDate,
    int InvoiceId,
    string InvoiceNumber,
    int BusinessPartnerId,
    string BusinessPartnerName,
    int DriverId,
    string DriverName,
    decimal? Cost,
    string? CostNotes,
    byte[] RowVersion)
{
    public string? CountryName { get; init; }
}

public sealed record DriverTripBulkCostUpdateResponse(
    IReadOnlyList<DriverTripCostResponse> Items);
