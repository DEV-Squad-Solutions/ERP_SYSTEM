using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Users;
using MiniErp.Infrastructure.Identity;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Users;

public sealed class UserService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    ICurrentUserService currentUserService)
    : IUserService, IScopedService
{
    public async Task<Result<PagedResponse<UserResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        if (pagination.PageNumber <= 0 ||
            pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize)
        {
            return Result<PagedResponse<UserResponse>>.Failure(InvalidPagination());
        }

        var query = dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.UserName)
            .ThenBy(user => user.Id);
        var totalCount = await query.CountAsync(cancellationToken);
        var offset = (long)(pagination.PageNumber - 1) * pagination.PageSize;

        IReadOnlyList<UserResponse> users = offset >= totalCount
            ? []
            : await Project(query)
                .Skip((int)offset)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pagination.PageSize);

        return Result<PagedResponse<UserResponse>>.Success(
            new PagedResponse<UserResponse>(
                users,
                pagination.PageNumber,
                pagination.PageSize,
                totalCount,
                totalPages));
    }

    public async Task<Result<UserResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return Result<UserResponse>.Failure(InvalidId());
        }

        var user = await Project(
                dbContext.Users
                    .AsNoTracking()
                    .Where(user => user.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

        return user is null
            ? Result<UserResponse>.Failure(NotFound(id))
            : Result<UserResponse>.Success(user);
    }

    public async Task<Result<IReadOnlyList<string>>> GetRolesAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> roles = await roleManager.Roles
            .AsNoTracking()
            .Where(role => role.Name != null)
            .OrderBy(role => role.Name)
            .Select(role => role.Name!)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<string>>.Success(roles);
    }

    public async Task<Result<UserResponse>> AddAsync(
        UserCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = request.Adapt<ApplicationUser>();
        var duplicateError = await FindDuplicateAsync(
            user.UserName!,
            user.Email!,
            excludedId: null);
        if (duplicateError is not null)
        {
            return Result<UserResponse>.Failure(duplicateError);
        }

        var rolesResult = await ResolveRolesAsync(
            request.Roles,
            cancellationToken);
        if (rolesResult.IsFailure)
        {
            return Result<UserResponse>.Failure(rolesResult.Error);
        }

        var companiesResult = await GetCompaniesAsync(
            request.CompanyIds,
            cancellationToken);
        if (companiesResult.IsFailure)
        {
            return Result<UserResponse>.Failure(companiesResult.Error);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Result<UserResponse>.Failure(IdentityError(createResult));
        }

        var addRolesResult = await userManager.AddToRolesAsync(
            user,
            rolesResult.Value);
        if (!addRolesResult.Succeeded)
        {
            return Result<UserResponse>.Failure(IdentityError(addRolesResult));
        }

        dbContext.UserCompanies.AddRange(
            companiesResult.Value.Select(company => new UserCompany
            {
                UserId = user.Id,
                CompanyId = company.Id
            }));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<UserResponse>.Success(
            ToResponse(user, rolesResult.Value, companiesResult.Value));
    }

    public async Task<Result<UserResponse>> UpdateAsync(
        Guid id,
        UserUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return Result<UserResponse>.Failure(InvalidId());
        }

        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Result<UserResponse>.Failure(NotFound(id));
        }

        var normalizedUser = request.Adapt<ApplicationUser>();
        var duplicateError = await FindDuplicateAsync(
            normalizedUser.UserName!,
            normalizedUser.Email!,
            id);
        if (duplicateError is not null)
        {
            return Result<UserResponse>.Failure(duplicateError);
        }

        var rolesResult = await ResolveRolesAsync(
            request.Roles,
            cancellationToken);
        if (rolesResult.IsFailure)
        {
            return Result<UserResponse>.Failure(rolesResult.Error);
        }

        var adminRoleError = await ValidateAdminRoleChangeAsync(
            user,
            rolesResult.Value);
        if (adminRoleError is not null)
        {
            return Result<UserResponse>.Failure(adminRoleError);
        }

        var companiesResult = await GetCompaniesAsync(
            request.CompanyIds,
            cancellationToken);
        if (companiesResult.IsFailure)
        {
            return Result<UserResponse>.Failure(companiesResult.Error);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        request.Adapt(user);
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Result<UserResponse>.Failure(IdentityError(updateResult));
        }

        var rolesUpdateResult = await SyncRolesAsync(user, rolesResult.Value);
        if (rolesUpdateResult.IsFailure)
        {
            return Result<UserResponse>.Failure(rolesUpdateResult.Error);
        }

        await SyncCompaniesAsync(
            user.Id,
            companiesResult.Value.Select(company => company.Id).ToHashSet(),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<UserResponse>.Success(
            ToResponse(user, rolesResult.Value, companiesResult.Value));
    }

    public async Task<Result<UserResponse>> AssignCompaniesAsync(
        Guid id,
        UserCompaniesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return Result<UserResponse>.Failure(InvalidId());
        }

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Result<UserResponse>.Failure(NotFound(id));
        }

        var companiesResult = await GetCompaniesAsync(
            request.CompanyIds,
            cancellationToken);
        if (companiesResult.IsFailure)
        {
            return Result<UserResponse>.Failure(companiesResult.Error);
        }

        await SyncCompaniesAsync(
            user.Id,
            companiesResult.Value.Select(company => company.Id).ToHashSet(),
            cancellationToken);

        var roles = await userManager.GetRolesAsync(user);
        return Result<UserResponse>.Success(
            ToResponse(
                user,
                roles.OrderBy(role => role).ToArray(),
                companiesResult.Value));
    }

    public async Task<Result> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure(InvalidId());
        }

        var currentUserResult = currentUserService.GetUserId();
        if (currentUserResult.IsFailure)
        {
            return Result.Failure(currentUserResult.Error);
        }

        if (currentUserResult.Value == id)
        {
            return Result.Failure(
                Error.Conflict(
                    "Users.CannotDeleteCurrentUser",
                    "The currently logged-in user cannot delete their own account."));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (await userManager.IsInRoleAsync(user, ApplicationRoles.Admin))
        {
            var admins = await userManager.GetUsersInRoleAsync(ApplicationRoles.Admin);
            if (admins.Count <= 1)
            {
                return Result.Failure(LastAdminError());
            }
        }

        var deleteResult = await userManager.DeleteAsync(user);
        return deleteResult.Succeeded
            ? Result.Success()
            : Result.Failure(IdentityError(deleteResult));
    }

    private IQueryable<UserResponse> Project(IQueryable<ApplicationUser> users) =>
        users.Select(user => new UserResponse(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            dbContext.UserRoles
                .Where(userRole => userRole.UserId == user.Id)
                .Join(
                    dbContext.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (_, role) => role.Name)
                .Where(roleName => roleName != null)
                .OrderBy(roleName => roleName)
                .Select(roleName => roleName!)
                .ToList(),
            user.UserCompanies
                .OrderBy(userCompany => userCompany.Company.Name)
                .ThenBy(userCompany => userCompany.CompanyId)
                .Select(userCompany => new UserCompanyResponse(
                    userCompany.CompanyId,
                    userCompany.Company.Name))
                .ToList()));

    private async Task<Error?> FindDuplicateAsync(
        string userName,
        string email,
        Guid? excludedId)
    {
        var userWithName = await userManager.FindByNameAsync(userName);
        if (userWithName is not null && userWithName.Id != excludedId)
        {
            return Error.Conflict(
                "Users.UserNameExists",
                $"Username '{userName}' already exists.");
        }

        var userWithEmail = await userManager.FindByEmailAsync(email);
        return userWithEmail is not null && userWithEmail.Id != excludedId
            ? Error.Conflict(
                "Users.EmailExists",
                $"Email '{email}' already exists.")
            : null;
    }

    private async Task<Result<List<string>>> ResolveRolesAsync(
        IReadOnlyCollection<string> requestedRoles,
        CancellationToken cancellationToken)
    {
        var normalizedRoles = requestedRoles
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var roles = await roleManager.Roles
            .AsNoTracking()
            .Where(role => role.Name != null)
            .Select(role => role.Name!)
            .ToListAsync(cancellationToken);
        var resolvedRoles = roles
            .Where(role => normalizedRoles.Contains(
                role,
                StringComparer.OrdinalIgnoreCase))
            .OrderBy(role => role)
            .ToList();

        if (resolvedRoles.Count == normalizedRoles.Length)
        {
            return Result<List<string>>.Success(resolvedRoles);
        }

        var missingRoles = normalizedRoles.Except(
            resolvedRoles,
            StringComparer.OrdinalIgnoreCase);
        return Result<List<string>>.Failure(
            Error.NotFound(
                "Users.RolesNotFound",
                $"Roles were not found: {string.Join(", ", missingRoles)}."));
    }

    private async Task<Result<List<UserCompanyResponse>>> GetCompaniesAsync(
        IReadOnlyCollection<int> requestedCompanyIds,
        CancellationToken cancellationToken)
    {
        var companyIds = requestedCompanyIds.Distinct().ToArray();
        var companies = await dbContext.Companies
            .AsNoTracking()
            .Where(company => companyIds.Contains(company.Id))
            .OrderBy(company => company.Name)
            .ThenBy(company => company.Id)
            .Select(company => new UserCompanyResponse(company.Id, company.Name))
            .ToListAsync(cancellationToken);

        if (companies.Count == companyIds.Length)
        {
            return Result<List<UserCompanyResponse>>.Success(companies);
        }

        var foundIds = companies.Select(company => company.Id).ToHashSet();
        var missingIds = companyIds.Where(id => !foundIds.Contains(id));
        return Result<List<UserCompanyResponse>>.Failure(
            Error.NotFound(
                "Users.CompaniesNotFound",
                $"Companies were not found or are deleted: {string.Join(", ", missingIds)}."));
    }

    private async Task SyncCompaniesAsync(
        Guid userId,
        HashSet<int> requestedCompanyIds,
        CancellationToken cancellationToken)
    {
        var assignments = await dbContext.UserCompanies
            .IgnoreQueryFilters()
            .Where(userCompany => userCompany.UserId == userId)
            .ToListAsync(cancellationToken);

        dbContext.UserCompanies.RemoveRange(
            assignments.Where(assignment =>
                !requestedCompanyIds.Contains(assignment.CompanyId)));

        var existingCompanyIds = assignments
            .Select(assignment => assignment.CompanyId)
            .ToHashSet();
        dbContext.UserCompanies.AddRange(
            requestedCompanyIds
                .Where(companyId => !existingCompanyIds.Contains(companyId))
                .Select(companyId => new UserCompany
                {
                    UserId = userId,
                    CompanyId = companyId
                }));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Result> SyncRolesAsync(
        ApplicationUser user,
        IReadOnlyCollection<string> requestedRoles)
    {
        var currentRoles = await userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles
            .Where(currentRole => !requestedRoles.Contains(
                currentRole,
                StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (rolesToRemove.Length > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(
                user,
                rolesToRemove);
            if (!removeResult.Succeeded)
            {
                return Result.Failure(IdentityError(removeResult));
            }
        }

        var rolesToAdd = requestedRoles
            .Where(role => !currentRoles.Contains(
                role,
                StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (rolesToAdd.Length > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                return Result.Failure(IdentityError(addResult));
            }
        }

        return Result.Success();
    }

    private async Task<Error?> ValidateAdminRoleChangeAsync(
        ApplicationUser user,
        IReadOnlyCollection<string> requestedRoles)
    {
        if (!await userManager.IsInRoleAsync(user, ApplicationRoles.Admin) ||
            requestedRoles.Contains(
                ApplicationRoles.Admin,
                StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var admins = await userManager.GetUsersInRoleAsync(ApplicationRoles.Admin);
        return admins.Count <= 1 ? LastAdminError() : null;
    }

    private static UserResponse ToResponse(
        ApplicationUser user,
        IReadOnlyList<string> roles,
        IReadOnlyList<UserCompanyResponse> companies) =>
        new(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            roles,
            companies);

    private static Error IdentityError(IdentityResult result) =>
        Error.Validation(
            "Users.IdentityValidation",
            string.Join(
                "; ",
                result.Errors.Select(error => $"{error.Code}: {error.Description}")));

    private static Error InvalidId() =>
        Error.Validation("Users.InvalidId", "User ID must not be empty.");

    private static Error NotFound(Guid id) =>
        Error.NotFound("Users.NotFound", $"User with ID {id} was not found.");

    private static Error InvalidPagination() =>
        Error.Validation(
            "Pagination.Invalid",
            $"Page number must be greater than zero and page size must be between 1 and {PaginationRequest.MaxPageSize}.");

    private static Error LastAdminError() =>
        Error.Conflict(
            "Users.LastAdmin",
            "The last Admin user cannot be deleted, and the Admin role cannot be removed from that account.");
}
