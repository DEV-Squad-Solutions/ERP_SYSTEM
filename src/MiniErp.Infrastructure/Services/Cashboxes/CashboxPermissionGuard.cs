using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;

namespace MiniErp.Infrastructure.Services.Cashboxes;

// The nullable dependency preserves standalone service fixtures. Production DI
// registers the access service and always supplies it to these constructors.
internal static class CashboxPermissionGuard
{
    internal static Task<Result<CashboxAccessScope>> ScopeAsync(
        ICashboxAccessService? access, CancellationToken cancellationToken) =>
        access?.GetScopeAsync(cancellationToken) ??
        Task.FromResult(Result<CashboxAccessScope>.Success(new CashboxAccessScope(
            HasAllCashboxes: true, ViewCashboxIds: [], ExecuteCashboxIds: [])));

    internal static Task<Result> ViewAsync(
        ICashboxAccessService? access, int id, CancellationToken cancellationToken) =>
        access?.EnsureViewAsync(id, cancellationToken) ?? Task.FromResult(Result.Success());

    internal static Task<Result> ExecuteAsync(
        ICashboxAccessService? access, int id, CancellationToken cancellationToken) =>
        access?.EnsureExecuteAsync(id, cancellationToken) ?? Task.FromResult(Result.Success());

    internal static Error Forbidden() => Error.Forbidden(
        "Cashboxes.AccessDenied", "ليس لديك صلاحية الوصول إلى هذه الخزنة.");
}
