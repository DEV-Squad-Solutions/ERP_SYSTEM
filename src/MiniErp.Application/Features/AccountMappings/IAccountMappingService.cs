using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.AccountMappings;

public interface IAccountMappingService
{
    Task<Result<IReadOnlyList<AccountMappingResponse>>> GetAsync(
        int fiscalYearId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AccountMappingResponse>>> ReplaceAsync(
        int fiscalYearId,
        ReplaceAccountMappingsRequest request,
        CancellationToken cancellationToken = default);
}
