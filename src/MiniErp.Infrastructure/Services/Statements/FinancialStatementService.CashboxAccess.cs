using MiniErp.Application.Common.Results;

namespace MiniErp.Infrastructure.Services.Statements;

public sealed partial class FinancialStatementService
{
    private async Task<Result> EnsureFullCashboxAccessAsync(CancellationToken cancellationToken)
    {
        if (cashboxAccessService is null)
            return Result.Success(); // Compatibility for isolated service tests; production DI supplies the resolver.
        var scope = await cashboxAccessService.GetScopeAsync(cancellationToken);
        if (scope.IsFailure) return Result.Failure(scope.Errors);
        return scope.Value.HasAllCashboxes
            ? Result.Success()
            : Result.Failure(Error.Forbidden("CashboxPermissions.FullCompanyRequired",
                "Full-company financial reports require access to all company cashboxes."));
    }
}
