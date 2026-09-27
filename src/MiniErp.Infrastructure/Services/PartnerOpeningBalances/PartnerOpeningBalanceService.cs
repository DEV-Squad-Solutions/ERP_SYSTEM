using System.Data;
using static MiniErp.Application.Features.PartnerOpeningBalances.PartnerOpeningBalanceErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.PartnerOpeningBalances;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.PartnerOpeningBalances;

public sealed class PartnerOpeningBalanceService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IFiscalYearQueryScopeResolver fiscalYearQueryScopeResolver,
    IExchangeRateResolver exchangeRateResolver,
    IFiscalYearPeriodGuard? fiscalYearPeriodGuard = null,
    IOpeningBalancePostingService? openingBalancePostingService = null)
    : IPartnerOpeningBalanceService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<PartnerOpeningBalanceResponse>>> GetAllAsync(
        PaginationRequest pagination,
        PartnerOpeningBalanceFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new PartnerOpeningBalanceFilterRequest();
        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: filters.FiscalYearId,
            fromDate: filters.FromDate,
            toDate: filters.ToDate,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<PagedResponse<PartnerOpeningBalanceResponse>>.Failure(
                fiscalYear.Errors);
        }

        if (pagination.PageNumber <= 0 ||
            pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize)
        {
            return Result<PagedResponse<PartnerOpeningBalanceResponse>>.Failure(
                PaginationErrors.Invalid());
        }

        var baseCurrency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;

        var persistedRows = dbContext.PartnerOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.FiscalYearId == fiscalYear.Value.FiscalYearId)
            .Select(balance => new PartnerOpeningBalanceListRow
            {
                Id = balance.Id,
                CompanyId = balance.CompanyId,
                FiscalYearId = balance.FiscalYearId,
                FiscalYearName = balance.FiscalYear.Name,
                BusinessPartnerId = balance.BusinessPartnerId,
                BusinessPartnerName = balance.BusinessPartner.Name,
                DocumentNumber = balance.DocumentNumber,
                DocumentDate = balance.DocumentDate,
                Currency = balance.Currency,
                BaseCurrency = baseCurrency,
                ExchangeRate = balance.ExchangeRate,
                BalanceType = balance.BalanceType,
                Amount = balance.Amount,
                BaseAmount = balance.BaseAmount,
                Notes = balance.Notes,
                RowVersion = balance.RowVersion,
                IsCarriedForward = false
            });

        var carriedRows = CreateCarriedForwardRows(
            fiscalYearId: fiscalYear.Value.FiscalYearId,
            fiscalYearName: fiscalYear.Value.FiscalYearName,
            baseCurrency: baseCurrency);

        var persisted = await ApplyFilters(persistedRows, filters)
            .ToListAsync(cancellationToken);
        var carriedForward = await ApplyFilters(carriedRows, filters)
            .ToListAsync(cancellationToken);
        var query = persisted
            .Concat(carriedForward)
            .OrderByDescending(balance => balance.DocumentDate)
            .ThenByDescending(balance => balance.IsCarriedForward)
            .ThenByDescending(balance => balance.Id);

        var totalCount = persisted.Count + carriedForward.Count;
        var offset = (long)(pagination.PageNumber - 1) * pagination.PageSize;
        var rows = offset >= totalCount
            ? []
            : query
                .Skip((int)offset)
                .Take(pagination.PageSize)
                .ToArray();
        var items = rows
            .Select(ToResponse)
            .ToArray();
        var totalPages = (int)Math.Ceiling(
            totalCount / (double)pagination.PageSize);

        return Result<PagedResponse<PartnerOpeningBalanceResponse>>.Success(
            new PagedResponse<PartnerOpeningBalanceResponse>(
                Items: items,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: totalPages));
    }

    private static IQueryable<PartnerOpeningBalanceListRow> ApplyFilters(
        IQueryable<PartnerOpeningBalanceListRow> query,
        PartnerOpeningBalanceFilterRequest filters)
    {
        var documentNumber = filters.DocumentNumber?.Trim();
        return query
            .Where(balance =>
                string.IsNullOrEmpty(documentNumber) ||
                balance.DocumentNumber.Contains(documentNumber))
            .Where(balance =>
                !filters.BusinessPartnerId.HasValue ||
                balance.BusinessPartnerId == filters.BusinessPartnerId.Value)
            .Where(balance =>
                !filters.Currency.HasValue ||
                balance.Currency == filters.Currency.Value)
            .Where(balance =>
                !filters.BalanceType.HasValue ||
                balance.BalanceType == filters.BalanceType.Value)
            .Where(balance =>
                !filters.FromDate.HasValue ||
                balance.DocumentDate >= filters.FromDate.Value)
            .Where(balance =>
                !filters.ToDate.HasValue ||
                balance.DocumentDate <= filters.ToDate.Value);
    }

    public async Task<Result<PartnerOpeningBalanceResponse>> GetByIdAsync(
        int id,
        int? fiscalYearId = null,
        CancellationToken cancellationToken = default)
    {
        if (id == 0)
        {
            return Result<PartnerOpeningBalanceResponse>.Failure(InvalidId());
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: fiscalYearId,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<PartnerOpeningBalanceResponse>.Failure(
                fiscalYear.Errors);
        }

        if (id < 0)
        {
            var baseCurrency = await dbContext.CompanySettings
                .AsNoTracking()
                .Where(settings => settings.CompanyId == companyId)
                .Select(settings => (CurrencyCode?)settings.BaseCurrency)
                .SingleOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;
            var carriedForward = await CreateCarriedForwardRows(
                    fiscalYearId: fiscalYear.Value.FiscalYearId,
                    fiscalYearName: fiscalYear.Value.FiscalYearName,
                    baseCurrency: baseCurrency)
                .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);

            return carriedForward is null
                ? Result<PartnerOpeningBalanceResponse>.Failure(NotFound(id))
                : Result<PartnerOpeningBalanceResponse>.Success(
                    ToResponse(carriedForward));
        }

        var response = await ProjectResponseQuery(
                    id,
                    fiscalYear.Value.FiscalYearId)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<PartnerOpeningBalanceResponse>.Failure(NotFound(id))
            : Result<PartnerOpeningBalanceResponse>.Success(response);
    }

    public async Task<Result<PartnerOpeningBalanceResponse>> AddAsync(
        PartnerOpeningBalanceRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                request.DocumentDate,
                nameof(PartnerOpeningBalanceRequest.DocumentDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<PartnerOpeningBalanceResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        var normalized = request.Adapt<PartnerOpeningBalance>();

        var partnerError = await ValidateBusinessPartnerAsync(
            normalized.BusinessPartnerId,
            normalized.Currency,
            cancellationToken);
        if (partnerError is not null)
        {
            return Result<PartnerOpeningBalanceResponse>.Failure(partnerError);
        }

        var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
            normalized.Currency,
            normalized.DocumentDate,
            request.ExchangeRate,
            cancellationToken);
        if (exchangeRateResult.IsFailure)
        {
            return Result<PartnerOpeningBalanceResponse>.Failure(
                exchangeRateResult.Error);
        }

        normalized.CompanyId = companyId;
        normalized.DocumentNumber = await EntityIdentifierGenerator
            .GenerateUniqueAsync(
                dbContext,
                prefix: "POB",
                companyId: companyId,
                existingIdentifiers: dbContext.PartnerOpeningBalances
                    .IgnoreQueryFilters()
                    .Where(entity => entity.CompanyId == companyId)
                    .Select(entity => entity.DocumentNumber),
                cancellationToken);
        normalized.ApplyExchangeRate(
            exchangeRateResult.Value.ExchangeRateId,
            exchangeRateResult.Value.Rate);
        dbContext.PartnerOpeningBalances.Add(normalized);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (openingBalancePostingService is not null)
        {
            var postingResult = await openingBalancePostingService
                .SynchronizePartnerAsync(normalized.Id, cancellationToken);
            if (postingResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<PartnerOpeningBalanceResponse>.Failure(
                    postingResult.Errors);
            }
        }

        var response = await ProjectResponseQuery(normalized.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result<PartnerOpeningBalanceResponse>.Success(response);
    }

    public async Task<Result<PartnerOpeningBalanceResponse>> UpdateAsync(
        int id,
        PartnerOpeningBalanceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id < 0)
        {
            return Result<PartnerOpeningBalanceResponse>.Failure(
                CarriedForwardReadOnly());
        }

        if (id == 0)
        {
            return Result<PartnerOpeningBalanceResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: > 0 })
        {
            return Result<PartnerOpeningBalanceResponse>.Failure(RowVersionRequired());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var normalized = request.Adapt<PartnerOpeningBalance>();

        var openingBalance = await dbContext.PartnerOpeningBalances
            .FirstOrDefaultAsync(
                balance =>
                    balance.CompanyId == companyId &&
                    balance.Id == id,
                cancellationToken);
        if (openingBalance is null)
        {
            return Result<PartnerOpeningBalanceResponse>.Failure(NotFound(id));
        }

        if (!openingBalance.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<PartnerOpeningBalanceResponse>.Failure(Concurrency());
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: openingBalance.FiscalYearId,
            fromDate: request.DocumentDate,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<PartnerOpeningBalanceResponse>.Failure(
                fiscalYear.Errors);
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                openingBalance.DocumentDate,
                nameof(PartnerOpeningBalanceRequest.DocumentDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<PartnerOpeningBalanceResponse>.Failure(
                    fiscalYearResult.Errors);
            }

            fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                request.DocumentDate,
                nameof(PartnerOpeningBalanceUpdateRequest.DocumentDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<PartnerOpeningBalanceResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        var partnerError = await ValidateBusinessPartnerAsync(
            normalized.BusinessPartnerId,
            normalized.Currency,
            cancellationToken);
        if (partnerError is not null)
        {
            return Result<PartnerOpeningBalanceResponse>.Failure(partnerError);
        }

        var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
            normalized.Currency,
            normalized.DocumentDate,
            request.ExchangeRate,
            cancellationToken);
        if (exchangeRateResult.IsFailure)
        {
            return Result<PartnerOpeningBalanceResponse>.Failure(
                exchangeRateResult.Error);
        }

        openingBalance.BusinessPartnerId = normalized.BusinessPartnerId;
        openingBalance.DocumentDate = normalized.DocumentDate;
        openingBalance.Currency = normalized.Currency;
        openingBalance.BalanceType = normalized.BalanceType;
        openingBalance.Amount = normalized.Amount;
        openingBalance.Notes = normalized.Notes;
        openingBalance.ApplyExchangeRate(
            exchangeRateResult.Value.ExchangeRateId,
            exchangeRateResult.Value.Rate);

        var entry = dbContext.Entry(openingBalance);
        entry.State = EntityState.Modified;
        entry.Property(balance => balance.RowVersion)
            .OriginalValue = request.RowVersion;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            if (openingBalancePostingService is not null)
            {
                var postingResult = await openingBalancePostingService
                    .SynchronizePartnerAsync(id, cancellationToken);
                if (postingResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return Result<PartnerOpeningBalanceResponse>.Failure(
                        postingResult.Errors);
                }
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<PartnerOpeningBalanceResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result<PartnerOpeningBalanceResponse>.Success(response);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id < 0)
        {
            return Result.Failure(CarriedForwardReadOnly());
        }

        if (id == 0)
        {
            return Result.Failure(InvalidId());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var openingBalance = await dbContext.PartnerOpeningBalances
            .FirstOrDefaultAsync(
                balance =>
                    balance.CompanyId == companyId &&
                    balance.Id == id,
                cancellationToken);
        if (openingBalance is null)
        {
            return Result.Failure(NotFound(id));
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                openingBalance.DocumentDate,
                nameof(PartnerOpeningBalanceRequest.DocumentDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result.Failure(fiscalYearResult.Errors);
            }
        }

        dbContext.PartnerOpeningBalances.Remove(openingBalance);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            if (openingBalancePostingService is not null)
            {
                var postingResult = await openingBalancePostingService
                    .DeleteAsync(
                        JournalEntrySourceType.PartnerOpeningBalance,
                        openingBalance.Id,
                        cancellationToken);
                if (postingResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return Result.Failure(postingResult.Errors);
                }
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private IQueryable<PartnerOpeningBalanceResponse> ProjectResponseQuery(
        int id,
        int? fiscalYearId = null) =>
        dbContext.PartnerOpeningBalances
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.Id == id &&
                (!fiscalYearId.HasValue ||
                 balance.FiscalYearId == fiscalYearId.Value))
            .ProjectToType<PartnerOpeningBalanceResponse>();

    private IQueryable<PartnerOpeningBalanceListRow> CreateCarriedForwardRows(
        int fiscalYearId,
        string fiscalYearName,
        CurrencyCode baseCurrency)
    {
        var rows =
            from line in dbContext.JournalEntryLines.AsNoTracking()
            join partner in dbContext.BusinessPartners.AsNoTracking()
                on new { line.CompanyId, Id = line.PartyId!.Value }
                equals new { partner.CompanyId, partner.Id }
            where line.CompanyId == companyId &&
                  line.PartyId.HasValue &&
                  (line.PartyType == JournalPartyType.Customer ||
                   line.PartyType == JournalPartyType.Supplier) &&
                  line.JournalEntry.FiscalYearId == fiscalYearId &&
                  line.JournalEntry.Status == JournalEntryStatus.Posted &&
                  line.JournalEntry.ReversalOfEntryId == null &&
                  line.JournalEntry.ReversedOn == null &&
                  line.JournalEntry.SourceType ==
                      JournalEntrySourceType.FiscalYearClosing
            group line by new
            {
                line.CompanyId,
                BusinessPartnerId = line.PartyId!.Value,
                BusinessPartnerName = partner.Name,
                line.Currency,
                line.JournalEntry.EntryDate,
                line.JournalEntry.EntryNumber,
                line.JournalEntry.SourceNumber
            }
            into groupRows
            where groupRows.Sum(line =>
                line.TransactionDebit - line.TransactionCredit) != 0m
            select new PartnerOpeningBalanceListRow
            {
                Id = -groupRows.Min(line => line.Id),
                CompanyId = groupRows.Key.CompanyId,
                FiscalYearId = fiscalYearId,
                FiscalYearName = fiscalYearName,
                BusinessPartnerId = groupRows.Key.BusinessPartnerId,
                BusinessPartnerName = groupRows.Key.BusinessPartnerName,
                DocumentNumber = groupRows.Key.SourceNumber ??
                    groupRows.Key.EntryNumber,
                DocumentDate = groupRows.Key.EntryDate,
                Currency = groupRows.Key.Currency,
                BaseCurrency = baseCurrency,
                ExchangeRate = Math.Abs(
                    groupRows.Sum(line => line.Debit - line.Credit) /
                    groupRows.Sum(line =>
                        line.TransactionDebit - line.TransactionCredit)),
                BalanceType = groupRows.Sum(line =>
                        line.TransactionDebit - line.TransactionCredit) > 0m
                    ? PartnerBalanceType.Receivable
                    : PartnerBalanceType.Payable,
                Amount = Math.Abs(groupRows.Sum(line =>
                    line.TransactionDebit - line.TransactionCredit)),
                BaseAmount = Math.Abs(groupRows.Sum(line =>
                    line.Debit - line.Credit)),
                Notes = "رصيد مرحّل من إقفال السنة المالية",
                RowVersion = null,
                IsCarriedForward = true
            };

        return rows;
    }

    private static PartnerOpeningBalanceResponse ToResponse(
        PartnerOpeningBalanceListRow row) =>
        new(
            Id: row.Id,
            CompanyId: row.CompanyId,
            FiscalYearId: row.FiscalYearId,
            FiscalYearName: row.FiscalYearName,
            BusinessPartnerId: row.BusinessPartnerId,
            BusinessPartnerName: row.BusinessPartnerName,
            DocumentNumber: row.DocumentNumber,
            DocumentDate: row.DocumentDate,
            Currency: row.Currency,
            BaseCurrency: row.BaseCurrency,
            ExchangeRate: row.ExchangeRate,
            BalanceType: row.BalanceType,
            Amount: row.Amount,
            BaseAmount: row.BaseAmount,
            Notes: row.Notes,
            RowVersion: row.RowVersion ?? [])
        {
            IsCarriedForward = row.IsCarriedForward
        };

    private sealed class PartnerOpeningBalanceListRow
    {
        public int Id { get; init; }

        public int CompanyId { get; init; }

        public int FiscalYearId { get; init; }

        public string FiscalYearName { get; init; } = string.Empty;

        public int BusinessPartnerId { get; init; }

        public string BusinessPartnerName { get; init; } = string.Empty;

        public string DocumentNumber { get; init; } = string.Empty;

        public DateOnly DocumentDate { get; init; }

        public CurrencyCode Currency { get; init; }

        public CurrencyCode BaseCurrency { get; init; }

        public decimal ExchangeRate { get; init; }

        public PartnerBalanceType BalanceType { get; init; }

        public decimal Amount { get; init; }

        public decimal BaseAmount { get; init; }

        public string? Notes { get; init; }

        public byte[]? RowVersion { get; init; }

        public bool IsCarriedForward { get; init; }
    }

    private async Task<Error?> ValidateBusinessPartnerAsync(
        int businessPartnerId,
        CurrencyCode currency,
        CancellationToken cancellationToken)
    {
        var partner = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(candidate =>
                candidate.CompanyId == companyId &&
                candidate.Id == businessPartnerId)
            .Select(candidate => new
            {
                candidate.IsActive,
                candidate.Currency
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (partner is null)
        {
            return BusinessPartnerNotFound(businessPartnerId);
        }

        if (!partner.IsActive)
        {
            return BusinessPartnerInactive();
        }

        return partner.Currency == currency
            ? null
            : CurrencyMismatch();
    }

}
