using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.DriverTrips;

public interface IDriverTripService
{
    Task<Result<DriverTripCostResponse>> GetByIdAsync(int id, int? fiscalYearId = null, CancellationToken cancellationToken = default);
    Task<Result<DriverTripCostResponse>> AddAsync(DriverTripRequest request, CancellationToken cancellationToken = default);
    Task<Result<DriverTripCostResponse>> UpdateAsync(int id, DriverTripUpdateRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int id, byte[]? rowVersion, CancellationToken cancellationToken = default);

    Task<Result<PagedResponse<DriverTripCostResponse>>> GetCostEntryAsync(
        PaginationRequest pagination,
        DriverTripCostFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<DriverTripBulkCostUpdateResponse>> UpdateCostsAsync(
        DriverTripBulkCostUpdateRequest request,
        CancellationToken cancellationToken = default);
}
