using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Features.FiscalYears;

public interface IFiscalYearService
{
    Task<Result<PagedResponse<FiscalYearResponse>>> GetAllAsync(
        PaginationRequest pagination,
        FiscalYearFilterRequest? filters = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<FiscalYearSelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default);

    Task<Result<FiscalYearResponse>> GetCurrentAsync(
        CancellationToken cancellationToken = default);

    Task<Result<FiscalYearResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<FiscalYearResponse>> AddAsync(
        FiscalYearRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<FiscalYearResponse>> UpdateAsync(
        int id,
        FiscalYearUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<FiscalYearResponse>> CloseAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<FiscalYearResponse>> ReopenAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
