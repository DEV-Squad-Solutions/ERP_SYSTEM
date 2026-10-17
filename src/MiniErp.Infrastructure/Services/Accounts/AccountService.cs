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
using MiniErp.Infrastructure.Services;
using static MiniErp.Application.Features.Accounts.AccountErrors;

namespace MiniErp.Infrastructure.Services.Accounts;

public sealed class AccountService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    ICashboxAccessService? cashboxAccessService = null)
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

    public async Task<Result<IReadOnlyList<ExpenseAccountSelectResponse>>>
        GetExpenseSelectAsync(CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Accounts
            .AsNoTracking()
            .Where(account =>
                account.CompanyId == companyId &&
                account.IsActive &&
                account.AccountType == AccountType.Expense)
            .OrderBy(account => account.Code)
            .ThenBy(account => account.Id)
            .Select(account => new
            {
                account.Id,
                account.Code,
                account.Name,
                account.ParentAccountId,
                account.IsPosting
            })
            .ToListAsync(cancellationToken);

        IReadOnlyList<ExpenseAccountSelectResponse> response = rows
            .Select(row => new ExpenseAccountSelectResponse(
                Id: row.Id,
                Code: row.Code,
                Name: row.Name,
                ParentAccountId: row.ParentAccountId,
                IsPosting: row.IsPosting))
            .ToArray();

        return Result<IReadOnlyList<ExpenseAccountSelectResponse>>.Success(
            response);
    }

    public async Task<Result<IReadOnlyList<JournalAccountSelectResponse>>> GetJournalSelectAsync(
        int fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        var scope = cashboxAccessService is null ? null
            : await cashboxAccessService.GetScopeAsync(cancellationToken);
        if (scope?.IsFailure == true)
            return Result<IReadOnlyList<JournalAccountSelectResponse>>.Failure(scope.Errors);
        var allCashboxes = scope is null || scope.Value.HasAllCashboxes;
        var visibleCashboxIds = scope?.Value.ViewCashboxIds ?? [];
        if (fiscalYearId <= 0)
        {
            return Result<IReadOnlyList<JournalAccountSelectResponse>>.Failure(
                InvalidId());
        }

        var fiscalYearExists = await dbContext.FiscalYears
            .AsNoTracking()
            .AnyAsync(
                year =>
                    year.CompanyId == companyId &&
                    year.Id == fiscalYearId,
                cancellationToken);
        if (!fiscalYearExists)
        {
            return Result<IReadOnlyList<JournalAccountSelectResponse>>.Failure(
                FiscalYearNotFound(fiscalYearId));
        }

        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(account =>
                account.CompanyId == companyId &&
                account.IsActive &&
                account.IsPosting &&
                account.ParentAccountId.HasValue)
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

        var accountIds = accounts.Select(account => account.Id).ToArray();
        var mappings = await dbContext.AccountMappings
            .AsNoTracking()
            .Where(mapping =>
                mapping.CompanyId == companyId &&
                mapping.FiscalYearId == fiscalYearId &&
                accountIds.Contains(mapping.AccountId) &&
                (mapping.MappingType == AccountingMappingType.CustomerControl ||
                 mapping.MappingType == AccountingMappingType.SupplierControl ||
                 mapping.MappingType == AccountingMappingType.EmployeeControl ||
                 mapping.MappingType == AccountingMappingType.EmployeeReceivable ||
                 mapping.MappingType == AccountingMappingType.DriverControl ||
                 mapping.MappingType == AccountingMappingType.Cashbox))
            .Select(mapping => new
            {
                mapping.AccountId,
                mapping.MappingType,
                mapping.SourceId
            })
            .ToListAsync(cancellationToken);

        var partyTypesByAccount = mappings
            .Select(mapping => new
            {
                mapping.AccountId,
                PartyType = ToJournalPartyType(mapping.MappingType),
                mapping.SourceId
            })
            .Where(mapping => mapping.PartyType != JournalPartyType.Cashbox ||
                              mapping.SourceId.HasValue)
            .Distinct()
            .ToLookup(mapping => mapping.AccountId, mapping => mapping.PartyType);
        var requiredPartyTypes = partyTypesByAccount
            .SelectMany(group => group)
            .ToHashSet();

        IReadOnlyList<JournalPartySelectResponse> businessPartners = [];
        if (requiredPartyTypes.Contains(JournalPartyType.Customer) ||
            requiredPartyTypes.Contains(JournalPartyType.Supplier))
        {
            var rows = await dbContext.BusinessPartners
                .AsNoTracking()
                .Where(partner =>
                    partner.CompanyId == companyId &&
                    partner.IsActive)
                .OrderBy(partner => partner.Name)
                .ThenBy(partner => partner.Id)
                .Select(partner => new
                {
                    partner.Id,
                    partner.Code,
                    partner.Name,
                    partner.Currency
                })
                .ToListAsync(cancellationToken);
            businessPartners = rows.Select(party =>
                new JournalPartySelectResponse(
                    Id: party.Id,
                    Code: party.Code,
                    Name: party.Name)
                {
                    Currency = party.Currency
                }).ToArray();
        }

        IReadOnlyList<JournalPartySelectResponse> employees = [];
        if (requiredPartyTypes.Contains(JournalPartyType.Employee))
        {
            var rows = await dbContext.Employees
                .AsNoTracking()
                .Where(employee =>
                    employee.CompanyId == companyId &&
                    employee.IsActive)
                .OrderBy(employee => employee.Name)
                .ThenBy(employee => employee.Id)
                .Select(employee => new
                {
                    employee.Id,
                    employee.Code,
                    employee.Name
                })
                .ToListAsync(cancellationToken);
            employees = rows.Select(party =>
                new JournalPartySelectResponse(
                    Id: party.Id,
                    Code: party.Code,
                    Name: party.Name)).ToArray();
        }

        IReadOnlyList<JournalPartySelectResponse> drivers = [];
        if (requiredPartyTypes.Contains(JournalPartyType.Driver))
        {
            var rows = await dbContext.Drivers
                .AsNoTracking()
                .Where(driver =>
                    driver.CompanyId == companyId &&
                    driver.IsActive)
                .OrderBy(driver => driver.Name)
                .ThenBy(driver => driver.Id)
                .Select(driver => new
                {
                    driver.Id,
                    driver.Code,
                    driver.Name
                })
                .ToListAsync(cancellationToken);
            drivers = rows.Select(party =>
                new JournalPartySelectResponse(
                    Id: party.Id,
                    Code: party.Code,
                    Name: party.Name)).ToArray();
        }

        var cashboxIds = mappings
            .Where(mapping =>
                mapping.MappingType == AccountingMappingType.Cashbox &&
                mapping.SourceId.HasValue)
            .Select(mapping => mapping.SourceId!.Value)
            .Distinct()
            .ToArray();
        var cashboxes = cashboxIds.Length == 0
            ? new Dictionary<int, JournalPartySelectResponse>()
            : await dbContext.Cashboxes
                .AsNoTracking()
                .Where(cashbox =>
                    cashbox.CompanyId == companyId &&
                    cashbox.IsActive &&
                    (allCashboxes || visibleCashboxIds.Contains(cashbox.Id)) &&
                    cashboxIds.Contains(cashbox.Id))
                .OrderBy(cashbox => cashbox.Name)
                .ThenBy(cashbox => cashbox.Id)
                .Select(cashbox => new
                {
                    cashbox.Id,
                    cashbox.Code,
                    cashbox.Name,
                    cashbox.Currency
                })
                .ToDictionaryAsync(
                    cashbox => cashbox.Id,
                    cashbox => new JournalPartySelectResponse(
                        Id: cashbox.Id,
                        Code: cashbox.Code,
                        Name: cashbox.Name)
                    {
                        Currency = cashbox.Currency
                    },
                    cancellationToken);

        IReadOnlyList<JournalAccountSelectResponse> response = accounts
            .Select(account => new JournalAccountSelectResponse(
                Id: account.Id,
                Code: account.Code,
                Name: account.Name,
                AccountType: account.AccountType,
                PartyGroups: partyTypesByAccount[account.Id]
                    .Distinct()
                    .OrderBy(partyType => partyType)
                    .Select(partyType => new JournalPartyGroupResponse(
                        PartyType: partyType,
                        Parties: partyType switch
                        {
                            JournalPartyType.Customer or
                            JournalPartyType.Supplier => businessPartners,
                            JournalPartyType.Employee => employees,
                            JournalPartyType.Driver => drivers,
                            JournalPartyType.Cashbox => mappings
                                .Where(mapping =>
                                    mapping.AccountId == account.Id &&
                                    mapping.MappingType == AccountingMappingType.Cashbox &&
                                    mapping.SourceId.HasValue &&
                                    cashboxes.ContainsKey(mapping.SourceId.Value))
                                .Select(mapping => cashboxes[mapping.SourceId!.Value])
                                .DistinctBy(cashbox => cashbox.Id)
                                .OrderBy(cashbox => cashbox.Name)
                                .ThenBy(cashbox => cashbox.Id)
                                .ToArray(),
                            _ => []
                        }))
                    .ToArray()))
            .ToArray();

        return Result<IReadOnlyList<JournalAccountSelectResponse>>.Success(
            response);
    }

    private static JournalPartyType ToJournalPartyType(
        AccountingMappingType mappingType) => mappingType switch
        {
            AccountingMappingType.CustomerControl => JournalPartyType.Customer,
            AccountingMappingType.SupplierControl => JournalPartyType.Supplier,
            AccountingMappingType.EmployeeControl or
            AccountingMappingType.EmployeeReceivable => JournalPartyType.Employee,
            AccountingMappingType.DriverControl => JournalPartyType.Driver,
            AccountingMappingType.Cashbox => JournalPartyType.Cashbox,
            _ => throw new ArgumentOutOfRangeException(nameof(mappingType))
        };

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

        if (dbContext.Database.IsSqlServer())
        {
            // Serialize automatic allocations for this company before reading parents or codes.
            // The transaction owns this lock until the account has been saved and committed.
            _ = await dbContext.Database.SqlQuery<int>($"""
                SELECT [Id] AS [Value]
                FROM [Companies] WITH (UPDLOCK, HOLDLOCK)
                WHERE [Id] = {companyId}
                """).SingleAsync(cancellationToken);
        }

        var parentValidation = await ValidateParentAsync(
            request.ParentAccountId,
            accountId: null,
            cancellationToken);
        if (parentValidation.IsFailure)
        {
            return Result<AccountResponse>.Failure(parentValidation.Errors);
        }

        var generatedCode = await GenerateAccountCodeAsync(
            request.ParentAccountId,
            cancellationToken);
        if (generatedCode is null)
        {
            return Result<AccountResponse>.Failure(AutomaticCodeUnavailable());
        }

        var account = (request with { Code = generatedCode }).Adapt<Account>();
        account.CompanyId = companyId;
        ApplyInheritedClassification(account, parentValidation.Value);
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ProjectResponseQuery(account.Id)
            .FirstAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<AccountResponse>.Success(response);
    }

    private async Task<string?> GenerateAccountCodeAsync(
        int? parentAccountId,
        CancellationToken cancellationToken)
    {
        // Soft-deleted accounts intentionally do not reserve their former code.
        var accounts = dbContext.Accounts
            .Where(account => account.CompanyId == companyId);
        var parentCode = parentAccountId.HasValue
            ? await accounts.Where(account => account.Id == parentAccountId.Value)
                .Select(account => account.Code).FirstOrDefaultAsync(cancellationToken)
            : null;
        if (parentAccountId.HasValue &&
            (string.IsNullOrEmpty(parentCode) ||
             parentCode.Length > 48 ||
             parentCode.Any(character => character is < '0' or > '9')))
            return null;
        var existingAccounts = await accounts
            .Select(account => new { account.Code, account.ParentAccountId })
            .ToListAsync(cancellationToken);
        var used = existingAccounts.Select(account => account.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Keep established four-digit branches readable without changing their hierarchy.
        // Continue after the highest direct child, rather than filling earlier gaps.
        if (parentCode is { Length: 4 } && parentCode[0] != '0' && parentCode[^1] == '0')
        {
            var parentNumber = int.Parse(parentCode, System.Globalization.CultureInfo.InvariantCulture);
            var branchSize = 1;
            for (var index = parentCode.Length - 1; index >= 0 && parentCode[index] == '0'; index--)
                branchSize *= 10;
            var lastCode = parentNumber + branchSize - 1;
            var nextCode = parentNumber + 1;
            foreach (var existing in existingAccounts)
            {
                if (existing.ParentAccountId == parentAccountId &&
                    existing.Code.Length == 4 &&
                    existing.Code.All(character => character is >= '0' and <= '9'))
                {
                    var number = int.Parse(existing.Code, System.Globalization.CultureInfo.InvariantCulture);
                    if (number > parentNumber && number <= lastCode)
                        nextCode = Math.Max(nextCode, number + 1);
                }
            }

            for (var number = nextCode; number <= lastCode; number++)
            {
                var candidate = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!used.Contains(candidate))
                    return candidate;
            }

            return null;
        }

        // Preserve suffix generation for other existing numeric parent formats.
        // Hierarchy always comes from ParentAccountId, never from the generated code.
        // At most used.Count candidates can be occupied, even by accounts under other parents.
        for (long sequence = 1; sequence <= (long)used.Count + 1; sequence++)
        {
            var candidate = parentAccountId.HasValue
                ? parentCode + sequence.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)
                : (sequence * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (candidate.Length > 50)
                return null;
            if (!used.Contains(candidate))
                return candidate;
        }

        return null;
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

        var effectiveAccountType = parentValidation.Value?.AccountType
            ?? request.AccountType;
        var effectiveNormalBalance = parentValidation.Value?.NormalBalance
            ?? request.NormalBalance;

        var hasChildren = await dbContext.Accounts.AnyAsync(
            child => child.CompanyId == companyId && child.ParentAccountId == id,
            cancellationToken);
        if (request.IsPosting && hasChildren)
        {
            return Result<AccountResponse>.Failure(PostingAccountHasChildren());
        }

        if (hasChildren &&
            (account.AccountType != effectiveAccountType ||
             account.NormalBalance != effectiveNormalBalance))
        {
            return Result<AccountResponse>.Failure(
                AccountWithChildrenCannotChangeClassification());
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
        if (hasMappings &&
            (account.AccountType != effectiveAccountType ||
             account.NormalBalance != effectiveNormalBalance ||
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

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

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

        if (await dbContext.CashVouchers
                .IgnoreQueryFilters()
                .AnyAsync(
                    voucher =>
                        voucher.CompanyId == companyId &&
                        voucher.AccountId == id,
                    cancellationToken))
        {
            return Result.Failure(HasCashVouchers());
        }

        if (await dbContext.JournalEntryLines
                .IgnoreQueryFilters()
                .AnyAsync(
                    line =>
                        line.CompanyId == companyId &&
                        line.AccountId == id,
                    cancellationToken))
        {
            return Result.Failure(HasJournalEntries());
        }

        if (await dbContext.MonetaryAccountRevaluations
                .IgnoreQueryFilters()
                .AnyAsync(
                    revaluation =>
                        revaluation.CompanyId == companyId &&
                        revaluation.AccountId == id,
                    cancellationToken))
        {
            return Result.Failure(HasRevaluations());
        }

        if (await dbContext.AccountMappings
                .IgnoreQueryFilters()
                .AnyAsync(
                    mapping =>
                        mapping.CompanyId == companyId &&
                        mapping.AccountId == id,
                    cancellationToken))
        {
            return Result.Failure(HasOperationalMappings());
        }

        // Classification alone is not financial use. Remove live links atomically with
        // the unused account; already soft-deleted links keep their original audit history.
        var statementMappings = await dbContext.AccountStatementMappings
            .Where(mapping => mapping.CompanyId == companyId && mapping.AccountId == id)
            .ToListAsync(cancellationToken);
        dbContext.AccountStatementMappings.RemoveRange(statementMappings);
        dbContext.Accounts.Remove(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

        var parentHasCashVouchers = await dbContext.CashVouchers
            .AsNoTracking()
            .AnyAsync(
                voucher =>
                    voucher.CompanyId == companyId &&
                    voucher.AccountId == parent.Id,
                cancellationToken);
        var parentHasJournalEntries = await dbContext.JournalEntryLines
            .AsNoTracking()
            .AnyAsync(
                line =>
                    line.CompanyId == companyId &&
                    line.AccountId == parent.Id,
                cancellationToken);
        if (parentHasCashVouchers || parentHasJournalEntries)
        {
            return Result<ParentClassification?>.Failure(ParentHasMovements());
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
