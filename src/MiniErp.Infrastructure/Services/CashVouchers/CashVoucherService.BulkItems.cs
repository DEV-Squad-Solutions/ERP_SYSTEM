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
    private async Task<Result<CashVoucherBulkItemResponse>> AddBulkItemAsync(
        CashVoucherBulkAddItemRequest item,
        CancellationToken cancellationToken)
    {
        var request = ToUpdateRequest(item.Voucher!, rowVersion: null);
        if (!HasExactlyOnePostingTarget(request))
        {
            var draft = await AddDraftAsync(new CashVoucherRequest(
                VoucherDate: request.VoucherDate,
                Direction: request.Direction,
                CashboxId: request.CashboxId!.Value,
                Amount: request.Amount,
                Description: request.Description,
                ReferenceNumber: request.ReferenceNumber,
                Notes: request.Notes,
                ExchangeRate: request.ExchangeRate), cancellationToken);
            return draft.IsFailure
                ? Result<CashVoucherBulkItemResponse>.Failure(draft.Errors)
                : Result<CashVoucherBulkItemResponse>.Success(new CashVoucherBulkItemResponse(
                    Action: CashVoucherBulkAction.Add,
                    Status: "Added",
                    Id: draft.Value.Id,
                    Voucher: draft.Value));
        }
        request = await NormalizeEmployeeMovementTypeAsync(
            request,
            currentVoucher: null,
            cancellationToken: cancellationToken);
        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                request.VoucherDate,
                nameof(CashVoucherBulkVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherBulkItemResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        var preparation = await PrepareAsync(
            request,
            currentVoucher: null,
            enforceManualPostingTarget: true,
            cancellationToken: cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                preparation.Errors);
        }

        var voucher = new CashVoucher
        {
            CompanyId = companyId,
            VoucherNumber = await GenerateVoucherNumberAsync(
                request.Direction,
                cancellationToken)
        };
        request.Adapt(voucher);
        voucher.PartyType = preparation.Value.PartyType;
        voucher.Classification = preparation.Value.Classification;
        voucher.IsPosted = true;
        await ApplyPreparationAsync(voucher, preparation.Value, cancellationToken);
        voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);

        dbContext.CashVouchers.Add(voucher);
        await dbContext.SaveChangesAsync(cancellationToken);
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
            .SynchronizeAsync(voucher, cancellationToken);
        if (postingResult.IsFailure)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                postingResult.Errors);
        }

        var response = await ProjectResponseQuery(voucher.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        return Result<CashVoucherBulkItemResponse>.Success(
            new CashVoucherBulkItemResponse(
                Action: CashVoucherBulkAction.Add,
                Status: "Added",
                Id: voucher.Id,
                Voucher: response));
    }

    private async Task<Result<CashVoucherBulkItemResponse>> UpdateBulkItemAsync(
        CashVoucherBulkUpdateItemRequest item,
        CancellationToken cancellationToken)
    {
        var id = item.Id;
        var voucher = await dbContext.CashVouchers.FirstOrDefaultAsync(
            entity =>
                entity.Id == id &&
                entity.CompanyId == companyId,
            cancellationToken);
        if (voucher is null)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                NotFound(id));
        }

        var entry = dbContext.Entry(voucher);
        await entry.ReloadAsync(cancellationToken);
        if (entry.State == EntityState.Detached || voucher.CompanyId != companyId || voucher.IsDeleted)
            return Result<CashVoucherBulkItemResponse>.Failure(NotFound(id));
        var access = await EnsureVoucherExecuteAsync(voucher, cancellationToken);
        if (access.IsFailure) return Result<CashVoucherBulkItemResponse>.Failure(access.Errors);
        if (voucher.InvoiceId.HasValue)
            return Result<CashVoucherBulkItemResponse>.Failure(InvoiceGeneratedReadOnly());
        if (voucher.CashboxTransferId.HasValue)
            return Result<CashVoucherBulkItemResponse>.Failure(TransferGeneratedReadOnly());
        if (!voucher.RowVersion.SequenceEqual(item.RowVersion!))
            return Result<CashVoucherBulkItemResponse>.Failure(Concurrency());

        var request = ToUpdateRequest(item.Voucher!, item.RowVersion);
        var fiscalYearScope = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: voucher.FiscalYearId,
            fromDate: request.VoucherDate,
            cancellationToken: cancellationToken);
        if (fiscalYearScope.IsFailure)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                fiscalYearScope.Errors);
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenForFiscalYearAsync(
                voucher.FiscalYearId,
                voucher.VoucherDate,
                nameof(CashVoucherBulkVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherBulkItemResponse>.Failure(
                    fiscalYearResult.Errors);
            }

            fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenForFiscalYearAsync(
                voucher.FiscalYearId,
                request.VoucherDate,
                nameof(CashVoucherBulkVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherBulkItemResponse>.Failure(
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
            return Result<CashVoucherBulkItemResponse>.Failure(
                preparation.Errors);
        }

        entry.Property(entity => entity.RowVersion).OriginalValue =
            item.RowVersion!;
        var originalPartnerReferences = (
            voucher.BusinessPartnerId, voucher.CashMovementTypeId, voucher.InvoiceId);
        request.Adapt(voucher);
        voucher.PartyType = preparation.Value.PartyType;
        voucher.Classification = preparation.Value.Classification;
        voucher.IsPosted = true;
        await ApplyPreparationAsync(voucher, preparation.Value, cancellationToken);
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
                return Result<CashVoucherBulkItemResponse>.Failure(
                    postingResult.Errors);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<CashVoucherBulkItemResponse>.Failure(Concurrency());
        }

        await entry.ReloadAsync(cancellationToken);
        var response = await ProjectResponseQuery(voucher.Id, voucher.FiscalYearId)
            .AsNoTracking()
            .FirstAsync(cancellationToken);
        return Result<CashVoucherBulkItemResponse>.Success(
            new CashVoucherBulkItemResponse(
                Action: CashVoucherBulkAction.Update,
                Status: "Updated",
                Id: voucher.Id,
                Voucher: response));
    }

    private async Task<Result<CashVoucherBulkItemResponse>> DeleteBulkItemAsync(
        CashVoucherBulkDeleteItemRequest item,
        CancellationToken cancellationToken)
    {
        var result = await DeleteAsync(item.Id, cancellationToken);
        return Result<CashVoucherBulkItemResponse>.Failure(result.Errors);
    }
}
