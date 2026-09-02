using System.Data;
using static MiniErp.Application.Features.PartnerOpeningBalances.PartnerOpeningBalanceErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.PartnerOpeningBalances;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.PartnerOpeningBalances;

public sealed class PartnerOpeningBalanceService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IExchangeRateResolver exchangeRateResolver,
    IFiscalYearPeriodGuard? fiscalYearPeriodGuard = null)
    : IPartnerOpeningBalanceService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<PartnerOpeningBalanceResponse>>> GetAllAsync(
        PaginationRequest pagination,
        PartnerOpeningBalanceFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new PartnerOpeningBalanceFilterRequest();
        var query = dbContext.PartnerOpeningBalances
            .AsNoTracking()
            .Where(balance => balance.CompanyId == companyId)
            .Where(balance =>
                string.IsNullOrWhiteSpace(filters.DocumentNumber) ||
                balance.DocumentNumber.Contains(filters.DocumentNumber.Trim()))
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
                balance.DocumentDate <= filters.ToDate.Value)
            .OrderByDescending(balance => balance.DocumentDate)
            .ThenByDescending(balance => balance.Id);

        return await paginationService.PaginateAsync<
            PartnerOpeningBalance,
            PartnerOpeningBalanceResponse>(
                query,
                pagination,
                cancellationToken);
    }

    public async Task<Result<PartnerOpeningBalanceResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<PartnerOpeningBalanceResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
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
        if (id <= 0)
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
        if (id <= 0)
        {
            return Result.Failure(InvalidId());
        }

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
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }

        return Result.Success();
    }

    private IQueryable<PartnerOpeningBalanceResponse> ProjectResponseQuery(int id) =>
        dbContext.PartnerOpeningBalances
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.Id == id)
            .ProjectToType<PartnerOpeningBalanceResponse>();

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
