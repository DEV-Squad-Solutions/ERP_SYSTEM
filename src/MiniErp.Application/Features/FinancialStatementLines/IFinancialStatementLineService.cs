using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.FinancialStatementLines;

public interface IFinancialStatementLineService
{
    Task<Result<PagedResponse<FinancialStatementLineResponse>>> GetAllAsync(
        PaginationRequest pagination,
        FinancialStatementLineFilterRequest filters,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<FinancialStatementLineTreeResponse>>> GetTreeAsync(
        int fiscalYearId,
        FinancialStatementType statementType,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<FinancialStatementLineSelectResponse>>> GetSelectAsync(
        int fiscalYearId,
        FinancialStatementType statementType,
        CancellationToken cancellationToken = default);

    Task<Result<FinancialStatementLineResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Result<FinancialStatementLineResponse>> AddAsync(
        FinancialStatementLineRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<FinancialStatementLineResponse>> UpdateAsync(
        int id,
        FinancialStatementLineUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
