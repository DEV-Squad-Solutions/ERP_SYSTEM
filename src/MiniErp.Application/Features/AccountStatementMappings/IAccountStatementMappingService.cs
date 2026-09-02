using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.AccountStatementMappings;

public interface IAccountStatementMappingService
{
    Task<Result<IReadOnlyList<AccountStatementMappingResponse>>> GetAsync(
        int fiscalYearId,
        FinancialStatementType statementType,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AccountStatementMappingResponse>>> ReplaceAsync(
        int fiscalYearId,
        FinancialStatementType statementType,
        ReplaceAccountStatementMappingsRequest request,
        CancellationToken cancellationToken = default);
}
