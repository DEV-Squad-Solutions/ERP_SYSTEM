using MiniErp.Application.Common.Results;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.AccountMappings;

public interface IAccountMappingResolver
{
    Task<Result<int>> ResolveAsync(
        int fiscalYearId,
        AccountingMappingType mappingType,
        int? sourceId = null,
        CancellationToken cancellationToken = default);
}
