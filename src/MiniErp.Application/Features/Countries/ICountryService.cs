using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Countries;

public interface ICountryService
{
    Task<Result<PagedResponse<CountryResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default);

    Task<Result<CountryResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<CountryResponse>> AddAsync(
        CountryRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CountryResponse>> UpdateAsync(
        int id,
        CountryRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
