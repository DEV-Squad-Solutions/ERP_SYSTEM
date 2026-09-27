using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Common.Abstractions;

public interface IFiscalYearInventoryCarryForwardService : IScopedService
{
    Task<Result> CarryForwardAsync(
        int sourceFiscalYearId,
        int targetFiscalYearId,
        DateOnly targetStartDate,
        string sourceFiscalYearName,
        CancellationToken cancellationToken = default);
}
