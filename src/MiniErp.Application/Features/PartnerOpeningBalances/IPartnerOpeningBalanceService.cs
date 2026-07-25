using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.PartnerOpeningBalances;

public interface IPartnerOpeningBalanceService
{
    Task<Result<PagedResponse<PartnerOpeningBalanceResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);

    Task<Result<PartnerOpeningBalanceResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<PartnerOpeningBalanceResponse>> AddAsync(
        PartnerOpeningBalanceRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PartnerOpeningBalanceResponse>> UpdateAsync(
        int id,
        PartnerOpeningBalanceUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
