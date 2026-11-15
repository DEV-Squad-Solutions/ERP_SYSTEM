using System.Data;
using static MiniErp.Application.Features.Cashboxes.CashboxErrors;
using ExchangeRateErrors = MiniErp.Application.Features.ExchangeRates.ExchangeRateErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Cashboxes;
using MiniErp.Application.Features.Companies;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.ExchangeRates;

namespace MiniErp.Infrastructure.Services.Cashboxes;

public sealed class CashboxService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IExchangeRateResolver exchangeRateResolver,
    TimeProvider timeProvider,
    IOpeningBalancePostingService? openingBalancePostingService = null,
    IExchangeRatePostingSynchronizer? exchangeRatePostingSynchronizer = null,
    IDefaultAccountingSetupService? defaultAccountingSetupService = null,
    ICashboxAccessService? cashboxAccessService = null)
    : ICashboxService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<CashboxResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CashboxFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new CashboxFilterRequest();
        var access = await CashboxPermissionGuard.ScopeAsync(cashboxAccessService, cancellationToken);
        if (access.IsFailure) return Result<PagedResponse<CashboxResponse>>.Failure(access.Errors);
        var scope = access.Value;
        var search = filters.Search?.Trim();
        var code = filters.Code?.Trim();
        var name = filters.Name?.Trim();

        var query = dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox => cashbox.CompanyId == companyId)
            .Where(cashbox => scope.HasAllCashboxes || scope.ViewCashboxIds.Contains(cashbox.Id))
            .Where(cashbox =>
                string.IsNullOrEmpty(search) ||
                cashbox.Code.Contains(search) ||
                cashbox.Name.Contains(search) ||
                (cashbox.Notes != null &&
                 cashbox.Notes.Contains(search)))
            .Where(cashbox =>
                string.IsNullOrEmpty(code) ||
                cashbox.Code.Contains(code))
            .Where(cashbox =>
                string.IsNullOrEmpty(name) ||
                cashbox.Name.Contains(name))
            .Where(cashbox =>
                !filters.Currency.HasValue ||
                cashbox.Currency == filters.Currency.Value)
            .Where(cashbox =>
                !filters.IsActive.HasValue ||
                cashbox.IsActive == filters.IsActive.Value)
            .OrderByDescending(cashbox => cashbox.CreatedOn)
            .ThenByDescending(cashbox => cashbox.Id);

        var pageResult = await paginationService.PaginateAsync<
            Cashbox,
            CashboxResponse>(
            query,
            pagination,
            cancellationToken);
        if (pageResult.IsFailure)
        {
            return pageResult;
        }

        var balances = await LoadCurrentFiscalYearBalancesAsync(
            pageResult.Value.Items.Select(cashbox => cashbox.Id),
            cancellationToken);
        var items = pageResult.Value.Items
            .Select(cashbox => cashbox with
            {
                CurrentBalance = balances.GetValueOrDefault(cashbox.Id)
            })
            .ToList();
        return Result<PagedResponse<CashboxResponse>>.Success(
            pageResult.Value with { Items = items });
    }

    public async Task<Result<IReadOnlyList<CashboxSelectResponse>>>
        GetSelectAsync(CancellationToken cancellationToken = default)
    {
        var access = await CashboxPermissionGuard.ScopeAsync(cashboxAccessService, cancellationToken);
        if (access.IsFailure) return Result<IReadOnlyList<CashboxSelectResponse>>.Failure(access.Errors);
        var scope = access.Value;
        var response = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox => scope.HasAllCashboxes || scope.ViewCashboxIds.Contains(cashbox.Id))
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                cashbox.IsActive)
            .OrderBy(cashbox => cashbox.Name)
            .ThenBy(cashbox => cashbox.Id)
            .ProjectToType<CashboxSelectResponse>()
            .ToListAsync(cancellationToken);

        var balances = await LoadCurrentFiscalYearBalancesAsync(
            response.Select(cashbox => cashbox.Id),
            cancellationToken);
        response = response
            .Select(cashbox => cashbox with
            {
                CurrentBalance = balances.GetValueOrDefault(cashbox.Id)
            })
            .ToList();

        return Result<IReadOnlyList<CashboxSelectResponse>>.Success(response);
    }

    public async Task<Result<CashboxResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashboxResponse>.Failure(InvalidId());
        }

        var access = await CashboxPermissionGuard.ViewAsync(cashboxAccessService, id, cancellationToken);
        if (access.IsFailure) return Result<CashboxResponse>.Failure(access.Errors);
        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (response is null)
        {
            return Result<CashboxResponse>.Failure(NotFound(id));
        }

        response = await ApplyCurrentFiscalYearBalanceAsync(
            response,
            cancellationToken);
        return Result<CashboxResponse>.Success(response);
    }

    public async Task<Result<CashboxResponse>> AddAsync(
        CashboxRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = await CashboxPermissionGuard.ScopeAsync(cashboxAccessService, cancellationToken);
        if (access.IsFailure) return Result<CashboxResponse>.Failure(access.Errors);
        if (!access.Value.HasAllCashboxes) return Result<CashboxResponse>.Failure(CashboxPermissionGuard.Forbidden());
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var cashbox = request.Adapt<Cashbox>();
        cashbox.CompanyId = companyId;
        cashbox.Code = await EntityIdentifierGenerator.GenerateUniqueAsync(
            dbContext,
            prefix: "CBX",
            companyId: companyId,
            existingIdentifiers: dbContext.Cashboxes
                .IgnoreQueryFilters()
                .Where(entity => entity.CompanyId == companyId)
                .Select(entity => entity.Code),
            cancellationToken);

        var openingDate = request.OpeningBalanceDate ??
            DateOnly.FromDateTime(
                timeProvider.GetUtcNow().UtcDateTime);
        var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
            cashbox.Currency,
            openingDate,
            request.OpeningExchangeRate,
            cancellationToken);
        if (exchangeRateResult.IsFailure)
        {
            return Result<CashboxResponse>.Failure(
                exchangeRateResult.Error);
        }
        cashbox.ApplyOpeningExchangeRate(
            openingDate,
            exchangeRateResult.Value.ExchangeRateId,
            exchangeRateResult.Value.Rate);

        var duplicateErrors = await FindDuplicateAsync(
            cashbox,
            excludedId: null,
            cancellationToken);
        if (duplicateErrors.Count > 0)
        {
            return Result<CashboxResponse>.Failure(duplicateErrors);
        }

        dbContext.Cashboxes.Add(cashbox);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (defaultAccountingSetupService is not null)
        {
            await defaultAccountingSetupService.EnsureCashboxAsync(
                companyId,
                cashbox.Id,
                cancellationToken);
        }

        if (openingBalancePostingService is not null)
        {
            var postingResult = await openingBalancePostingService
                .SynchronizeCashboxAsync(cashbox.Id, cancellationToken);
            if (postingResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<CashboxResponse>.Failure(postingResult.Errors);
            }
        }

        var response = await ProjectResponseQuery(cashbox.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        response = await ApplyCurrentFiscalYearBalanceAsync(
            response,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<CashboxResponse>.Success(response);
    }

    public async Task<Result<CashboxResponse>> UpdateAsync(
        int id,
        CashboxUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashboxResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<CashboxResponse>.Failure(RowVersionRequired());
        }

        var access = await CashboxPermissionGuard.ExecuteAsync(cashboxAccessService, id, cancellationToken);
        if (access.IsFailure) return Result<CashboxResponse>.Failure(access.Errors);
        if (request.UpdateLinkedTransactions)
        {
            var scope = await CashboxPermissionGuard.ScopeAsync(cashboxAccessService, cancellationToken);
            if (scope.IsFailure) return Result<CashboxResponse>.Failure(scope.Errors);
            if (!scope.Value.HasAllCashboxes) return Result<CashboxResponse>.Failure(CashboxPermissionGuard.Forbidden());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var cashbox = await dbContext.Cashboxes.FirstOrDefaultAsync(
            entity =>
                entity.Id == id &&
                entity.CompanyId == companyId,
            cancellationToken);
        if (cashbox is null)
        {
            return Result<CashboxResponse>.Failure(NotFound(id));
        }

        var entry = dbContext.Entry(cashbox);
        await entry.ReloadAsync(cancellationToken);
        if (entry.State == EntityState.Detached || cashbox.CompanyId != companyId || cashbox.IsDeleted)
        {
            return Result<CashboxResponse>.Failure(NotFound(id));
        }

        if (!cashbox.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<CashboxResponse>.Failure(Concurrency());
        }

        var deactivating = cashbox.IsActive && !request.IsActive;
        if (deactivating)
        {
            var balanceCheck = await EnsureZeroCurrentBalanceAsync(id, cancellationToken);
            if (balanceCheck.IsFailure)
                return Result<CashboxResponse>.Failure(balanceCheck.Errors);
        }

        var normalized = request.Adapt<Cashbox>();
        var duplicateErrors = await FindDuplicateAsync(
            normalized,
            id,
            cancellationToken);
        if (duplicateErrors.Count > 0)
        {
            return Result<CashboxResponse>.Failure(duplicateErrors);
        }

        var hasVouchers = await HasVouchersAsync(id, cancellationToken);
        if (hasVouchers &&
            (cashbox.OpeningBalance != request.OpeningBalance ||
             cashbox.Currency != request.Currency))
        {
            return Result<CashboxResponse>.Failure(
                OpeningOrCurrencyChangeNotAllowed());
        }

        var openingDate = request.OpeningBalanceDate ??
            cashbox.OpeningBalanceDate;
        var previousOpening = (cashbox.Currency, cashbox.OpeningBalance, cashbox.OpeningBalanceDate,
            cashbox.OpeningExchangeRate);
        int? exchangeRateId;
        decimal exchangeRate;
        var keepsExistingRateReference =
            cashbox.Currency == request.Currency &&
            cashbox.OpeningBalanceDate == openingDate &&
            (!request.OpeningExchangeRate.HasValue ||
             request.OpeningExchangeRate.Value == cashbox.OpeningExchangeRate ||
             (request.UpdateLinkedTransactions &&
              cashbox.OpeningExchangeRateId.HasValue));
        if (keepsExistingRateReference)
        {
            if (request.OpeningExchangeRate is decimal requestedRate &&
                !Domain.Entities.Companies.ExchangeRateRules.IsValidRate(
                    requestedRate))
            {
                return Result<CashboxResponse>.Failure(
                    ExchangeRateErrors.InvalidRate());
            }

            exchangeRateId = cashbox.OpeningExchangeRateId;
            exchangeRate = request.OpeningExchangeRate is decimal rate
                ? Domain.Entities.Companies.ExchangeRateRules.RoundRate(rate)
                : cashbox.OpeningExchangeRate;
        }
        else
        {
            var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
                request.Currency,
                openingDate,
                request.OpeningExchangeRate,
                cancellationToken);
            if (exchangeRateResult.IsFailure)
            {
                return Result<CashboxResponse>.Failure(
                    exchangeRateResult.Error);
            }

            exchangeRateId = exchangeRateResult.Value.ExchangeRateId;
            exchangeRate = exchangeRateResult.Value.Rate;
        }

        if (request.UpdateLinkedTransactions &&
            request.OpeningExchangeRate.HasValue &&
            exchangeRateId is int linkedExchangeRateId)
        {
            var cascadeError = await CascadeExchangeRateChangeAsync(
                linkedExchangeRateId,
                exchangeRate,
                cancellationToken);
            if (cascadeError is not null)
            {
                return Result<CashboxResponse>.Failure(cascadeError);
            }
        }

        entry.Property(entity => entity.RowVersion).OriginalValue =
            request.RowVersion;
        var code = cashbox.Code;
        request.Adapt(cashbox);
        cashbox.Code = code;
        var openingChanged = previousOpening != (cashbox.Currency, cashbox.OpeningBalance, openingDate, exchangeRate);
        if (openingChanged)
        {
            cashbox.ApplyOpeningExchangeRate(openingDate, exchangeRateId, exchangeRate);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            if (deactivating && openingChanged)
            {
                // Full PUT can change opening fields together with activity.
                // Check the resulting native balance before posting anything.
                var balanceCheck = await EnsureZeroCurrentBalanceAsync(id, cancellationToken);
                if (balanceCheck.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return Result<CashboxResponse>.Failure(balanceCheck.Errors);
                }
            }

            // Metadata/status edits must not rewrite an unchanged opening
            // journal after its year closes or its cashbox becomes inactive.
            if (openingChanged && openingBalancePostingService is not null)
            {
                var postingResult = await openingBalancePostingService
                    .SynchronizeCashboxAsync(cashbox.Id, cancellationToken);
                if (postingResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return Result<CashboxResponse>.Failure(
                        postingResult.Errors);
                }
            }

            if (request.UpdateLinkedTransactions &&
                exchangeRateId.HasValue &&
                exchangeRatePostingSynchronizer is not null)
            {
                var postingResult = await exchangeRatePostingSynchronizer
                    .SynchronizeAsync(
                        exchangeRateId.Value,
                        cancellationToken);
                if (postingResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return Result<CashboxResponse>.Failure(
                        postingResult.Errors);
                }
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<CashboxResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        response = await ApplyCurrentFiscalYearBalanceAsync(
            response,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<CashboxResponse>.Success(response);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var access = await CashboxPermissionGuard.ExecuteAsync(cashboxAccessService, id, cancellationToken);
        if (access.IsFailure) return access;
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var cashbox = await dbContext.Cashboxes.FirstOrDefaultAsync(
            entity =>
                entity.Id == id &&
                entity.CompanyId == companyId,
            cancellationToken);
        if (cashbox is null)
        {
            return Result.Failure(NotFound(id));
        }

        var entry = dbContext.Entry(cashbox);
        await entry.ReloadAsync(cancellationToken);
        if (entry.State == EntityState.Detached || cashbox.CompanyId != companyId || cashbox.IsDeleted)
            return Result.Failure(NotFound(id));

        // Deletion depends on remaining movements in every fiscal year.
        // Deleted voucher history still protects currency/opening edits, but
        // no longer makes an otherwise unused cashbox undeletable forever.
        if (await dbContext.CashVouchers.IgnoreQueryFilters().AnyAsync(
                voucher => voucher.CompanyId == companyId &&
                    voucher.CashboxId == id && !voucher.IsDeleted,
                cancellationToken))
        {
            return Result.Failure(HasVouchers());
        }

        if (await HasOtherCurrentMovementsAsync(id, cancellationToken))
            return Result.Failure(HasMovements());

        cashbox.IsActive = false;
        // Set soft-delete directly so EF does not sever required relationships
        // to already-deleted historical children tracked by this context.
        cashbox.IsDeleted = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (openingBalancePostingService is not null)
        {
            var postingResult = await openingBalancePostingService.DeleteAsync(
                JournalEntrySourceType.CashboxOpeningBalance,
                cashbox.Id,
                cancellationToken);
            if (postingResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result.Failure(postingResult.Errors);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private IQueryable<CashboxResponse> ProjectResponseQuery(int id) =>
        dbContext.Cashboxes
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                cashbox.Id == id)
            .ProjectToType<CashboxResponse>();

    private async Task<CashboxResponse> ApplyCurrentFiscalYearBalanceAsync(
        CashboxResponse response,
        CancellationToken cancellationToken)
    {
        var balances = await LoadCurrentFiscalYearBalancesAsync(
            [response.Id],
            cancellationToken);
        return response with
        {
            CurrentBalance = balances.GetValueOrDefault(response.Id)
        };
    }

    private async Task<IReadOnlyDictionary<int, decimal>>
        LoadCurrentFiscalYearBalancesAsync(
            IEnumerable<int> cashboxIds,
            CancellationToken cancellationToken)
    {
        var ids = cashboxIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, decimal>();
        }

        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year =>
                year.CompanyId == companyId &&
                year.IsCurrent)
            .Select(year => new
            {
                year.Id,
                year.StartDate,
                year.EndDate
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (fiscalYear is null)
        {
            // Readers retain the zero display fallback, but a missing year
            // cannot authorize a balance-dependent write.
            return new Dictionary<int, decimal>();
        }

        return await dbContext.Cashboxes
            .AsNoTracking()
            .Where(cashbox =>
                cashbox.CompanyId == companyId &&
                ids.Contains(cashbox.Id))
            .Select(cashbox => new
            {
                cashbox.Id,
                Balance =
                    (cashbox.OpeningBalanceDate >= fiscalYear.StartDate &&
                     cashbox.OpeningBalanceDate <= fiscalYear.EndDate
                        ? cashbox.OpeningBalance
                        : 0m) +
                    (dbContext.JournalEntryLines
                        .Where(line =>
                            line.CompanyId == companyId &&
                            !line.IsDeleted &&
                            line.JournalEntry.CompanyId == companyId &&
                            !line.JournalEntry.IsDeleted &&
                            line.JournalEntry.FiscalYearId == fiscalYear.Id &&
                            line.JournalEntry.EntryType ==
                                JournalEntryType.Opening &&
                            line.JournalEntry.SourceType ==
                                JournalEntrySourceType.FiscalYearClosing &&
                            line.JournalEntry.Status ==
                                JournalEntryStatus.Posted &&
                            line.JournalEntry.ReversalOfEntryId == null &&
                            line.JournalEntry.ReversedOn == null &&
                            line.PartyType == JournalPartyType.Cashbox &&
                            line.PartyId == cashbox.Id &&
                            // Carried base-currency revaluation deltas do not
                            // represent additional cashbox currency units.
                            line.Currency == cashbox.Currency)
                        .Select(line => (decimal?)
                            (line.TransactionDebit -
                             line.TransactionCredit))
                        .Sum() ?? 0m) +
                    (cashbox.Vouchers
                        .Where(voucher =>
                            voucher.CompanyId == companyId &&
                            voucher.FiscalYearId == fiscalYear.Id &&
                            voucher.IsPosted)
                        .Select(voucher => (decimal?)
                            (voucher.Direction == CashDirection.Receipt
                                ? voucher.Amount
                                : -voucher.Amount))
                        .Sum() ?? 0m) +
                    (dbContext.JournalEntryLines
                        .Where(line =>
                            line.CompanyId == companyId && !line.IsDeleted &&
                            line.JournalEntry.CompanyId == companyId && !line.JournalEntry.IsDeleted &&
                            line.JournalEntry.FiscalYearId == fiscalYear.Id &&
                            line.JournalEntry.Status == JournalEntryStatus.Posted &&
                            line.JournalEntry.ReversalOfEntryId == null && line.JournalEntry.ReversedOn == null &&
                            line.JournalEntry.SourceType == null &&
                            (line.JournalEntry.EntryType == JournalEntryType.Manual ||
                             line.JournalEntry.EntryType == JournalEntryType.Adjustment ||
                             line.JournalEntry.EntryType == JournalEntryType.Opening) &&
                            line.PartyType == JournalPartyType.Cashbox && line.PartyId == cashbox.Id &&
                            line.Currency == cashbox.Currency)
                        .Select(line => (decimal?)(line.TransactionDebit - line.TransactionCredit))
                        .Sum() ?? 0m)
            })
            .ToDictionaryAsync(
                row => row.Id,
                row => row.Balance,
                cancellationToken);
    }

    private async Task<Result> EnsureZeroCurrentBalanceAsync(int cashboxId, CancellationToken cancellationToken)
    {
        var balances = await LoadCurrentFiscalYearBalancesAsync([cashboxId], cancellationToken);
        if (!balances.TryGetValue(cashboxId, out var balance))
            return Result.Failure(Application.Features.FiscalYears.FiscalYearErrors.CurrentNotFound());
        return balance == 0m ? Result.Success() : Result.Failure(NonZeroBalance());
    }

    private async Task<IReadOnlyList<Error>> FindDuplicateAsync(
        Cashbox cashbox,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var normalizedName = cashbox.Name.ToUpperInvariant();

        var duplicate = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                (!excludedId.HasValue || entity.Id != excludedId.Value) &&
                entity.Name.ToUpper() == normalizedName)
            .Select(entity => entity.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (duplicate is null)
        {
            return Array.Empty<Error>();
        }

        return [NameExists(cashbox.Name)];
    }

    private Task<Error?> CascadeExchangeRateChangeAsync(
        int exchangeRateId,
        decimal exchangeRate,
        CancellationToken cancellationToken) =>
        ExchangeRateCascadeUpdater.UpdateAsync(
            dbContext,
            companyId,
            timeProvider,
            exchangeRateId,
            exchangeRate,
            InvalidLinkedTransfer(),
            cancellationToken);

    private async Task<bool> HasVouchersAsync(
        int cashboxId,
        CancellationToken cancellationToken) =>
        await dbContext.CashVouchers
            .IgnoreQueryFilters()
            .AnyAsync(
                voucher =>
                    voucher.CompanyId == companyId &&
                    voucher.CashboxId == cashboxId,
                cancellationToken);

    private async Task<bool> HasOtherCurrentMovementsAsync(int cashboxId, CancellationToken cancellationToken) =>
        await dbContext.CashboxTransfers.IgnoreQueryFilters().AnyAsync(
            transfer => transfer.CompanyId == companyId && !transfer.IsDeleted &&
                (transfer.SourceCashboxId == cashboxId || transfer.DestinationCashboxId == cashboxId),
            cancellationToken) ||
        await dbContext.JournalEntryLines.IgnoreQueryFilters().AnyAsync(
            line => line.CompanyId == companyId && !line.IsDeleted &&
                line.JournalEntry.CompanyId == companyId && !line.JournalEntry.IsDeleted &&
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.ReversalOfEntryId == null && line.JournalEntry.ReversedOn == null &&
                line.PartyType == JournalPartyType.Cashbox && line.PartyId == cashboxId &&
                // The cashbox's own opening is removed by the existing posting
                // service in this transaction. Other live entries must remain.
                !(line.JournalEntry.SourceType == JournalEntrySourceType.CashboxOpeningBalance &&
                  line.JournalEntry.SourceId == cashboxId),
            cancellationToken);

}
