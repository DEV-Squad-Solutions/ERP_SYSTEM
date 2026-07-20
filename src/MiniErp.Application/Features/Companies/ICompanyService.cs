using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.Companies;

public interface ICompanyService
{
    Task<Result<PagedResponse<CompanyResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default);

    Task<Result<CompanyResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<CompanyResponse>> AddAsync(
        CompanyRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CompanyResponse>> UpdateAsync(
        int id,
        CompanyRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
