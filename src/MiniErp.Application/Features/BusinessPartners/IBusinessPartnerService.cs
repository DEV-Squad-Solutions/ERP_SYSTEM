using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.BusinessPartners;


public interface IBusinessPartnerService
{
    Task<Result<PagedResponse<BusinessPartnerResponse>>> GetAllAsync(
        PaginationRequest pagination,
        BusinessPartnerFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<BusinessPartnerSelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default);

    Task<Result<BusinessPartnerResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<BusinessPartnerContainerStoreResponse>> GetContainerStoreAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<BusinessPartnerResponse>> AddAsync(
        BusinessPartnerRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<BusinessPartnerResponse>> UpdateAsync(
        int id,
        BusinessPartnerRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
