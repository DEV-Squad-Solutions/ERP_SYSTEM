using Mapster;
using MiniErp.Domain.Entities.Logistics;

namespace MiniErp.Application.Features.DriverTrips;

public sealed class DriverTripCostMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<DriverTrip, DriverTripCostResponse>()
            .Map(response => response.DriverTripId, trip => trip.Id)
            .Map(
                response => response.TripNumber,
                trip => "TR-" + trip.Id)
            .Map(
                response => response.DriverName,
                trip => trip.Driver.Name)
            .Map(response => response.CostNotes, trip => trip.CostNotes);
    }
}
