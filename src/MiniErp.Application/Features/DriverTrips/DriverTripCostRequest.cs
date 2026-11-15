using System.Text.Json.Serialization;

namespace MiniErp.Application.Features.DriverTrips;

public sealed record DriverTripCostUpdateItem(
    int DriverTripId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? Cost,
    string? Notes,
    byte[]? RowVersion);

public sealed record DriverTripBulkCostUpdateRequest(
    IReadOnlyList<DriverTripCostUpdateItem> Items)
{
    public const int MaximumItemCount = 100;

    public const int NotesMaximumLength = 1_000;
}
