using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.CashboxTransfers;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.Cashboxes;
using static MiniErp.Application.Features.CashboxTransfers.CashboxTransferErrors;

namespace MiniErp.Infrastructure.Services.CashboxTransfers;

public sealed partial class CashboxTransferService
{
    public async Task<Result<PagedResponse<CashboxTransferListResponse>>>
        GetAllAsync(
            PaginationRequest pagination,
            CashboxTransferFilterRequest filters,
            CancellationToken cancellationToken = default)
    {
        var access = await CashboxPermissionGuard.ScopeAsync(cashboxAccessService, cancellationToken);
        if (access.IsFailure) return Result<PagedResponse<CashboxTransferListResponse>>.Failure(access.Errors);
        var scope = access.Value;
        foreach (var cashboxId in new[] { filters.SourceCashboxId, filters.DestinationCashboxId })
        {
            if (cashboxId.HasValue)
            {
                var permission = await CashboxPermissionGuard.ViewAsync(cashboxAccessService, cashboxId.Value, cancellationToken);
                if (permission.IsFailure) return Result<PagedResponse<CashboxTransferListResponse>>.Failure(permission.Errors);
            }
        }
        if (ValidateFilters(filters) is { } filterError)
        {
            return Result<PagedResponse<CashboxTransferListResponse>>.Failure(
                filterError);
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: filters.FiscalYearId,
            fromDate: filters.FromDate,
            toDate: filters.ToDate,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<PagedResponse<CashboxTransferListResponse>>.Failure(
                fiscalYear.Errors);
        }

        var search = filters.Search?.Trim();
        var query = dbContext.CashboxTransfers
            .AsNoTracking()
            .Where(transfer => scope.HasAllCashboxes ||
                (scope.ViewCashboxIds.Contains(transfer.SourceCashboxId) &&
                 scope.ViewCashboxIds.Contains(transfer.DestinationCashboxId)))
            .Where(transfer =>
                transfer.CompanyId == companyId &&
                transfer.FiscalYearId == fiscalYear.Value.FiscalYearId)
            .Where(transfer =>
                string.IsNullOrEmpty(search) ||
                transfer.TransferNumber.Contains(search) ||
                transfer.SourceCashbox.Name.Contains(search) ||
                transfer.DestinationCashbox.Name.Contains(search) ||
                (transfer.Description != null &&
                 transfer.Description.Contains(search)))
            .Where(transfer =>
                !filters.SourceCashboxId.HasValue ||
                transfer.SourceCashboxId == filters.SourceCashboxId.Value)
            .Where(transfer =>
                !filters.DestinationCashboxId.HasValue ||
                transfer.DestinationCashboxId ==
                    filters.DestinationCashboxId.Value)
            .Where(transfer =>
                !filters.FromDate.HasValue ||
                transfer.TransferDate >= filters.FromDate.Value)
            .Where(transfer =>
                !filters.ToDate.HasValue ||
                transfer.TransferDate <= filters.ToDate.Value);
        var orderedQuery = query
            .OrderByDescending(transfer => transfer.TransferDate)
            .ThenByDescending(transfer => transfer.Id);

        return await paginationService.PaginateAsync<
            CashboxTransfer,
            CashboxTransferListResponse>(
                orderedQuery,
                pagination,
                cancellationToken);
    }

    public async Task<Result<CashboxTransferResponse>> GetByIdAsync(
        int id,
        int? fiscalYearId = null,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashboxTransferResponse>.Failure(InvalidId());
        }

