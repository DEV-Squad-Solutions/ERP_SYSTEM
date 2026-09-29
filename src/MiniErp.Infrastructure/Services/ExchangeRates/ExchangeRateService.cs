using System.Data;
using System.Globalization;
using static MiniErp.Application.Features.ExchangeRates.ExchangeRateErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.ExchangeRates;

public sealed class ExchangeRateService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    TimeProvider timeProvider,
    IExchangeRateResolver exchangeRateResolver,
    IFiscalYearQueryScopeResolver fiscalYearQueryScopeResolver,
    IFiscalYearPeriodGuard fiscalYearPeriodGuard,
    IExchangeRateProvider? exchangeRateProvider = null,
    IExchangeRatePostingSynchronizer? exchangeRatePostingSynchronizer = null,
    IInventoryCostingService? inventoryCostingService = null)
    : IExchangeRateService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<ExchangeRateResponse>>> GetAllAsync(
        PaginationRequest pagination,
        ExchangeRateFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new ExchangeRateFilterRequest();

        var fiscalYearResult = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: filters.FiscalYearId,
            fromDate: filters.DateFrom,
            toDate: filters.DateTo,
            cancellationToken: cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result<PagedResponse<ExchangeRateResponse>>.Failure(
                fiscalYearResult.Errors);
        }

        var fiscalYearId = fiscalYearResult.Value.FiscalYearId;

        var normalizedSearch = Normalize(filters.Search);
        CurrencyCode[] searchCurrencies = normalizedSearch is null
            ? []
            : Enum.GetValues<CurrencyCode>()
                .Where(currency => currency.ToString().Contains(
                    normalizedSearch,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

        var query = dbContext.ExchangeRates
            .AsNoTracking()
            .Where(rate =>
                rate.CompanyId == companyId &&
                rate.FiscalYearId == fiscalYearId);

        if (normalizedSearch is not null)
        {
            var searchText = normalizedSearch.ToLowerInvariant();
            var notesQuery = query.Where(rate =>
                rate.Notes != null &&
                rate.Notes.ToLower().Contains(searchText));

            if (searchCurrencies.Length > 0)
            {
                var currencyQuery = query.Where(rate =>
                    rate.Currency == searchCurrencies[0]);
                foreach (var currency in searchCurrencies.Skip(1))
                {
                    var candidate = currency;
                    currencyQuery = currencyQuery.Union(query.Where(rate =>
                        rate.Currency == candidate));
                }

                query = notesQuery.Union(currencyQuery);
            }
            else
            {
                query = notesQuery;
            }
        }

        var orderedQuery = query
            .Where(rate =>
                !filters.Currency.HasValue ||
                rate.Currency == filters.Currency.Value)
            .Where(rate =>
                !filters.DateFrom.HasValue ||
                rate.RateDate >= filters.DateFrom.Value)
            .Where(rate =>
                !filters.DateTo.HasValue ||
                rate.RateDate <= filters.DateTo.Value)
            .Where(rate =>
                !filters.Source.HasValue ||
                rate.Source == filters.Source.Value)
            .OrderByDescending(rate => rate.RateDate)
            .ThenByDescending(rate => rate.Id);

        return await paginationService.PaginateAsync<
            ExchangeRate,
            ExchangeRateResponse>(
            orderedQuery,
            pagination,
            cancellationToken);
    }

    public async Task<Result<ExchangeRateResponse>> GetByIdAsync(
        int id,
        int? fiscalYearId = null,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<ExchangeRateResponse>.Failure(InvalidId());
        }

        var fiscalYearResult = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: fiscalYearId,
            cancellationToken: cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result<ExchangeRateResponse>.Failure(
                fiscalYearResult.Errors);
        }

        var response = await ProjectResponseQuery(
                id,
                fiscalYearResult.Value.FiscalYearId)
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<ExchangeRateResponse>.Failure(NotFound(id))
            : Result<ExchangeRateResponse>.Success(response);
    }

    public async Task<Result<ExchangeRateResolutionResponse>> ResolveAsync(
        CurrencyCode currency,
        DateOnly date,
        int? fiscalYearId = null,
        CancellationToken cancellationToken = default)
    {
        var fiscalYearResult = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: fiscalYearId,
            fromDate: date,
            toDate: date,
            cancellationToken: cancellationToken);
        if (fiscalYearResult.IsFailure)
        {
            return Result<ExchangeRateResolutionResponse>.Failure(
                fiscalYearResult.Errors);
        }

        var result = await exchangeRateResolver.ResolveAsync(
            currency,
            date,
            requestedRate: null,
            cancellationToken: cancellationToken);

        return result.IsFailure
            ? Result<ExchangeRateResolutionResponse>.Failure(result.Error)
            : Result<ExchangeRateResolutionResponse>.Success(
                new ExchangeRateResolutionResponse(
                    ExchangeRateId: result.Value.ExchangeRateId,
                    BaseCurrency: result.Value.BaseCurrency,
                    Currency: result.Value.Currency,
                    RequestedDate: result.Value.RequestedDate,
                    RateDate: result.Value.RateDate,
                    Rate: result.Value.Rate,
                    Source: result.Value.Source,
                    IsBaseCurrency: result.Value.IsBaseCurrency));
    }

    public async Task<Result<ExchangeRateImportPreviewResponse>> PreviewImportAsync(
        ExchangeRateImportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (exchangeRateProvider is null)
        {
            return Result<ExchangeRateImportPreviewResponse>.Failure(
                ExternalExchangeRateErrors.ProviderUnavailable());
        }

        var baseCurrency = await GetRequiredBaseCurrencyAsync(cancellationToken);
        if (!baseCurrency.HasValue)
        {
            return Result<ExchangeRateImportPreviewResponse>.Failure(CompanySettingsNotFound());
        }

        var currencies = (request.Currencies is null || request.Currencies.Count == 0
                ? Enum.GetValues<CurrencyCode>()
                : request.Currencies)
            .Where(currency => currency != baseCurrency.Value)
            .Distinct()
            .ToArray();
        var items = new List<ExchangeRateImportPreviewItemResponse>(currencies.Length);
        var receivedCount = 0;

        foreach (var currency in currencies)
        {
            var providerResult = await exchangeRateProvider.GetRateAsync(
                currency, baseCurrency.Value, request.RateDate, cancellationToken);
            if (providerResult.IsSuccess)
            {
                var rate = providerResult.Value;
                items.Add(new ExchangeRateImportPreviewItemResponse(
                    Currency: rate.Currency,
                    BaseCurrency: rate.BaseCurrency,
                    RequestedDate: rate.RequestedDate,
                    RateDate: rate.RateDate,
                    Rate: rate.Rate,
                    Error: null));
                receivedCount++;
                continue;
            }

            if (providerResult.Error.Code is
                "ExchangeRates.ProviderRateNotFound" or
                "ExchangeRates.ProviderUnsupportedCurrency")
            {
                items.Add(new ExchangeRateImportPreviewItemResponse(
                    Currency: currency,
                    BaseCurrency: baseCurrency.Value,
                    RequestedDate: request.RateDate,
                    RateDate: null,
                    Rate: null,
                    Error: providerResult.Error.Description));
                continue;
            }

            return Result<ExchangeRateImportPreviewResponse>.Failure(providerResult.Error);
        }

        return Result<ExchangeRateImportPreviewResponse>.Success(
            new ExchangeRateImportPreviewResponse(
                RequestedDate: request.RateDate,
                Provider: exchangeRateProvider.Name,
                BaseCurrency: baseCurrency.Value,
                RequestedCount: currencies.Length,
                ReceivedCount: receivedCount,
                Items: items));
    }

    public async Task<Result<ExchangeRateImportResponse>> ImportAsync(
        ExchangeRateImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var periodResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
            request.RateDate,
            nameof(ExchangeRateImportRequest.RateDate),
            cancellationToken);
        if (periodResult.IsFailure)
        {
            return Result<ExchangeRateImportResponse>.Failure(
                periodResult.Errors);
        }

        if (exchangeRateProvider is null)
        {
            return Result<ExchangeRateImportResponse>.Failure(
                ExternalExchangeRateErrors.ProviderUnavailable());
        }

        var baseCurrency = await GetRequiredBaseCurrencyAsync(cancellationToken);
        if (!baseCurrency.HasValue)
        {
            return Result<ExchangeRateImportResponse>.Failure(CompanySettingsNotFound());
        }

        var currencies = (request.Currencies is null || request.Currencies.Count == 0
                ? Enum.GetValues<CurrencyCode>()
                : request.Currencies)
            .Where(currency => currency != baseCurrency.Value)
            .Distinct()
            .ToArray();

        var items = new List<ExchangeRateImportItemResponse>(currencies.Length);
        var received = new List<ExternalExchangeRate>(currencies.Length);

        foreach (var currency in currencies)
        {
            var providerResult = await exchangeRateProvider.GetRateAsync(
                currency, baseCurrency.Value, request.RateDate, cancellationToken);
            if (providerResult.IsSuccess)
            {
                received.Add(providerResult.Value);
                continue;
            }

            if (providerResult.Error.Code is
                "ExchangeRates.ProviderRateNotFound" or
                "ExchangeRates.ProviderUnsupportedCurrency")
            {
                items.Add(new ExchangeRateImportItemResponse(
                    Currency: currency,
                    BaseCurrency: baseCurrency.Value,
                    RequestedDate: request.RateDate,
                    RateDate: null,
                    Rate: null,
                    Status: ExchangeRateImportItemStatus.Failed,
                    Reason: providerResult.Error.Description));
                continue;
            }

            return Result<ExchangeRateImportResponse>.Failure(providerResult.Error);
        }

        // The provider answers with its latest published rate, which on a
        // holiday or weekend is dated earlier (for example 1 January returns
        // 31 December, possibly in a closed fiscal year). The imported rate is
        // the rate in effect on the requested date, so it is stored on that
        // date: it stays in the requested (open) fiscal year, where documents
        // resolve rates. The provider's publication date is kept in Notes.
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            foreach (var externalRate in received)
            {
                var importNotes = ProviderDateNote(externalRate);
                var existing = await dbContext.ExchangeRates.FirstOrDefaultAsync(rate =>
                    rate.CompanyId == companyId &&
                    rate.Currency == externalRate.Currency &&
                    rate.RateDate == request.RateDate,
                    cancellationToken);

                if (existing is null)
                {
                    var imported = new ExchangeRate
                    {
                        CompanyId = companyId,
                        Currency = externalRate.Currency,
                        RateDate = request.RateDate,
                        Rate = externalRate.Rate,
                        Source = ExchangeRateSource.Imported,
                        Provider = externalRate.Provider,
                        Notes = importNotes
                    };
                    imported.Touch(timeProvider.GetUtcNow().UtcDateTime);
                    dbContext.ExchangeRates.Add(imported);
                    items.Add(ImportItem(externalRate, ExchangeRateImportItemStatus.Imported, null));
                    continue;
                }

                if (existing.Source == ExchangeRateSource.Manual)
                {
                    items.Add(ImportItem(externalRate, ExchangeRateImportItemStatus.Skipped,
                        "A manual exchange rate already exists for this currency and date."));
                    continue;
                }

                if (await IsReferencedAsync(existing.Id, cancellationToken))
                {
                    items.Add(ImportItem(externalRate, ExchangeRateImportItemStatus.Skipped,
                        "The existing imported exchange rate is referenced by a financial document."));
                    continue;
                }

                if (!request.ReplaceUnreferencedImportedRates)
                {
                    items.Add(ImportItem(externalRate, ExchangeRateImportItemStatus.Skipped,
                        "An imported exchange rate already exists; replacement was not requested."));
                    continue;
                }

                var entry = dbContext.Entry(existing);
                existing.Rate = externalRate.Rate;
                existing.Source = ExchangeRateSource.Imported;
                existing.Provider = externalRate.Provider;
                existing.Notes = importNotes;
                existing.Touch(timeProvider.GetUtcNow().UtcDateTime);
                entry.Property(rate => rate.LastModifiedAt).IsModified = true;
                items.Add(ImportItem(externalRate, ExchangeRateImportItemStatus.Updated, null));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            await transaction.RollbackAsync(cancellationToken);
            return Result<ExchangeRateImportResponse>.Failure(Concurrency());
        }
        catch (DbUpdateException exception) when (IsDuplicateConstraint(exception))
        {
            dbContext.ChangeTracker.Clear();
            await transaction.RollbackAsync(cancellationToken);
            return Result<ExchangeRateImportResponse>.Failure(Duplicate());
        }

        return Result<ExchangeRateImportResponse>.Success(
            BuildImportResponse(request.RateDate, exchangeRateProvider.Name,
                currencies.Length, received.Count, items));
    }

    private static string? ProviderDateNote(ExternalExchangeRate externalRate) =>
        externalRate.RateDate == externalRate.RequestedDate
            ? null
            : string.Create(
                CultureInfo.InvariantCulture,
                $"سعر المصدر منشور بتاريخ {externalRate.RateDate:yyyy-MM-dd}");

    private static ExchangeRateImportItemResponse ImportItem(
        ExternalExchangeRate externalRate,
        ExchangeRateImportItemStatus status,
        string? reason) => new(
        Currency: externalRate.Currency,
        BaseCurrency: externalRate.BaseCurrency,
        RequestedDate: externalRate.RequestedDate,
        RateDate: externalRate.RateDate,
        Rate: externalRate.Rate,
        Status: status,
        Reason: reason);

    private static ExchangeRateImportResponse BuildImportResponse(
        DateOnly requestedDate, string provider, int requestedCount, int receivedCount,
        IReadOnlyList<ExchangeRateImportItemResponse> items)
    {
        var imported = items.Count(item => item.Status == ExchangeRateImportItemStatus.Imported);
        var updated = items.Count(item => item.Status == ExchangeRateImportItemStatus.Updated);
        var skipped = items.Count(item => item.Status == ExchangeRateImportItemStatus.Skipped);
        var failed = items.Count(item => item.Status == ExchangeRateImportItemStatus.Failed);
        return new ExchangeRateImportResponse(
            RequestedDate: requestedDate,
            Provider: provider,
            RequestedCount: requestedCount,
            ReceivedCount: receivedCount,
            ImportedCount: imported,
            UpdatedCount: updated,
            SkippedCount: skipped,
            FailedCount: failed,
            Items: items);
    }

    public async Task<Result<ExchangeRateResponse>> AddAsync(
        ExchangeRateRequest request,
        CancellationToken cancellationToken = default)
    {
        var periodResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
            request.RateDate,
            nameof(ExchangeRateRequest.RateDate),
            cancellationToken);
        if (periodResult.IsFailure)
        {
            return Result<ExchangeRateResponse>.Failure(periodResult.Errors);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var baseCurrency = await GetBaseCurrencyAsync(cancellationToken);
        var validationError = ValidateRate(
            request.Currency,
            request.Rate,
            baseCurrency);
        if (validationError is not null)
        {
            return Result<ExchangeRateResponse>.Failure(validationError);
        }

        if (await DuplicateExistsAsync(
                request.Currency,
                request.RateDate,
                excludedId: null,
                cancellationToken))
        {
            return Result<ExchangeRateResponse>.Failure(Duplicate());
        }

        var rate = new ExchangeRate
        {
            CompanyId = companyId,
            Currency = request.Currency,
            RateDate = request.RateDate,
            Rate = ExchangeRateRules.RoundRate(request.Rate),
            Source = ExchangeRateSource.Manual,
            Provider = null,
            Notes = Normalize(request.Notes)
        };
        rate.Touch(timeProvider.GetUtcNow().UtcDateTime);

        dbContext.ExchangeRates.Add(rate);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsDuplicateConstraint(exception))
        {
            dbContext.ChangeTracker.Clear();
            return Result<ExchangeRateResponse>.Failure(Duplicate());
        }

        var response = await ProjectResponseQuery(rate.Id)
            .SingleAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<ExchangeRateResponse>.Success(response);
    }

    public async Task<Result<ExchangeRateResponse>> UpdateAsync(
        int id,
        ExchangeRateUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<ExchangeRateResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<ExchangeRateResponse>.Failure(
                RowVersionRequired());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var rate = await dbContext.ExchangeRates
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == id,
                cancellationToken);
        if (rate is null)
        {
            return Result<ExchangeRateResponse>.Failure(NotFound(id));
        }

        var periodResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
            rate.RateDate,
            nameof(ExchangeRateUpdateRequest.RateDate),
            cancellationToken);
        if (periodResult.IsFailure)
        {
            return Result<ExchangeRateResponse>.Failure(periodResult.Errors);
        }

        periodResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
            request.RateDate,
            nameof(ExchangeRateUpdateRequest.RateDate),
            cancellationToken);
        if (periodResult.IsFailure)
        {
            return Result<ExchangeRateResponse>.Failure(periodResult.Errors);
        }

        if (!rate.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<ExchangeRateResponse>.Failure(Concurrency());
        }

        var isReferenced = await IsReferencedAsync(id, cancellationToken);
        var rateChanges = ExchangeRateRules.RoundRate(request.Rate) != rate.Rate;
        // Notes can change freely; only a new rate value rewrites the linked
        // documents and therefore needs the explicit cascade option.
        if (isReferenced && rateChanges && !request.UpdateLinkedTransactions)
        {
            return Result<ExchangeRateResponse>.Failure(Referenced());
        }


        if (isReferenced &&
            (request.Currency != rate.Currency ||
             request.RateDate != rate.RateDate))
        {
            return Result<ExchangeRateResponse>.Failure(
                ReferencedIdentityChangeNotAllowed());
        }

        var baseCurrency = await GetBaseCurrencyAsync(cancellationToken);
        var validationError = ValidateRate(
            request.Currency,
            request.Rate,
            baseCurrency);
        if (validationError is not null)
        {
            return Result<ExchangeRateResponse>.Failure(validationError);
        }

        if (await DuplicateExistsAsync(
                request.Currency,
                request.RateDate,
                id,
                cancellationToken))
        {
            return Result<ExchangeRateResponse>.Failure(Duplicate());
        }

        var entry = dbContext.Entry(rate);
        entry.Property(entity => entity.RowVersion).OriginalValue =
            request.RowVersion;

        if (request.UpdateLinkedTransactions)
        {
            var cascadeError = await ExchangeRateCascadeUpdater.UpdateAsync(
                dbContext,
                companyId,
                timeProvider,
                id,
                request.Rate,
                InvalidLinkedTransfer(),
                cancellationToken);
            if (cascadeError is not null)
            {
                return Result<ExchangeRateResponse>.Failure(cascadeError);
            }
        }

        // A user-set value makes the rate manual; editing only its notes keeps
        // an imported or carried-forward rate's origin.
        if (rateChanges ||
            request.Currency != rate.Currency ||
            request.RateDate != rate.RateDate)
        {
            rate.Source = ExchangeRateSource.Manual;
            rate.Provider = null;
        }

        rate.Currency = request.Currency;
        rate.RateDate = request.RateDate;
        rate.Rate = ExchangeRateRules.RoundRate(request.Rate);
        rate.Notes = Normalize(request.Notes);
        rate.Touch(timeProvider.GetUtcNow().UtcDateTime);
        entry.Property(entity => entity.LastModifiedAt).IsModified = true;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            if (request.UpdateLinkedTransactions &&
                inventoryCostingService is not null)
            {
                var costingKeys = await dbContext.InvoiceLines
                    .AsNoTracking()
                    .Where(line =>
                        line.CompanyId == companyId &&
                        line.ItemId.HasValue &&
                        line.Invoice.ExchangeRateId == id &&
                        line.Invoice.InvoiceType == InvoiceType.Purchase)
                    .Select(line => new InventoryCostingKey(
                        line.Invoice.StoreId,
                        line.ItemId!.Value,
                        line.Invoice.FiscalYearId))
                    .Distinct()
                    .ToArrayAsync(cancellationToken);
                var costingError = await inventoryCostingService
                    .RecalculateAsync(costingKeys, cancellationToken);
                if (costingError is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return Result<ExchangeRateResponse>.Failure(costingError);
                }
            }

            if (request.UpdateLinkedTransactions &&
                exchangeRatePostingSynchronizer is not null)
            {
                var postingResult = await exchangeRatePostingSynchronizer
                    .SynchronizeAsync(id, cancellationToken);
                if (postingResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return Result<ExchangeRateResponse>.Failure(
                        postingResult.Errors);
                }
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<ExchangeRateResponse>.Failure(Concurrency());
        }
        catch (DbUpdateException exception)
            when (IsDuplicateConstraint(exception))
        {
            dbContext.ChangeTracker.Clear();
            return Result<ExchangeRateResponse>.Failure(Duplicate());
        }

        var response = await ProjectResponseQuery(id)
            .SingleAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<ExchangeRateResponse>.Success(response);
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
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var rate = await dbContext.ExchangeRates
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == id,
                cancellationToken);
        if (rate is null)
        {
            return Result.Failure(NotFound(id));
        }

        var periodResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
            rate.RateDate,
            nameof(ExchangeRateRequest.RateDate),
            cancellationToken);
        if (periodResult.IsFailure)
        {
            return periodResult;
        }

        if (await IsReferencedAsync(id, cancellationToken))
        {
            return Result.Failure(Referenced());
        }

        dbContext.ExchangeRates.Remove(rate);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<CurrencyCode> GetBaseCurrencyAsync(
        CancellationToken cancellationToken) =>
        await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ??
        CurrencyCode.EGP;

    private async Task<CurrencyCode?> GetRequiredBaseCurrencyAsync(
        CancellationToken cancellationToken) =>
        await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken);

    private Task<bool> DuplicateExistsAsync(
        CurrencyCode currency,
        DateOnly rateDate,
        int? excludedId,
        CancellationToken cancellationToken) =>
        dbContext.ExchangeRates
            .AsNoTracking()
            .AnyAsync(
                rate =>
                    rate.CompanyId == companyId &&
                    rate.Currency == currency &&
                    rate.RateDate == rateDate &&
                    (!excludedId.HasValue || rate.Id != excludedId.Value),
                cancellationToken);

    private Task<bool> IsReferencedAsync(
        int id,
        CancellationToken cancellationToken) =>
        ExchangeRateReferences.IsReferencedAsync(
            dbContext,
            companyId,
            id,
            cancellationToken);

    private IOrderedQueryable<ExchangeRateResponse> ProjectResponseQuery(
        int id,
        int? fiscalYearId = null) =>
        dbContext.ExchangeRates
            .AsNoTracking()
            .Where(rate =>
                rate.CompanyId == companyId &&
                (!fiscalYearId.HasValue ||
                 rate.FiscalYearId == fiscalYearId.Value) &&
                rate.Id == id)
            .ProjectToType<ExchangeRateResponse>()
            .OrderBy(response => response.Id);

    private static Error? ValidateRate(
        CurrencyCode currency,
        decimal rate,
        CurrencyCode baseCurrency)
    {
        if (!Enum.IsDefined(currency))
        {
            return InvalidCurrency();
        }

        if (currency == baseCurrency)
        {
            return BaseCurrencyRateNotAllowed();
        }

        return ExchangeRateRules.IsValidRate(rate)
            ? null
            : InvalidRate();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsDuplicateConstraint(DbUpdateException exception)
    {
        var message = exception.ToString();
        // SQL Server reports the unique index name; the index gained the
        // FiscalYearId column when rates became fiscal-year owned.
        return message.Contains(
                   "IX_ExchangeRates_CompanyId_FiscalYearId_Currency_RateDate",
                   StringComparison.OrdinalIgnoreCase) ||
            message.Contains(
                   "IX_ExchangeRates_CompanyId_Currency_RateDate",
                   StringComparison.OrdinalIgnoreCase) ||
            message.Contains(
                "ExchangeRates.CompanyId, ExchangeRates.FiscalYearId, ExchangeRates.Currency, ExchangeRates.RateDate",
                StringComparison.OrdinalIgnoreCase) ||
            message.Contains(
                "UNIQUE constraint failed: ExchangeRates.CompanyId, ExchangeRates.FiscalYearId, ExchangeRates.Currency, ExchangeRates.RateDate",
                StringComparison.OrdinalIgnoreCase) ||
            message.Contains(
                "UNIQUE constraint failed: ExchangeRates.CompanyId, ExchangeRates.Currency, ExchangeRates.RateDate",
                StringComparison.OrdinalIgnoreCase);
    }

}
