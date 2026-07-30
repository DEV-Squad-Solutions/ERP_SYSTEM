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
    TimeProvider timeProvider)
    : IExchangeRateService, IExchangeRateResolver, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<ExchangeRateResponse>>> GetAllAsync(
        PaginationRequest pagination,
        ExchangeRateFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new ExchangeRateFilterRequest();

        var query = dbContext.ExchangeRates
            .AsNoTracking()
            .Where(rate => rate.CompanyId == companyId)
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
            .ThenBy(rate => rate.Currency)
            .ThenByDescending(rate => rate.Id);

        return await paginationService.PaginateAsync<
            ExchangeRate,
            ExchangeRateResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<ExchangeRateResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<ExchangeRateResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<ExchangeRateResponse>.Failure(NotFound(id))
            : Result<ExchangeRateResponse>.Success(response);
    }

    public async Task<Result<ExchangeRateResolutionResponse>> ResolveAsync(
        CurrencyCode currency,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var result = await ResolveAsync(
            currency,
            date,
            requestedRate: null,
            cancellationToken);

        return result.IsFailure
            ? Result<ExchangeRateResolutionResponse>.Failure(result.Error)
            : Result<ExchangeRateResolutionResponse>.Success(
                new ExchangeRateResolutionResponse(
                    result.Value.ExchangeRateId,
                    result.Value.BaseCurrency,
                    result.Value.Currency,
                    result.Value.RequestedDate,
                    result.Value.RateDate,
                    result.Value.Rate,
                    result.Value.Source,
                    result.Value.IsBaseCurrency));
    }

    async Task<Result<ResolvedExchangeRate>> IExchangeRateResolver.ResolveAsync(
        CurrencyCode currency,
        DateOnly date,
        decimal? requestedRate,
        CancellationToken cancellationToken) =>
        await ResolveAsync(
            currency,
            date,
            requestedRate,
            cancellationToken);

    public async Task<Result<ExchangeRateResponse>> AddAsync(
        ExchangeRateRequest request,
        CancellationToken cancellationToken = default)
    {
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
            Source = request.Source,
            Notes = Normalize(request.Notes)
        };
        rate.Touch(timeProvider.GetUtcNow().UtcDateTime);

        dbContext.ExchangeRates.Add(rate);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ProjectResponseQuery(rate.Id)
            .SingleAsync(cancellationToken);
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

        if (!rate.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<ExchangeRateResponse>.Failure(Concurrency());
        }

        if (await IsReferencedAsync(id, cancellationToken))
        {
            return Result<ExchangeRateResponse>.Failure(Referenced());
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

        rate.Currency = request.Currency;
        rate.RateDate = request.RateDate;
        rate.Rate = ExchangeRateRules.RoundRate(request.Rate);
        rate.Source = request.Source;
        rate.Notes = Normalize(request.Notes);
        rate.Touch(timeProvider.GetUtcNow().UtcDateTime);
        entry.Property(entity => entity.LastModifiedAt).IsModified = true;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<ExchangeRateResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
            .SingleAsync(cancellationToken);
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

        if (await IsReferencedAsync(id, cancellationToken))
        {
            return Result.Failure(Referenced());
        }

        dbContext.ExchangeRates.Remove(rate);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<ResolvedExchangeRate>> ResolveAsync(
        CurrencyCode currency,
        DateOnly date,
        decimal? requestedRate,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(currency))
        {
            return Result<ResolvedExchangeRate>.Failure(InvalidCurrency());
        }

        var baseCurrency = await GetBaseCurrencyAsync(cancellationToken);
        if (currency == baseCurrency)
        {
            if (requestedRate.HasValue && requestedRate.Value != 1m)
            {
                return Result<ResolvedExchangeRate>.Failure(
                    BaseCurrencyRateMustBeOne());
            }

            return Result<ResolvedExchangeRate>.Success(
                new ResolvedExchangeRate(
                    null,
                    baseCurrency,
                    currency,
                    date,
                    null,
                    1m,
                    null,
                    true));
        }

        if (requestedRate.HasValue)
        {
            if (!ExchangeRateRules.IsValidRate(requestedRate.Value))
            {
                return Result<ResolvedExchangeRate>.Failure(InvalidRate());
            }

            return Result<ResolvedExchangeRate>.Success(
                new ResolvedExchangeRate(
                    null,
                    baseCurrency,
                    currency,
                    date,
                    date,
                    ExchangeRateRules.RoundRate(requestedRate.Value),
                    ExchangeRateSource.Manual,
                    false));
        }

        var rate = await dbContext.ExchangeRates
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Currency == currency &&
                entity.RateDate <= date)
            .OrderByDescending(entity => entity.RateDate)
            .ThenByDescending(entity => entity.Id)
            .Select(entity => new
            {
                entity.Id,
                entity.RateDate,
                entity.Rate,
                entity.Source
            })
            .FirstOrDefaultAsync(cancellationToken);

        return rate is null
            ? Result<ResolvedExchangeRate>.Failure(Missing(currency, date))
            : Result<ResolvedExchangeRate>.Success(
                new ResolvedExchangeRate(
                    rate.Id,
                    baseCurrency,
                    currency,
                    date,
                    rate.RateDate,
                    rate.Rate,
                    rate.Source,
                    false));
    }

    private async Task<CurrencyCode> GetBaseCurrencyAsync(
        CancellationToken cancellationToken) =>
        await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ??
        CurrencyCode.EGP;

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

    private async Task<bool> IsReferencedAsync(
        int id,
        CancellationToken cancellationToken) =>
        await dbContext.Invoices
            .IgnoreQueryFilters()
            .AnyAsync(
                invoice =>
                    invoice.CompanyId == companyId &&
                    invoice.ExchangeRateId == id,
                cancellationToken) ||
        await dbContext.CashVouchers
            .IgnoreQueryFilters()
            .AnyAsync(
                voucher =>
                    voucher.CompanyId == companyId &&
                    voucher.ExchangeRateId == id,
                cancellationToken) ||
        await dbContext.PartnerOpeningBalances
            .IgnoreQueryFilters()
            .AnyAsync(
                balance =>
                    balance.CompanyId == companyId &&
                    balance.ExchangeRateId == id,
                cancellationToken) ||
        await dbContext.Cashboxes
            .IgnoreQueryFilters()
            .AnyAsync(
                cashbox =>
                    cashbox.CompanyId == companyId &&
                    cashbox.OpeningExchangeRateId == id,
                cancellationToken);

    private IOrderedQueryable<ExchangeRateResponse> ProjectResponseQuery(
        int id) =>
        dbContext.ExchangeRates
            .AsNoTracking()
            .Where(rate =>
                rate.CompanyId == companyId &&
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

    private static Error InvalidId() =>
        Error.Validation(
            "ExchangeRates.InvalidId",
            "يجب أن يكون رقم سعر الصرف أكبر من صفر.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "ExchangeRates.NotFound",
            $"لم يتم العثور على سعر الصرف رقم {id}.");

    private static Error InvalidCurrency() =>
        Error.Validation(
            "ExchangeRates.InvalidCurrency",
            "العملة المحددة غير صالحة.",
            nameof(ExchangeRateRequest.Currency));

    private static Error InvalidRate() =>
        Error.Validation(
            "ExchangeRates.InvalidRate",
            "يجب أن يكون سعر الصرف أكبر من صفر وألا يتجاوز 12 منزلة عشرية.",
            nameof(ExchangeRateRequest.Rate));

    private static Error BaseCurrencyRateNotAllowed() =>
        Error.Validation(
            "ExchangeRates.BaseCurrencyRateNotAllowed",
            "لا يتم إنشاء سعر صرف لعملة الشركة الأساسية؛ سعرها يساوي واحدًا دائمًا.",
            nameof(ExchangeRateRequest.Currency));

    private static Error BaseCurrencyRateMustBeOne() =>
        Error.Validation(
            "ExchangeRates.BaseCurrencyRateMustBeOne",
            "يجب أن يساوي سعر صرف عملة الشركة الأساسية واحدًا.",
            "exchangeRate");

    private static Error Missing(
        CurrencyCode currency,
        DateOnly date) =>
        Error.Validation(
            "ExchangeRates.Missing",
            $"لا يوجد سعر صرف للعملة {currency} بتاريخ {date:yyyy-MM-dd} أو قبله.",
            "exchangeRate");

    private static Error Duplicate() =>
        Error.Conflict(
            "ExchangeRates.Duplicate",
            "يوجد سعر صرف نشط لهذه العملة في التاريخ نفسه.",
            nameof(ExchangeRateRequest.RateDate));

    private static Error Referenced() =>
        Error.Conflict(
            "ExchangeRates.Referenced",
            "لا يمكن تعديل أو حذف سعر صرف مستخدم في مستند مالي. أضف سعرًا بتاريخ جديد بدلًا من ذلك.");

    private static Error RowVersionRequired() =>
        Error.Validation(
            "ExchangeRates.RowVersionRequired",
            "يجب إرسال إصدار سعر الصرف الحالي.",
            nameof(ExchangeRateUpdateRequest.RowVersion));

    private static Error Concurrency() =>
        Error.Conflict(
            "ExchangeRates.Concurrency",
            "تم تعديل سعر الصرف بواسطة مستخدم آخر. أعد تحميل البيانات ثم حاول مرة أخرى.");
}
