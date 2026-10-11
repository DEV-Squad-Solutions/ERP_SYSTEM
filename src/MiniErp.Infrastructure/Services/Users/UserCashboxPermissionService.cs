using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Users;
using MiniErp.Infrastructure.Identity;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Users;

public sealed class UserCashboxPermissionService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext companyContext,
    ICurrentUserService currentUserService,
    ILogger<UserCashboxPermissionService> logger) : IUserCashboxPermissionService, IScopedService
{
    public async Task<Result<UserCashboxPermissionsResponse>> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var authorized = await EnsureAdminAsync(cancellationToken);
        if (authorized.IsFailure)
            return Result<UserCashboxPermissionsResponse>.Failure(authorized.Errors);

        var membership = await dbContext.UserCompanies.AsNoTracking()
            .SingleOrDefaultAsync(mapping => mapping.UserId == userId && mapping.CompanyId == companyContext.CompanyId, cancellationToken);
        if (membership is null)
            return Result<UserCashboxPermissionsResponse>.Failure(CashboxPermissionErrors.UserNotInCompany());

        var grants = await dbContext.UserCashboxPermissions.AsNoTracking()
            .Where(permission => permission.UserId == userId && permission.CompanyId == companyContext.CompanyId)
            .OrderBy(permission => permission.CashboxId)
            .Select(permission => new { permission.CashboxId, permission.CanView, permission.CanExecute })
            .ToListAsync(cancellationToken);
        return Result<UserCashboxPermissionsResponse>.Success(ToResponse(membership, grants.Select(grant => new UserCashboxPermissionGrant(
            CashboxId: grant.CashboxId, CanView: grant.CanView || grant.CanExecute, CanExecute: grant.CanExecute)).ToArray()));
    }

    public async Task<Result<UserCashboxPermissionsResponse>> ReplaceAsync(
        Guid userId, UserCashboxPermissionsRequest request, CancellationToken cancellationToken = default)
    {
        var authorized = await EnsureAdminAsync(cancellationToken);
        if (authorized.IsFailure)
            return Result<UserCashboxPermissionsResponse>.Failure(authorized.Errors);
        if (request.Permissions is null || request.Permissions.Any(grant => grant is null || grant.CashboxId <= 0 || (!grant.CanView && !grant.CanExecute)))
            return Result<UserCashboxPermissionsResponse>.Failure(CashboxPermissionErrors.InvalidRequest());

        var grants = request.Permissions.GroupBy(grant => grant.CashboxId)
            .Select(group => new UserCashboxPermissionGrant(
                CashboxId: group.Key,
                CanView: group.Any(grant => grant.CanView || grant.CanExecute),
                CanExecute: group.Any(grant => grant.CanExecute)))
            .OrderBy(grant => grant.CashboxId).ToArray();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var membership = await dbContext.UserCompanies
            .SingleOrDefaultAsync(mapping => mapping.UserId == userId && mapping.CompanyId == companyContext.CompanyId, cancellationToken);
        if (membership is null)
            return Result<UserCashboxPermissionsResponse>.Failure(CashboxPermissionErrors.UserNotInCompany());
        if (membership.CashboxPermissionsRevision != request.Revision)
            return Result<UserCashboxPermissionsResponse>.Failure(CashboxPermissionErrors.Conflict());

        // Include deleted cashboxes here so replacing grants also cleans up old assignments.
        var existing = await dbContext.UserCashboxPermissions.IgnoreQueryFilters()
            .Where(permission => permission.UserId == userId && permission.CompanyId == companyContext.CompanyId)
            .ToListAsync(cancellationToken);
        var existingIds = existing.Select(permission => permission.CashboxId).ToHashSet();
        var requestedIds = grants.Select(grant => grant.CashboxId).ToArray();
        var validCashboxes = await dbContext.Cashboxes.AsNoTracking()
            .Where(cashbox => cashbox.CompanyId == companyContext.CompanyId && requestedIds.Contains(cashbox.Id))
            .Select(cashbox => new { cashbox.Id, cashbox.IsActive })
            .ToListAsync(cancellationToken);
        if (validCashboxes.Count != requestedIds.Length || validCashboxes.Any(cashbox => !cashbox.IsActive && !existingIds.Contains(cashbox.Id)))
            return Result<UserCashboxPermissionsResponse>.Failure(CashboxPermissionErrors.InvalidCashboxes());

        var requestedById = grants.ToDictionary(grant => grant.CashboxId);
        foreach (var permission in existing)
        {
            if (!requestedById.TryGetValue(permission.CashboxId, out var grant))
                dbContext.UserCashboxPermissions.Remove(permission);
            else
            {
                permission.CanView = grant.CanView;
                permission.CanExecute = grant.CanExecute;
            }
        }
        dbContext.UserCashboxPermissions.AddRange(grants.Where(grant => !existingIds.Contains(grant.CashboxId))
            .Select(grant => new UserCashboxPermission
            {
                UserId = userId,
                CompanyId = companyContext.CompanyId,
                CashboxId = grant.CashboxId,
                CanView = grant.CanView,
                CanExecute = grant.CanExecute
            }));
        membership.AllCashboxes = request.AllCashboxes;
        membership.CashboxPermissionsRevision = Guid.NewGuid();
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<UserCashboxPermissionsResponse>.Failure(CashboxPermissionErrors.Conflict());
        }

        logger.LogInformation(
            "Cashbox permissions changed by {ActorUserId} for user {UserId} in company {CompanyId}: all={AllCashboxes}, grants={GrantCount}, revision={Revision}.",
            authorized.Value, userId, companyContext.CompanyId, membership.AllCashboxes, grants.Length, membership.CashboxPermissionsRevision);
        return Result<UserCashboxPermissionsResponse>.Success(ToResponse(membership, grants));
    }

    private async Task<Result<Guid>> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        var actor = currentUserService.GetUserId();
        if (actor.IsFailure)
            return actor;
        if (!await dbContext.UserCompanies.AsNoTracking().AnyAsync(
                mapping => mapping.UserId == actor.Value && mapping.CompanyId == companyContext.CompanyId, cancellationToken))
            return Result<Guid>.Failure(CashboxPermissionErrors.CompanyAccessDenied());
        var isAdmin = await dbContext.UserRoles.AsNoTracking()
            .Where(mapping => mapping.UserId == actor.Value)
            .Join(dbContext.Roles, mapping => mapping.RoleId, role => role.Id, (_, role) => role.Name)
            .AnyAsync(role => role == ApplicationRoles.Admin, cancellationToken);
        return isAdmin ? actor : Result<Guid>.Failure(CashboxPermissionErrors.AdminRequired());
    }

    private static UserCashboxPermissionsResponse ToResponse(UserCompany membership, IReadOnlyList<UserCashboxPermissionGrant> grants) => new(
        UserId: membership.UserId,
        CompanyId: membership.CompanyId,
        AllCashboxes: membership.AllCashboxes,
        Revision: membership.CashboxPermissionsRevision,
        Permissions: grants);
}
