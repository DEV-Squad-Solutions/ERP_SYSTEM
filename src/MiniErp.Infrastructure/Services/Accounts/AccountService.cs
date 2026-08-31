using System.Data;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Accounts;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.Accounts.AccountErrors;

namespace MiniErp.Infrastructure.Services.Accounts;

public sealed class AccountService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : IAccountService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<AccountResponse>>> GetAllAsync(
        PaginationRequest pagination,
        AccountFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new AccountFilterRequest();
        var search = filters.Search?.Trim();

        var query = dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.CompanyId == companyId)
            .Where(account =>
                string.IsNullOrEmpty(search) ||
                account.Code.Contains(search) ||
                account.Name.Contains(search))
            .Where(account =>
                !filters.AccountType.HasValue ||
                account.AccountType == filters.AccountType.Value)
            .Where(account =>
                !filters.NormalBalance.HasValue ||
                account.NormalBalance == filters.NormalBalance.Value)
            .Where(account =>
                !filters.ParentAccountId.HasValue ||
                account.ParentAccountId == filters.ParentAccountId.Value)
            .Where(account =>
                !filters.IsPosting.HasValue ||
                account.IsPosting == filters.IsPosting.Value)
            .Where(account =>
                !filters.IsActive.HasValue ||
                account.IsActive == filters.IsActive.Value)
            .OrderBy(account => account.Code)
            .ThenBy(account => account.Id);

        return await paginationService.PaginateAsync<Account, AccountResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<AccountTreeResponse>>> GetTreeAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.CompanyId == companyId)
            .OrderBy(account => account.Code)
            .ThenBy(account => account.Id)
            .Select(account => new AccountTreeRow
            {
                Id = account.Id,
                Code = account.Code,
                Name = account.Name,
                ParentAccountId = account.ParentAccountId,
                AccountType = account.AccountType,
                NormalBalance = account.NormalBalance,
                IsPosting = account.IsPosting,
                IsActive = account.IsActive,
                RowVersion = account.RowVersion
            })
            .ToListAsync(cancellationToken);

        var childrenByParent = rows.ToLookup(row => row.ParentAccountId);
        IReadOnlyList<AccountTreeResponse> response = BuildTree(
            parentAccountId: null,
            childrenByParent);

        return Result<IReadOnlyList<AccountTreeResponse>>.Success(response);
    }

    public async Task<Result<IReadOnlyList<AccountSelectResponse>>> GetSelectAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Accounts
            .AsNoTracking()
            .Where(account =>
                account.CompanyId == companyId &&
                account.IsActive &&
                account.IsPosting)
            .OrderBy(account => account.Code)
            .ThenBy(account => account.Id)
            .Select(account => new
            {
                account.Id,
                account.Code,
                account.Name,
                account.AccountType
            })
            .ToListAsync(cancellationToken);

        IReadOnlyList<AccountSelectResponse> response = rows
            .Select(row => new AccountSelectResponse(
                Id: row.Id,
                Code: row.Code,
                Name: row.Name,
                AccountType: row.AccountType))
            .ToList();

        return Result<IReadOnlyList<AccountSelectResponse>>.Success(response);
    }

    public async Task<Result<AccountResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<AccountResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<AccountResponse>.Failure(NotFound(id))
            : Result<AccountResponse>.Success(response);
    }

    public async Task<Result<AccountResponse>> AddAsync(
        AccountRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var normalizedCode = request.Code.Trim();
        if (await CodeExistsAsync(normalizedCode, excludedId: null, cancellationToken))
        {
            return Result<AccountResponse>.Failure(CodeExists(normalizedCode));
        }

        var parentValidation = await ValidateParentAsync(
            request.ParentAccountId,
            accountId: null,
            cancellationToken);
        if (parentValidation.IsFailure)
        {
            return Result<AccountResponse>.Failure(parentValidation.Errors);
        }

        var account = request.Adapt<Account>();
        account.CompanyId = companyId;
        ApplyInheritedClassification(account, parentValidation.Value);
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ProjectResponseQuery(account.Id)
            .FirstAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<AccountResponse>.Success(response);
    }

    public async Task<Result<AccountResponse>> UpdateAsync(
        int id,
        AccountUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<AccountResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<AccountResponse>.Failure(RowVersionRequired());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var account = await dbContext.Accounts
            .FirstOrDefaultAsync(
                entity => entity.CompanyId == companyId && entity.Id == id,
                cancellationToken);
        if (account is null)
        {
            return Result<AccountResponse>.Failure(NotFound(id));
        }

        if (!account.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<AccountResponse>.Failure(Concurrency());
        }

        var normalizedCode = request.Code.Trim();
        if (await CodeExistsAsync(normalizedCode, id, cancellationToken))
        {
            return Result<AccountResponse>.Failure(CodeExists(normalizedCode));
        }

        var parentValidation = await ValidateParentAsync(
            request.ParentAccountId,
            id,
            cancellationToken);
        if (parentValidation.IsFailure)
        {
            return Result<AccountResponse>.Failure(parentValidation.Errors);
        }

        var hasChildren = await dbContext.Accounts.AnyAsync(
            child => child.CompanyId == companyId && child.ParentAccountId == id,
            cancellationToken);
        if (request.IsPosting && hasChildren)
        {
            return Result<AccountResponse>.Failure(PostingAccountHasChildren());
        }

        if (!request.IsActive && await dbContext.Accounts.AnyAsync(
                child =>
                    child.CompanyId == companyId &&
                    child.ParentAccountId == id &&
                    child.IsActive,
                cancellationToken))
        {
            return Result<AccountResponse>.Failure(InactiveAccountHasChildren());
        }

        var hasMappings = await dbContext.AccountStatementMappings
            .IgnoreQueryFilters()
            .AnyAsync(
            mapping => mapping.CompanyId == companyId && mapping.AccountId == id,
            cancellationToken);
        var effectiveAccountType = parentValidation.Value?.AccountType
            ?? request.AccountType;
        if (hasMappings &&
            (account.AccountType != effectiveAccountType ||
             !request.IsPosting ||
             !request.IsActive))
        {
            return Result<AccountResponse>.Failure(
                MappedAccountCannotChangeClassification());
        }

        var entry = dbContext.Entry(account);
        entry.Property(entity => entity.RowVersion).OriginalValue = request.RowVersion;
        request.Adapt(account);
        ApplyInheritedClassification(account, parentValidation.Value);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<AccountResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .FirstAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<AccountResponse>.Success(response);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        var account = await dbContext.Accounts.FirstOrDefaultAsync(
            entity => entity.CompanyId == companyId && entity.Id == id,
            cancellationToken);
        if (account is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (await dbContext.Accounts.AnyAsync(
                child => child.CompanyId == companyId && child.ParentAccountId == id,
                cancellationToken))
        {
            return Result.Failure(HasChildren());
        }

        if (await dbContext.AccountStatementMappings
                .IgnoreQueryFilters()
                .AnyAsync(
                    mapping =>
                        mapping.CompanyId == companyId &&
                        mapping.AccountId == id,
                    cancellationToken))
        {
            return Result.Failure(HasStatementMappings());
        }

        dbContext.Accounts.Remove(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private IQueryable<AccountResponse> ProjectResponseQuery(int id) =>
        dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.CompanyId == companyId && account.Id == id)
            .ProjectToType<AccountResponse>();

    private Task<bool> CodeExistsAsync(
        string code,
        int? excludedId,
        CancellationToken cancellationToken) =>
        dbContext.Accounts.AsNoTracking().AnyAsync(
            account =>
                account.CompanyId == companyId &&
                (!excludedId.HasValue || account.Id != excludedId.Value) &&
                account.Code.ToUpper() == code.ToUpper(),
            cancellationToken);

    private async Task<Result<ParentClassification?>> ValidateParentAsync(
        int? parentAccountId,
        int? accountId,
        CancellationToken cancellationToken)
    {
        if (!parentAccountId.HasValue)
        {
            return Result<ParentClassification?>.Success(null);
        }

        if (accountId == parentAccountId)
        {
            return Result<ParentClassification?>.Failure(ParentCannotBeSelf());
        }

        var parent = await dbContext.Accounts
            .AsNoTracking()
            .Where(account =>
                account.CompanyId == companyId &&
                account.Id == parentAccountId.Value)
            .Select(account => new
            {
                account.Id,
                account.ParentAccountId,
                account.AccountType,
                account.NormalBalance,
                account.IsPosting,
                account.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (parent is null)
        {
            return Result<ParentClassification?>.Failure(
                ParentNotFound(parentAccountId.Value));
        }

        if (!parent.IsActive)
        {
            return Result<ParentClassification?>.Failure(ParentInactive());
        }

        if (parent.IsPosting)
        {
            return Result<ParentClassification?>.Failure(ParentMustBeGroup());
        }

        if (accountId.HasValue && await CreatesCycleAsync(
                accountId.Value,
                parent.Id,
                cancellationToken))
        {
            return Result<ParentClassification?>.Failure(HierarchyCycle());
        }

        return Result<ParentClassification?>.Success(new ParentClassification(
            parent.AccountType,
            parent.NormalBalance));
    }

    private static void ApplyInheritedClassification(
        Account account,
        ParentClassification? parent)
    {
        if (parent is null)
        {
            return;
        }

        account.AccountType = parent.AccountType;
        account.NormalBalance = parent.NormalBalance;
    }

    private async Task<bool> CreatesCycleAsync(
        int accountId,
        int parentAccountId,
        CancellationToken cancellationToken)
    {
        var parents = await dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.CompanyId == companyId)
            .ToDictionaryAsync(
                account => account.Id,
                account => account.ParentAccountId,
                cancellationToken);

        int? currentId = parentAccountId;
        var visited = new HashSet<int>();
        while (currentId.HasValue && visited.Add(currentId.Value))
        {
            if (currentId.Value == accountId)
            {
                return true;
            }

            currentId = parents.GetValueOrDefault(currentId.Value);
        }

        return false;
    }

    private static IReadOnlyList<AccountTreeResponse> BuildTree(
        int? parentAccountId,
        ILookup<int?, AccountTreeRow> childrenByParent) =>
        childrenByParent[parentAccountId]
            .Select(row => new AccountTreeResponse(
                Id: row.Id,
                Code: row.Code,
                Name: row.Name,
                ParentAccountId: row.ParentAccountId,
                AccountType: row.AccountType,
                NormalBalance: row.NormalBalance,
                IsPosting: row.IsPosting,
                IsActive: row.IsActive,
                RowVersion: row.RowVersion,
                Children: BuildTree(row.Id, childrenByParent)))
            .ToList();

    private sealed class AccountTreeRow
    {
        public int Id { get; init; }

        public string Code { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public int? ParentAccountId { get; init; }

        public AccountType AccountType { get; init; }

        public NormalBalance NormalBalance { get; init; }

        public bool IsPosting { get; init; }

        public bool IsActive { get; init; }

        public byte[] RowVersion { get; init; } = [];
    }

    private sealed record ParentClassification(
        AccountType AccountType,
        NormalBalance NormalBalance);
}