        var fiscalYear = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: fiscalYearId,
            cancellationToken: cancellationToken);
        if (fiscalYear.IsFailure)
        {
            return Result<CashboxTransferResponse>.Failure(fiscalYear.Errors);
        }

        var response = await BuildResponseAsync(
            id,
            cancellationToken,
            fiscalYear.Value.FiscalYearId);
        if (response is not null)
        {
            foreach (var cashboxId in new[] { response.SourceCashboxId, response.DestinationCashboxId })
            {
                var access = await CashboxPermissionGuard.ViewAsync(cashboxAccessService, cashboxId, cancellationToken);
                if (access.IsFailure) return Result<CashboxTransferResponse>.Failure(access.Errors);
            }
        }
        return response is null
            ? Result<CashboxTransferResponse>.Failure(NotFound(id))
            : Result<CashboxTransferResponse>.Success(response);
    }

    public async Task<Result<CashboxTransferResponse>> AddAsync(
        CashboxTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequestShape(
                request.TransferDate,
                request.SourceCashboxId,
                request.DestinationCashboxId,
                request.Amount,
                request.Description,
                request.Notes,
                request.ExchangeRate,
                request.DestinationAmount,
                request.ConversionRate,
                request.DestinationExchangeRate) is { } shapeError)
        {
            return Result<CashboxTransferResponse>.Failure(shapeError);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                request.TransferDate,
                nameof(CashboxTransferRequest.TransferDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashboxTransferResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        var preparation = await PrepareAsync(
            request.TransferDate,
            request.SourceCashboxId,
            request.DestinationCashboxId,
            request.Amount,
            request.ExchangeRate,
            request.DestinationAmount,
            request.ConversionRate,
            request.DestinationExchangeRate,
            currentTransfer: null,
            cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<CashboxTransferResponse>.Failure(
                preparation.Error);
        }


        var now = timeProvider.GetUtcNow().UtcDateTime;
        var transferNumber = await GenerateIdentifierAsync(
            prefix: "TRF",
            dbContext.CashboxTransfers
                .IgnoreQueryFilters()
                .Where(entity => entity.CompanyId == companyId)
                .Select(entity => entity.TransferNumber),
            cancellationToken);
        var paymentVoucherNumber = await GenerateIdentifierAsync(
            prefix: "PAY",
            dbContext.CashVouchers
                .IgnoreQueryFilters()
                .Where(entity => entity.CompanyId == companyId)
                .Select(entity => entity.VoucherNumber),
            cancellationToken);
        var receiptVoucherNumber = await GenerateIdentifierAsync(
            prefix: "RCV",
            dbContext.CashVouchers
                .IgnoreQueryFilters()
                .Where(entity => entity.CompanyId == companyId)
                .Select(entity => entity.VoucherNumber),
            cancellationToken);
        var transfer = new CashboxTransfer
        {
            CompanyId = companyId,
            TransferNumber = transferNumber,
            TransferDate = request.TransferDate,
            SourceCashboxId = request.SourceCashboxId,
            DestinationCashboxId = request.DestinationCashboxId,
            Description = Normalize(request.Description),
            Notes = Normalize(request.Notes)
        };
        transfer.Touch(now);
        dbContext.CashboxTransfers.Add(transfer);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.CashVouchers.AddRange(
            CreateVoucher(
                transfer,
                preparation.Value.SourceCashbox,
                CashDirection.Payment,
                paymentVoucherNumber,
                request.Amount,
                preparation.Value.SourceExchangeRate,
                now),
            CreateVoucher(
                transfer,
                preparation.Value.DestinationCashbox,
                CashDirection.Receipt,
                receiptVoucherNumber,
                preparation.Value.DestinationAmount,
                preparation.Value.DestinationExchangeRate,
                now));
        await dbContext.SaveChangesAsync(cancellationToken);

        if (cashboxTransferPostingService is not null)
        {
            var postingResult = await cashboxTransferPostingService
                .SynchronizeAsync(transfer.Id, cancellationToken);
            if (postingResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<CashboxTransferResponse>.Failure(
                    postingResult.Errors);
            }
        }

        var response = await BuildResponseAsync(
            transfer.Id,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<CashboxTransferResponse>.Success(response!);
    }

    public async Task<Result<CashboxTransferResponse>> UpdateAsync(
        int id,
        CashboxTransferUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashboxTransferResponse>.Failure(InvalidId());
        }

        if (request.RowVersion is not { Length: 8 })
        {
            return Result<CashboxTransferResponse>.Failure(
                RowVersionRequired());
        }

        if (ValidateRequestShape(
                request.TransferDate,
                request.SourceCashboxId,
                request.DestinationCashboxId,
                request.Amount,
                request.Description,
                request.Notes,
                request.ExchangeRate,
                request.DestinationAmount,
                request.ConversionRate,
                request.DestinationExchangeRate) is { } shapeError)
        {
            return Result<CashboxTransferResponse>.Failure(shapeError);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
        var transfer = await dbContext.CashboxTransfers
            .Include(entity => entity.Vouchers)
            .SingleOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == id,
                cancellationToken);
        if (transfer is null)
        {
            return Result<CashboxTransferResponse>.Failure(NotFound(id));
        }

        if (!transfer.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<CashboxTransferResponse>.Failure(Concurrency());
        }

        foreach (var cashboxId in new[] { transfer.SourceCashboxId, transfer.DestinationCashboxId })
        {
            var access = await CashboxPermissionGuard.ExecuteAsync(cashboxAccessService, cashboxId, cancellationToken);
            if (access.IsFailure) return Result<CashboxTransferResponse>.Failure(access.Errors);
        }

        var fiscalYearScope = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: transfer.FiscalYearId,
            fromDate: request.TransferDate,
            cancellationToken: cancellationToken);
        if (fiscalYearScope.IsFailure)
        {
            return Result<CashboxTransferResponse>.Failure(
                fiscalYearScope.Errors);
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                transfer.TransferDate,
                nameof(CashboxTransferRequest.TransferDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashboxTransferResponse>.Failure(
                    fiscalYearResult.Errors);
            }

            fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                request.TransferDate,
                nameof(CashboxTransferUpdateRequest.TransferDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashboxTransferResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        if (!TryGetVoucherPair(
                transfer.Vouchers,
                out var paymentVoucher,
                out var receiptVoucher))
        {
            return Result<CashboxTransferResponse>.Failure(
                InvalidVoucherPair());
        }

        var preparation = await PrepareAsync(
            request.TransferDate,
            request.SourceCashboxId,
            request.DestinationCashboxId,
            request.Amount,
            request.ExchangeRate,
            request.DestinationAmount,
            request.ConversionRate,
            request.DestinationExchangeRate,
            transfer,
            cancellationToken);
        if (preparation.IsFailure)
        {
            return Result<CashboxTransferResponse>.Failure(
                preparation.Error);
        }


        var entry = dbContext.Entry(transfer);
        entry.Property(entity => entity.RowVersion).OriginalValue =
            request.RowVersion;
        transfer.TransferDate = request.TransferDate;
        transfer.SourceCashboxId = request.SourceCashboxId;
        transfer.DestinationCashboxId = request.DestinationCashboxId;
        transfer.Description = Normalize(request.Description);
        transfer.Notes = Normalize(request.Notes);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        transfer.Touch(now);
        entry.Property(entity => entity.LastModifiedAt).IsModified = true;

        ApplyVoucher(
            paymentVoucher,
            transfer,
            preparation.Value.SourceCashbox,
            CashDirection.Payment,
            paymentVoucher.VoucherNumber,
            request.Amount,
            preparation.Value.SourceExchangeRate,
            now);
        ApplyVoucher(
            receiptVoucher,
            transfer,
            preparation.Value.DestinationCashbox,
            CashDirection.Receipt,
            receiptVoucher.VoucherNumber,
            preparation.Value.DestinationAmount,
            preparation.Value.DestinationExchangeRate,
            now);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<CashboxTransferResponse>.Failure(Concurrency());
        }

        if (cashboxTransferPostingService is not null)
        {
            var postingResult = await cashboxTransferPostingService
                .SynchronizeAsync(transfer.Id, cancellationToken);
            if (postingResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<CashboxTransferResponse>.Failure(
                    postingResult.Errors);
            }
        }

        var response = await BuildResponseAsync(id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<CashboxTransferResponse>.Success(response!);
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
        var transfer = await dbContext.CashboxTransfers
            .Include(entity => entity.Vouchers)
            .SingleOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == id,
                cancellationToken);
        if (transfer is null)
        {
            return Result.Failure(NotFound(id));
        }

        foreach (var cashboxId in new[] { transfer.SourceCashboxId, transfer.DestinationCashboxId })
        {
            var access = await CashboxPermissionGuard.ExecuteAsync(cashboxAccessService, cashboxId, cancellationToken);
            if (access.IsFailure) return access;
        }

        var fiscalYearScope = await fiscalYearQueryScopeResolver.ResolveAsync(
            fiscalYearId: transfer.FiscalYearId,
            fromDate: transfer.TransferDate,
            cancellationToken: cancellationToken);
        if (fiscalYearScope.IsFailure)
        {
            return Result.Failure(fiscalYearScope.Errors);
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                transfer.TransferDate,
                nameof(CashboxTransferRequest.TransferDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result.Failure(fiscalYearResult.Errors);
            }
        }

        if (!TryGetVoucherPair(
                transfer.Vouchers,
                out var paymentVoucher,
                out var receiptVoucher))
        {
            return Result.Failure(InvalidVoucherPair());
        }


        try
        {
            dbContext.CashVouchers.RemoveRange(
                paymentVoucher,
                receiptVoucher);
            dbContext.CashboxTransfers.Remove(transfer);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (cashboxTransferPostingService is not null)
            {
                var postingResult = await cashboxTransferPostingService
                    .DeleteAsync(transfer.Id, cancellationToken);
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
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result.Failure(Concurrency());
        }
    }
}
