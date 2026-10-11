using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Users;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Users;

public sealed class CashboxAccessService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext companyContext,
    ICurrentUserService currentUserService) : ICashboxAccessService, IScopedService
{
    public async Task<Result<CashboxAccessScope>> GetScopeAsync(CancellationToken cancellationToken = default)
    {
        var user = currentUserService.GetUserId();
        if (user.IsFailure)
            return Result<CashboxAccessScope>.Failure(user.Errors);

        var membership = await dbContext.UserCompanies.AsNoTracking()
            .Where(mapping => mapping.UserId == user.Value && mapping.CompanyId == companyContext.CompanyId)
            .Select(mapping => new { mapping.AllCashboxes })
            .SingleOrDefaultAsync(cancellationToken);
        if (membership is null)
            return Result<CashboxAccessScope>.Failure(CashboxPermissionErrors.CompanyAccessDenied());

        var isAdmin = await dbContext.UserRoles.AsNoTracking()
            .Where(mapping => mapping.UserId == user.Value)
            .Join(dbContext.Roles, mapping => mapping.RoleId, role => role.Id, (_, role) => role.Name)
            .AnyAsync(role => role == ApplicationRoles.Admin, cancellationToken);
        if (isAdmin || membership.AllCashboxes)
            return Result<CashboxAccessScope>.Success(new CashboxAccessScope(
                HasAllCashboxes: true, ViewCashboxIds: [], ExecuteCashboxIds: []));

        var permissions = await dbContext.UserCashboxPermissions.AsNoTracking()
            .Where(permission => permission.UserId == user.Value && permission.CompanyId == companyContext.CompanyId)
            .Select(permission => new { permission.CashboxId, permission.CanView, permission.CanExecute })
            .ToListAsync(cancellationToken);
        return Result<CashboxAccessScope>.Success(new CashboxAccessScope(
            HasAllCashboxes: false,
            ViewCashboxIds: permissions.Where(permission => permission.CanView || permission.CanExecute).Select(permission => permission.CashboxId).ToArray(),
            ExecuteCashboxIds: permissions.Where(permission => permission.CanExecute).Select(permission => permission.CashboxId).ToArray()));
    }

    public Task<Result> EnsureViewAsync(int cashboxId, CancellationToken cancellationToken = default) =>
        EnsureAsync(cashboxId, execute: false, cancellationToken);

    public Task<Result> EnsureExecuteAsync(int cashboxId, CancellationToken cancellationToken = default) =>
        EnsureAsync(cashboxId, execute: true, cancellationToken);

    private async Task<Result> EnsureAsync(int cashboxId, bool execute, CancellationToken cancellationToken)
    {
        var scope = await GetScopeAsync(cancellationToken);
        if (scope.IsFailure)
            return Result.Failure(scope.Errors);

        var inCompany = await dbContext.Cashboxes.AsNoTracking().AnyAsync(
            cashbox => cashbox.Id == cashboxId && cashbox.CompanyId == companyContext.CompanyId,
            cancellationToken);
        if (!inCompany || !(execute ? scope.Value.CanExecute(cashboxId) : scope.Value.CanView(cashboxId)))
            return Result.Failure(execute ? CashboxPermissionErrors.ExecuteDenied() : CashboxPermissionErrors.ViewDenied());

        return Result.Success();
    }
}
