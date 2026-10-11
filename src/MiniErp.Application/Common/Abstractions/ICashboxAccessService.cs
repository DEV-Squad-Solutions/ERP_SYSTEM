using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Common.Abstractions;

public interface ICashboxAccessService
{
    Task<Result<CashboxAccessScope>> GetScopeAsync(CancellationToken cancellationToken = default);
    Task<Result> EnsureViewAsync(int cashboxId, CancellationToken cancellationToken = default);
    Task<Result> EnsureExecuteAsync(int cashboxId, CancellationToken cancellationToken = default);
}
