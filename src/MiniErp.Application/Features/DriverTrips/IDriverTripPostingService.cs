using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.DriverTrips;

public interface IDriverTripPostingService
{
    Task<Result> SynchronizeAsync(
        int driverTripId,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int driverTripId,
        CancellationToken cancellationToken = default);
}
