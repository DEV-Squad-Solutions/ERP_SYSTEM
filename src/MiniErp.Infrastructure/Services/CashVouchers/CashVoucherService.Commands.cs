using System.Data;
using static MiniErp.Application.Features.CashVouchers.CashVoucherErrors;
using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Entities.BusinessPartners;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.Cashboxes;

namespace MiniErp.Infrastructure.Services.CashVouchers;

public sealed partial class CashVoucherService
{
    public async Task<Result<CashVoucherResponse>> AddAsync(
        CashVoucherRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var result = await AddDraftAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return result;
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    // Both single and incomplete bulk creation use the same operational draft
    // semantics. The caller owns the transaction so a bulk batch stays atomic.
    private async Task<Result<CashVoucherResponse>> AddDraftAsync(
        CashVoucherRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PartnerExchangeRate.HasValue)
            return Result<CashVoucherResponse>.Failure(Error.Validation(
                "CashVouchers.PartnerRateRequiresCompletion",
                "حدد سعر صرف الطرف عند توصيف السند وليس عند تسجيل حركة الخزينة الأولية.",
                nameof(request.PartnerExchangeRate)));
        var access = await CashboxPermissionGuard.ExecuteAsync(cashboxAccessService, request.CashboxId, cancellationToken);
        if (access.IsFailure) return Result<CashVoucherResponse>.Failure(access.Errors);
        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                request.VoucherDate,
                nameof(CashVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        var cashbox = await dbContext.Cashboxes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == request.CashboxId,
                cancellationToken);
        if (cashbox is null)
        {
            return Result<CashVoucherResponse>.Failure(
                CashboxNotFound(request.CashboxId));
        }

        if (!cashbox.IsActive)
        {
            return Result<CashVoucherResponse>.Failure(CashboxInactive());
        }

        var prefix = request.Direction == CashDirection.Receipt
            ? "RCV"
            : "PAY";
        var voucherNumber = await EntityIdentifierGenerator
            .GenerateUniqueAsync(
                dbContext,
                prefix,
                companyId,
                dbContext.CashVouchers
                    .IgnoreQueryFilters()
                    .Where(entity => entity.CompanyId == companyId)
                    .Select(entity => entity.VoucherNumber),
                cancellationToken);
        var voucher = new CashVoucher
        {
            CompanyId = companyId,
            VoucherNumber = voucherNumber,
            VoucherDate = request.VoucherDate,
            Direction = request.Direction,
            CashboxId = cashbox.Id,
            PartyType = CashPartyType.None,
            Classification = null,
            AccountId = null,
            CashMovementTypeId = null,
            EmployeeId = null,
            BusinessPartnerId = null,
            DriverId = null,
            DriverTripId = null,
            ExternalPartyName = null,
            Amount = request.Amount,
            ReferenceNumber = NormalizeText(request.ReferenceNumber),
            Description = NormalizeText(request.Description),
            Notes = NormalizeText(request.Notes),
            IsPosted = false
        };
        voucher.InitializeDraft(cashbox.Currency);
        if (request.ExchangeRate.HasValue)
        {
            var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
                currency: cashbox.Currency,
                date: request.VoucherDate,
                requestedRate: request.ExchangeRate,
                cancellationToken: cancellationToken);
            if (exchangeRateResult.IsFailure)
            {
                return Result<CashVoucherResponse>.Failure(exchangeRateResult.Errors);
            }

            voucher.ApplyExchangeRate(
                exchangeRateId: exchangeRateResult.Value.ExchangeRateId,
                exchangeRate: exchangeRateResult.Value.Rate);
        }
        voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);

        dbContext.CashVouchers.Add(voucher);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Drafts are intentionally operational only. Do not create partner /
        // employee movements or a journal entry; completion via PUT performs
        // those side effects atomically.

        var response = await ProjectResponseQuery(voucher.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        return Result<CashVoucherResponse>.Success(response);
    }

    public async Task<Result<CashVoucherBulkResponse>> BulkAsync(
        CashVoucherBulkRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = new CashVoucherBulkRequestValidator()
            .Validate(request);
        if (!validationResult.IsValid)
        {
            return Result<CashVoucherBulkResponse>.Failure(
                validationResult.Errors.Select(error =>
                    Error.Validation(
                        "CashVouchers.BulkValidation",
                        error.ErrorMessage,
                        error.PropertyName)));
        }

        // A bulk request is an independent unit of work. Clearing snapshots from
        // earlier CRUD calls ensures RowVersion checks use the database value.
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var results = new List<CashVoucherBulkItemResponse>();

        for (var index = 0; index < request.Items!.Count; index++)
        {
            var item = request.Items[index];
            Result<CashVoucherBulkItemResponse> itemResult = item switch
            {
                CashVoucherBulkAddItemRequest add => await AddBulkItemAsync(
                    add,
                    cancellationToken),
                CashVoucherBulkUpdateItemRequest update => await UpdateBulkItemAsync(
                    update,
                    cancellationToken),
                CashVoucherBulkDeleteItemRequest delete => await DeleteBulkItemAsync(
                    delete,
                    cancellationToken),
                _ => Result<CashVoucherBulkItemResponse>.Failure(
                    Error.Validation(
                        "CashVouchers.BulkInvalidAction",
                        "نوع العملية المرسل غير صحيح."))
            };

            if (itemResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<CashVoucherBulkResponse>.Failure(
                    itemResult.Errors.Select(error =>
                        BulkItemFailure(
                            index,
                            error,
                            isVoucherPayloadError:
                                item is CashVoucherBulkAddItemRequest or
                                    CashVoucherBulkUpdateItemRequest)));
            }

            results.Add(itemResult.Value);
        }

        await transaction.CommitAsync(cancellationToken);

        var summary = new CashVoucherBulkSummary(
            Added: results.Count(item => item.Action == CashVoucherBulkAction.Add),
            Updated: results.Count(item => item.Action == CashVoucherBulkAction.Update),
            Deleted: results.Count(item => item.Action == CashVoucherBulkAction.Delete));
        return Result<CashVoucherBulkResponse>.Success(
            new CashVoucherBulkResponse(
                Items: results,
                Summary: summary));
    }

    public async Task<Result<CashVoucherResponse>> UpdateDraftAsync(
        int id,
        CashVoucherDraftUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return Result<CashVoucherResponse>.Failure(InvalidId());
        if (request.RowVersion is not { Length: 8 })
            return Result<CashVoucherResponse>.Failure(RowVersionRequired());
        if (request.ExchangeRate.HasValue &&
            (!ExchangeRateRules.IsValidRate(request.ExchangeRate.Value) ||
             request.ExchangeRate.Value >= 10_000_000_000_000_000m))
            return Result<CashVoucherResponse>.Failure(ExchangeRateErrors.InvalidRate());
        var validation = new CashVoucherDraftUpdateRequestValidator().Validate(request);
        if (!validation.IsValid)
            return Result<CashVoucherResponse>.Failure(validation.Errors.Select(error =>
                Error.Validation($"CashVouchers.{error.PropertyName}", error.ErrorMessage, error.PropertyName)));

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var voucher = await dbContext.CashVouchers.FirstOrDefaultAsync(
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);
        if (voucher is null)
            return Result<CashVoucherResponse>.Failure(NotFound(id));

        // A scoped context may already track this voucher. Check the database
        // snapshot, not the earlier tracked RowVersion, inside the transaction.
        var entry = dbContext.Entry(voucher);
        await entry.ReloadAsync(cancellationToken);
        if (entry.State == EntityState.Detached || voucher.CompanyId != companyId || voucher.IsDeleted)
            return Result<CashVoucherResponse>.Failure(NotFound(id));

        var access = await EnsureVoucherExecuteAsync(voucher, cancellationToken);
        if (access.IsFailure)
            return Result<CashVoucherResponse>.Failure(access.Errors);
        if (voucher.InvoiceId.HasValue)
            return Result<CashVoucherResponse>.Failure(InvoiceGeneratedReadOnly());
        if (voucher.CashboxTransferId.HasValue)
            return Result<CashVoucherResponse>.Failure(TransferGeneratedReadOnly());
        if (voucher.IsPosted)
            return Result<CashVoucherResponse>.Failure(PostedReadOnly());
        if (!voucher.RowVersion.SequenceEqual(request.RowVersion))
            return Result<CashVoucherResponse>.Failure(Concurrency());

        var fiscalYearScope = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: voucher.FiscalYearId,
            fromDate: voucher.VoucherDate,
            cancellationToken: cancellationToken);
        if (fiscalYearScope.IsFailure)
            return Result<CashVoucherResponse>.Failure(fiscalYearScope.Errors);
        fiscalYearScope = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: voucher.FiscalYearId,
            fromDate: request.VoucherDate,
            cancellationToken: cancellationToken);
        if (fiscalYearScope.IsFailure)
            return Result<CashVoucherResponse>.Failure(fiscalYearScope.Errors);
        if (fiscalYearPeriodGuard is not null)
        {
            var openYear = await fiscalYearPeriodGuard.EnsureOpenForFiscalYearAsync(
                voucher.FiscalYearId,
                voucher.VoucherDate,
                nameof(CashVoucherRequest.VoucherDate),
                cancellationToken);
            if (openYear.IsFailure)
                return Result<CashVoucherResponse>.Failure(openYear.Errors);
            openYear = await fiscalYearPeriodGuard.EnsureOpenForFiscalYearAsync(
                voucher.FiscalYearId,
                request.VoucherDate,
                nameof(CashVoucherDraftUpdateRequest.VoucherDate),
                cancellationToken);
            if (openYear.IsFailure)
                return Result<CashVoucherResponse>.Failure(openYear.Errors);
        }
        access = await CashboxPermissionGuard.ExecuteAsync(cashboxAccessService, request.CashboxId, cancellationToken);
        if (access.IsFailure)
            return Result<CashVoucherResponse>.Failure(access.Errors);
        var cashbox = await dbContext.Cashboxes.AsNoTracking().FirstOrDefaultAsync(
            entity => entity.Id == request.CashboxId && entity.CompanyId == companyId, cancellationToken);
        if (cashbox is null)
            return Result<CashVoucherResponse>.Failure(CashboxNotFound(request.CashboxId));
        if (!cashbox.IsActive && cashbox.Id != voucher.CashboxId)
            return Result<CashVoucherResponse>.Failure(CashboxInactive());

        // No daily-rate lookup is required for an unpriced operational draft.
        // Only preserve a captured rate for the same currency and date. Keep
        // its rate id as well, rather than silently detaching a linked snapshot.
        decimal? rate = null;
        int? rateId = null;
        if (request.ExchangeRate.HasValue)
        {
            var resolvedRate = await exchangeRateResolver.ResolveAsync(
                currency: cashbox.Currency,
                date: request.VoucherDate,
                requestedRate: request.ExchangeRate,
                cancellationToken: cancellationToken);
            if (resolvedRate.IsFailure)
                return Result<CashVoucherResponse>.Failure(resolvedRate.Errors);
            rate = resolvedRate.Value.Rate;
            rateId = resolvedRate.Value.ExchangeRateId;
        }
        else if (cashbox.Currency == voucher.Currency && request.VoucherDate == voucher.VoucherDate &&
            (voucher.ExchangeRate != 1m || voucher.BaseAmount != 0m || voucher.ExchangeRateId.HasValue))
        {
            rate = voucher.ExchangeRate;
            rateId = voucher.ExchangeRateId;
        }
        else if (cashbox.Currency == (await dbContext.CompanySettings.AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP))
        {
            rate = 1m;
        }

        // Match SQL decimal(28,8) storage and avoid decimal multiplication
        // overflow before changing the draft snapshot.
        try
        {
            if (rate.HasValue && ExchangeRateRules.ConvertToBase(request.Amount, rate.Value)
                >= 100_000_000_000_000_000_000m)
                return Result<CashVoucherResponse>.Failure(Error.Validation(
                    "CashVouchers.BaseAmountOutOfRange",
                    "المقابل بالعملة الأساسية أكبر من الحد المسموح؛ راجع سعر الصرف.",
                    nameof(request.ExchangeRate)));
        }
        catch (OverflowException)
        {
            return Result<CashVoucherResponse>.Failure(Error.Validation(
                "CashVouchers.BaseAmountOutOfRange",
                "المقابل بالعملة الأساسية أكبر من الحد المسموح؛ راجع سعر الصرف.",
                nameof(request.ExchangeRate)));
        }

        entry.Property(entity => entity.RowVersion).OriginalValue = request.RowVersion;
        voucher.VoucherDate = request.VoucherDate;
        voucher.Direction = request.Direction;
        voucher.CashboxId = cashbox.Id;
        voucher.Amount = request.Amount;
        voucher.ReferenceNumber = NormalizeText(request.ReferenceNumber);
        voucher.Description = NormalizeText(request.Description);
        voucher.Notes = NormalizeText(request.Notes);
        voucher.InitializeDraft(cashbox.Currency);
        if (rate.HasValue)
            voucher.ApplyExchangeRate(exchangeRateId: rateId, exchangeRate: rate.Value);
        voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);
        entry.Property(entity => entity.LastModifiedAt).IsModified = true;
        try
        {
            // Draft-only snapshot: deliberately no movements or ledger posting.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<CashVoucherResponse>.Failure(Concurrency());
        }
        // Keep the tracked revision aligned with trigger-generated versions,
        // too, for callers that complete the draft in the same scoped context.
        await entry.ReloadAsync(cancellationToken);
        var response = await ProjectResponseQuery(id, voucher.FiscalYearId)
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<CashVoucherResponse>.Success(response);
    }

    public async Task<Result<CashVoucherResponse>> UpdateAsync(
        int id,
        CashVoucherUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashVoucherResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<CashVoucherResponse>.Failure(
                RowVersionRequired());
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var voucher = await dbContext.CashVouchers.FirstOrDefaultAsync(
            entity =>
                entity.Id == id &&
                entity.CompanyId == companyId,
            cancellationToken);
        if (voucher is null)
        {
            return Result<CashVoucherResponse>.Failure(NotFound(id));
        }

        var entry = dbContext.Entry(voucher);
        await entry.ReloadAsync(cancellationToken);
        if (entry.State == EntityState.Detached || voucher.CompanyId != companyId || voucher.IsDeleted)
            return Result<CashVoucherResponse>.Failure(NotFound(id));
        var access = await EnsureVoucherExecuteAsync(voucher, cancellationToken);
        if (access.IsFailure) return Result<CashVoucherResponse>.Failure(access.Errors);
        if (voucher.InvoiceId.HasValue)
            return Result<CashVoucherResponse>.Failure(InvoiceGeneratedReadOnly());
        if (voucher.CashboxTransferId.HasValue)
            return Result<CashVoucherResponse>.Failure(TransferGeneratedReadOnly());
        if (!voucher.RowVersion.SequenceEqual(request.RowVersion))
            return Result<CashVoucherResponse>.Failure(Concurrency());

        var fiscalYearScope = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: voucher.FiscalYearId,
            fromDate: request.VoucherDate,
            cancellationToken: cancellationToken);
        if (fiscalYearScope.IsFailure)
        {
            return Result<CashVoucherResponse>.Failure(
                fiscalYearScope.Errors);
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenForFiscalYearAsync(
                voucher.FiscalYearId,
                voucher.VoucherDate,
                nameof(CashVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherResponse>.Failure(
                    fiscalYearResult.Errors);
            }

            fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenForFiscalYearAsync(
                voucher.FiscalYearId,
                request.VoucherDate,
                nameof(CashVoucherUpdateRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        request = await NormalizeEmployeeMovementTypeAsync(
            request,
            voucher,
            cancellationToken);

        var preparation = await PrepareAsync(
            request,
            voucher,
            enforceManualPostingTarget: true,
            cancellationToken: cancellationToken);
        if (preparation.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CashVoucherResponse>.Failure(preparation.Error);
        }

        entry.Property(entity => entity.RowVersion).OriginalValue =
            request.RowVersion!;

        var originalPartnerReferences = (
            voucher.BusinessPartnerId, voucher.CashMovementTypeId, voucher.InvoiceId);
        request.Adapt(voucher);
        voucher.PartyType = preparation.Value.PartyType;
        voucher.Classification = preparation.Value.Classification;
        voucher.IsPosted = true;
        await ApplyPreparationAsync(
            voucher,
            preparation.Value,
            cancellationToken);
        voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);
        entry.Property(entity => entity.LastModifiedAt).IsModified = true;

        var partnerReferencesChanged = originalPartnerReferences != (
            voucher.BusinessPartnerId, voucher.CashMovementTypeId, voucher.InvoiceId);

        try
        {
            await SynchronizePartnerMovementAsync(
                voucher,
                preparation.Value.BusinessPartner is not null,
                cancellationToken);
            await SynchronizeEmployeeMovementAsync(
                voucher,
                preparation.Value.PartyType == CashPartyType.Employee,
                request.EmployeeMovementType,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var postingResult = await cashVoucherPostingService
                .SynchronizeAsync(
                    voucher,
                    partnerReferencesChanged: partnerReferencesChanged,
                    cancellationToken: cancellationToken);
            if (postingResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<CashVoucherResponse>.Failure(
                    postingResult.Errors);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<CashVoucherResponse>.Failure(Concurrency());
        }

        await entry.ReloadAsync(cancellationToken);
        var response = await ProjectResponseQuery(id, voucher.FiscalYearId)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<CashVoucherResponse>.Success(response);
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return Result.Failure(InvalidId());

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var voucher = await dbContext.CashVouchers.FirstOrDefaultAsync(
            entity => entity.Id == id && entity.CompanyId == companyId,
            cancellationToken);
        if (voucher is null)
            return Result.Failure(NotFound(id));

        var entry = dbContext.Entry(voucher);
        await entry.ReloadAsync(cancellationToken);
        if (entry.State == EntityState.Detached || voucher.CompanyId != companyId || voucher.IsDeleted)
            return Result.Failure(NotFound(id));

        var access = await EnsureVoucherExecuteAsync(voucher, cancellationToken);
        if (access.IsFailure) return access;
        if (voucher.InvoiceId.HasValue)
            return Result.Failure(InvoiceGeneratedReadOnly());
        if (voucher.CashboxTransferId.HasValue)
            return Result.Failure(TransferGeneratedReadOnly());

        // الحركة/السند المستقل مسموح بحذفه للأدمن أو المحاسب حتى بعد
        // التوصيف/الترحيل، بشرط إن السنة المالية مفتوحة عشان نقدر نعكس
        // القيد المحاسبي المرتبط.
        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard
                .EnsureOpenForFiscalYearAsync(
                    voucher.FiscalYearId,
                    voucher.VoucherDate,
                    nameof(CashVoucher.VoucherDate),
                    cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(fiscalYearResult.Errors);
            }
        }

        // إزالة حركات الطرف/الموظف المرتبطة بالسند قبل حذفه.
        await SynchronizePartnerMovementAsync(
            voucher,
            shouldExist: false,
            cancellationToken);
        await SynchronizeEmployeeMovementAsync(
            voucher,
            shouldExist: false,
            movementType: null,
            cancellationToken);

        voucher.IsDeleted = true;
        voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);
        entry.Property(entity => entity.LastModifiedAt).IsModified = true;

        try
        {
            if (voucher.IsPosted)
            {
                // عكس/حذف القيد المحاسبي التلقائي المرتبط بالسند.
                var postingResult = await cashVoucherPostingService
                    .DeleteAsync(voucher.Id, cancellationToken);
                if (postingResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return postingResult;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }

        return Result.Success();
    }
}
