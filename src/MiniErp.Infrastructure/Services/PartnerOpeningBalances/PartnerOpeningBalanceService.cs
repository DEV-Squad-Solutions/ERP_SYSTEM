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
    IExchangeRateResolver exchangeRateResolver)
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

        var documentNumberExists = await dbContext.PartnerOpeningBalances.AnyAsync(
            balance =>
                balance.CompanyId == companyId &&
                balance.DocumentNumber == normalized.DocumentNumber,
            cancellationToken);
        if (documentNumberExists)
        {
            return Result<PartnerOpeningBalanceResponse>.Failure(
                DocumentNumberExists(normalized.DocumentNumber));
        }

        normalized.CompanyId = companyId;
        normalized.ApplyExchangeRate(
            exchangeRateResult.Value.ExchangeRateId,
            exchangeRateResult.Value.Rate);
        dbContext.PartnerOpeningBalances.Add(normalized);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ProjectResponseQuery(normalized.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

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
            return Result<PartnerOpeningBalanceResponse>.Failure(
                Error.Validation(
                    "PartnerOpeningBalances.RowVersionRequired",
                    "يجب إرسال إصدار السجل الحالي للتعديل.",
                    nameof(PartnerOpeningBalanceUpdateRequest.RowVersion)));
        }

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

        var documentNumberExists = await dbContext.PartnerOpeningBalances.AnyAsync(
            balance =>
                balance.CompanyId == companyId &&
                balance.Id != id &&
                balance.DocumentNumber == normalized.DocumentNumber,
            cancellationToken);
        if (documentNumberExists)
        {
            return Result<PartnerOpeningBalanceResponse>.Failure(
                DocumentNumberExists(normalized.DocumentNumber));
        }

        openingBalance.BusinessPartnerId = normalized.BusinessPartnerId;
        openingBalance.DocumentNumber = normalized.DocumentNumber;
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
            return Error.NotFound(
                "PartnerOpeningBalances.BusinessPartnerNotFound",
                $"لم يتم العثور على العميل أو المورد رقم {businessPartnerId}.",
                nameof(PartnerOpeningBalanceRequest.BusinessPartnerId));
        }

        if (!partner.IsActive)
        {
            return Error.Conflict(
                "PartnerOpeningBalances.BusinessPartnerInactive",
                "لا يمكن استخدام عميل أو مورد غير نشط.",
                nameof(PartnerOpeningBalanceRequest.BusinessPartnerId));
        }

        return partner.Currency == currency
            ? null
            : Error.Conflict(
                "PartnerOpeningBalances.CurrencyMismatch",
                "يجب أن تطابق عملة رصيد الشريك عملة العميل أو المورد.",
                nameof(PartnerOpeningBalanceRequest.Currency));
    }

    private static Error InvalidId() =>
        Error.Validation(
            "PartnerOpeningBalances.InvalidId",
            "يجب أن يكون رقم رصيد الشريك أكبر من صفر.");

    private static Error NotFound(int id) =>
        Error.NotFound(
            "PartnerOpeningBalances.NotFound",
            $"لم يتم العثور على رصيد الشريك رقم {id}.");

    private static Error DocumentNumberExists(string number) =>
        Error.Conflict(
            "PartnerOpeningBalances.DocumentNumberExists",
            $"رقم المستند '{number}' مستخدم بالفعل.",
            nameof(PartnerOpeningBalanceRequest.DocumentNumber));

    private static Error Concurrency() =>
        Error.Conflict(
            "PartnerOpeningBalances.Concurrency",
            "تم تعديل رصيد الشريك بواسطة عملية أخرى. أعد تحميل المستند ثم حاول مرة أخرى.");
}
