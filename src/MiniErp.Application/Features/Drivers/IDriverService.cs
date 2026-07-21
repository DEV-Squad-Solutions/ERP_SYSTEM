using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Drivers;

public interface IDriverService
{
    Task<Result<PagedResponse<DriverResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default);

    Task<Result<DriverResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<DriverResponse>> AddAsync(
        DriverRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<DriverResponse>> UpdateAsync(
        int id,
        DriverRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
