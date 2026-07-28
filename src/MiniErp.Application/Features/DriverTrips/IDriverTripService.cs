using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.DriverTrips;

public interface IDriverTripService
{
    Task<Result<PagedResponse<DriverTripCostResponse>>> GetCostEntryAsync(
        PaginationRequest pagination,
        DriverTripCostFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<DriverTripBulkCostUpdateResponse>> UpdateCostsAsync(
        DriverTripBulkCostUpdateRequest request,
        CancellationToken cancellationToken = default);
}
