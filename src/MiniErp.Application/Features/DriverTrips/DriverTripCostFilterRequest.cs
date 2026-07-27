namespace MiniErp.Application.Features.DriverTrips;

public sealed record DriverTripCostFilterRequest(
    string? Search = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int? DriverId = null,
    string? InvoiceNumber = null,
    string? TripNumber = null,
    bool? HasCost = null);
