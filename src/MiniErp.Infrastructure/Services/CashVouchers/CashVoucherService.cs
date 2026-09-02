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
using MiniErp.Domain.Entities.Logistics;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.CashVouchers;

public sealed class CashVoucherService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IExchangeRateResolver exchangeRateResolver,
    TimeProvider timeProvider,
    IFiscalYearPeriodGuard? fiscalYearPeriodGuard = null)
    : ICashVoucherService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<CashVoucherResponse>>> GetAllAsync(
        PaginationRequest pagination,
        CashVoucherFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new CashVoucherFilterRequest();
        var search = filters.Search?.Trim();
        var voucherNumber = filters.VoucherNumber?.Trim();

        var query = dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher => voucher.CompanyId == companyId)
            .Where(voucher =>
                string.IsNullOrEmpty(search) ||
                voucher.VoucherNumber.Contains(search) ||
                (voucher.Cashbox != null &&
                 (voucher.Cashbox.Code.Contains(search) ||
                  voucher.Cashbox.Name.Contains(search))) ||
                (voucher.CashMovementType != null &&
                 voucher.CashMovementType.Name.Contains(search)) ||
                (voucher.BusinessPartner != null &&
                 (voucher.BusinessPartner.Code.Contains(search) ||
                  voucher.BusinessPartner.Name.Contains(search))) ||
                (voucher.Employee != null &&
                 (voucher.Employee.Code.Contains(search) ||
                  voucher.Employee.Name.Contains(search))) ||
                (voucher.Driver != null &&
                 (voucher.Driver.Code.Contains(search) ||
                  voucher.Driver.Name.Contains(search))) ||
                (voucher.Account != null &&
                 (voucher.Account.Code.Contains(search) ||
                  voucher.Account.Name.Contains(search))) ||
                (voucher.DriverTrip != null &&
                 voucher.DriverTrip.InvoiceNumber.Contains(search)) ||
                (voucher.Invoice != null &&
                 (voucher.Invoice.InvoiceNumber.Contains(search) ||
                  (voucher.Invoice.PartnerInvoiceNo != null &&
                   voucher.Invoice.PartnerInvoiceNo.Contains(search)))) ||
                (voucher.ExternalPartyName != null &&
                 voucher.ExternalPartyName.Contains(search)) ||
                (voucher.ReferenceNumber != null &&
                 voucher.ReferenceNumber.Contains(search)) ||
                (voucher.Description != null &&
                 voucher.Description.Contains(search)))
            .Where(voucher =>
                string.IsNullOrEmpty(voucherNumber) ||
                voucher.VoucherNumber.Contains(voucherNumber))
            .Where(voucher =>
                !filters.Direction.HasValue ||
                voucher.Direction == filters.Direction.Value)
            .Where(voucher =>
                !filters.CashboxId.HasValue ||
                voucher.CashboxId == filters.CashboxId.Value)
            .Where(voucher =>
                !filters.CashMovementTypeId.HasValue ||
                voucher.CashMovementTypeId ==
                filters.CashMovementTypeId.Value)
            .Where(voucher =>
                !filters.Classification.HasValue ||
                (voucher.CashMovementType != null &&
                 voucher.CashMovementType.Classification ==
                 filters.Classification.Value))
            .Where(voucher =>
                !filters.PartyType.HasValue ||
                voucher.PartyType == filters.PartyType.Value)
            .Where(voucher =>
                !filters.BusinessPartnerId.HasValue ||
                voucher.BusinessPartnerId ==
                filters.BusinessPartnerId.Value)
            .Where(voucher =>
                !filters.DriverId.HasValue ||
                voucher.DriverId == filters.DriverId.Value)
            .Where(voucher =>
                !filters.DriverTripId.HasValue ||
                voucher.DriverTripId == filters.DriverTripId.Value)
            .Where(voucher =>
                !filters.EmployeeId.HasValue ||
                voucher.EmployeeId == filters.EmployeeId.Value)
            .Where(voucher =>
                !filters.AccountId.HasValue ||
                voucher.AccountId == filters.AccountId.Value)
            .Where(voucher =>
                !filters.IsDraft.HasValue ||
                filters.IsDraft.Value ==
                !voucher.IsPosted)
            .Where(voucher =>
                !filters.FromDate.HasValue ||
                voucher.VoucherDate >= filters.FromDate.Value)
            .Where(voucher =>
                !filters.ToDate.HasValue ||
                voucher.VoucherDate <= filters.ToDate.Value)
            .OrderByDescending(voucher => voucher.VoucherDate)
            .ThenByDescending(voucher => voucher.Id);

        return await paginationService.PaginateAsync<
            CashVoucher,
            CashVoucherResponse>(
            query,
            pagination,
            cancellationToken);
    }

    public async Task<Result<CashVoucherResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<CashVoucherResponse>.Failure(InvalidId());
        }

        var response = await ProjectResponseQuery(id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? Result<CashVoucherResponse>.Failure(NotFound(id))
            : Result<CashVoucherResponse>.Success(response);
    }

    public async Task<Result<CashVoucherPartySelectResponse>>
        GetPartySelectAsync(
            CancellationToken cancellationToken = default)
    {
        var businessPartnerRows = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(partner =>
                partner.CompanyId == companyId &&
                partner.IsActive)
            .OrderBy(partner => partner.Name)
            .ThenBy(partner => partner.Id)
            .Select(partner => new
            {
                partner.Id,
                partner.Name
            })
            .ToListAsync(cancellationToken);

        var driverRows = await dbContext.Drivers
            .AsNoTracking()
            .Where(driver =>
                driver.CompanyId == companyId &&
                driver.IsActive)
            .OrderBy(driver => driver.Name)
            .ThenBy(driver => driver.Id)
            .Select(driver => new
            {
                driver.Id,
                driver.Name
            })
            .ToListAsync(cancellationToken);

        var employeeRows = await dbContext.Employees
            .AsNoTracking()
            .Where(employee =>
                employee.CompanyId == companyId &&
                employee.IsActive)
            .OrderBy(employee => employee.Name)
            .ThenBy(employee => employee.Id)
            .Select(employee => new
            {
                employee.Id,
                employee.Name
            })
            .ToListAsync(cancellationToken);

        var accountRows = await dbContext.Accounts
            .AsNoTracking()
            .Where(account =>
                account.CompanyId == companyId &&
                account.IsActive &&
                account.IsPosting &&
                (account.AccountType == AccountType.Expense ||
                 account.AccountType == AccountType.Revenue))
            .OrderBy(account => account.Name)
            .ThenBy(account => account.Id)
            .Select(account => new
            {
                account.Id,
                account.Code,
                account.Name,
                account.AccountType
            })
            .ToListAsync(cancellationToken);

        var response = new CashVoucherPartySelectResponse(
            BusinessPartners: businessPartnerRows
                .Select(partner => new SelectResponse(
                    Id: partner.Id,
                    Name: partner.Name))
                .ToList(),
            Drivers: driverRows
                .Select(driver => new SelectResponse(
                    Id: driver.Id,
                    Name: driver.Name))
                .ToList(),
            Employees: employeeRows
                .Select(employee => new SelectResponse(
                    Id: employee.Id,
                    Name: employee.Name))
                .ToList(),
            Expenses: accountRows
                .Where(account => account.AccountType == AccountType.Expense)
                .Select(account =>
                    new CashVoucherAccountSelectResponse(
                        Id: account.Id,
                        Name: account.Name,
                        Classification: CashMovementClassification.Expense,
                        Code: account.Code,
                        AccountType: account.AccountType))
                .ToList(),
            Revenues: accountRows
                .Where(account => account.AccountType == AccountType.Revenue)
                .Select(account =>
                    new CashVoucherAccountSelectResponse(
                        Id: account.Id,
                        Name: account.Name,
                        Classification: CashMovementClassification.Revenue,
                        Code: account.Code,
                        AccountType: account.AccountType))
                .ToList());

        return Result<CashVoucherPartySelectResponse>.Success(response);
    }

    public async Task<Result<CashVoucherResponse>> AddAsync(
        CashVoucherRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

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
            await transaction.RollbackAsync(cancellationToken);
            return Result<CashVoucherResponse>.Failure(
                CashboxNotFound(request.CashboxId));
        }

        if (!cashbox.IsActive)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CashVoucherResponse>.Failure(CashboxInactive());
        }

        var voucher = request.Adapt<CashVoucher>();
        voucher.CompanyId = companyId;
        var prefix = request.Direction == CashDirection.Receipt
            ? "RCV"
            : "PAY";
        voucher.VoucherNumber = await EntityIdentifierGenerator
            .GenerateUniqueAsync(
                dbContext,
                prefix,
                companyId,
                dbContext.CashVouchers
                    .IgnoreQueryFilters()
                    .Where(entity => entity.CompanyId == companyId)
                    .Select(entity => entity.VoucherNumber),
                cancellationToken);
        voucher.CashMovementTypeId = null;
        voucher.PartyType = CashPartyType.None;
        voucher.IsPosted = false;
        voucher.InitializeDraft(cashbox.Currency);
        voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);

        dbContext.CashVouchers.Add(voucher);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await ProjectResponseQuery(voucher.Id)
            .AsNoTracking()
            .FirstAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
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

        if (!voucher.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Result<CashVoucherResponse>.Failure(Concurrency());
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                voucher.VoucherDate,
                nameof(CashVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherResponse>.Failure(
                    fiscalYearResult.Errors);
            }

            fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                request.VoucherDate,
                nameof(CashVoucherUpdateRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        if (voucher.InvoiceId.HasValue)
        {
            return Result<CashVoucherResponse>.Failure(
                InvoiceGeneratedReadOnly());
        }

        if (voucher.CashboxTransferId.HasValue)
        {
            return Result<CashVoucherResponse>.Failure(
                TransferGeneratedReadOnly());
        }

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

        var entry = dbContext.Entry(voucher);
        entry.Property(entity => entity.RowVersion).OriginalValue =
            request.RowVersion;

        request.Adapt(voucher);
        voucher.PartyType = preparation.Value.PartyType;
        voucher.IsPosted = true;
        await ApplyPreparationAsync(
            voucher,
            preparation.Value,
            cancellationToken);
        voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);
        entry.Property(entity => entity.LastModifiedAt).IsModified = true;

        try
        {
            await SynchronizePartnerMovementAsync(
                voucher,
                preparation.Value.BusinessPartner is not null,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return Result<CashVoucherResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(id)
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
        {
            return Result.Failure(InvalidId());
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
            return Result.Failure(NotFound(id));
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                voucher.VoucherDate,
                nameof(CashVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result.Failure(fiscalYearResult.Errors);
            }
        }

        if (voucher.InvoiceId.HasValue)
        {
            return Result.Failure(InvoiceGeneratedReadOnly());
        }

        if (voucher.CashboxTransferId.HasValue)
        {
            return Result.Failure(TransferGeneratedReadOnly());
        }

        var balanceError = await ValidateFinalBalancesAsync(
            voucher,
            proposedCashboxId: null,
            proposedDirection: null,
            proposedAmount: null,
            cancellationToken);
        if (balanceError is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(balanceError);
        }

        try
        {
            var partnerMovements = await dbContext.BusinessPartnerMovements
                .Where(movement =>
                    movement.CompanyId == companyId &&
                    movement.CashVoucherId == id)
                .ToListAsync(cancellationToken);
            dbContext.BusinessPartnerMovements.RemoveRange(partnerMovements);
            dbContext.CashVouchers.Remove(voucher);
            await dbContext.SaveChangesAsync(cancellationToken);
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

    private async Task<Result<CashVoucherBulkItemResponse>> AddBulkItemAsync(
        CashVoucherBulkAddItemRequest item,
        CancellationToken cancellationToken)
    {
        var request = ToUpdateRequest(item.Voucher!, rowVersion: null);
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
        voucher.IsPosted = true;
        await ApplyPreparationAsync(voucher, preparation.Value, cancellationToken);
        voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);

        dbContext.CashVouchers.Add(voucher);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SynchronizePartnerMovementAsync(
            voucher,
            preparation.Value.BusinessPartner is not null,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

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

        if (!voucher.RowVersion.SequenceEqual(item.RowVersion!))
        {
            return Result<CashVoucherBulkItemResponse>.Failure(Concurrency());
        }

        var request = ToUpdateRequest(item.Voucher!, item.RowVersion);
        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                voucher.VoucherDate,
                nameof(CashVoucherBulkVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherBulkItemResponse>.Failure(
                    fiscalYearResult.Errors);
            }

            fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                request.VoucherDate,
                nameof(CashVoucherBulkVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherBulkItemResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        if (voucher.InvoiceId.HasValue)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                InvoiceGeneratedReadOnly());
        }

        if (voucher.CashboxTransferId.HasValue)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                TransferGeneratedReadOnly());
        }

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

        var entry = dbContext.Entry(voucher);
        entry.Property(entity => entity.RowVersion).OriginalValue =
            item.RowVersion!;
        request.Adapt(voucher);
        voucher.PartyType = preparation.Value.PartyType;
        voucher.IsPosted = true;
        await ApplyPreparationAsync(voucher, preparation.Value, cancellationToken);
        voucher.Touch(timeProvider.GetUtcNow().UtcDateTime);
        entry.Property(entity => entity.LastModifiedAt).IsModified = true;

        try
        {
            await SynchronizePartnerMovementAsync(
                voucher,
                preparation.Value.BusinessPartner is not null,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<CashVoucherBulkItemResponse>.Failure(Concurrency());
        }

        var response = await ProjectResponseQuery(voucher.Id)
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

        if (!voucher.RowVersion.SequenceEqual(item.RowVersion!))
        {
            return Result<CashVoucherBulkItemResponse>.Failure(Concurrency());
        }

        if (fiscalYearPeriodGuard is not null)
        {
            var fiscalYearResult = await fiscalYearPeriodGuard.EnsureOpenAsync(
                voucher.VoucherDate,
                nameof(CashVoucherBulkVoucherRequest.VoucherDate),
                cancellationToken);
            if (fiscalYearResult.IsFailure)
            {
                return Result<CashVoucherBulkItemResponse>.Failure(
                    fiscalYearResult.Errors);
            }
        }

        if (voucher.InvoiceId.HasValue)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                InvoiceGeneratedReadOnly());
        }

        if (voucher.CashboxTransferId.HasValue)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(
                TransferGeneratedReadOnly());
        }

        var balanceError = await ValidateFinalBalancesAsync(
            voucher,
            proposedCashboxId: null,
            proposedDirection: null,
            proposedAmount: null,
            cancellationToken);
        if (balanceError is not null)
        {
            return Result<CashVoucherBulkItemResponse>.Failure(balanceError);
        }

        var entry = dbContext.Entry(voucher);
        entry.Property(entity => entity.RowVersion).OriginalValue =
            item.RowVersion!;

        try
        {
            var partnerMovements = await dbContext.BusinessPartnerMovements
                .Where(movement =>
                    movement.CompanyId == companyId &&
                    movement.CashVoucherId == voucher.Id)
                .ToListAsync(cancellationToken);
            dbContext.BusinessPartnerMovements.RemoveRange(partnerMovements);
            dbContext.CashVouchers.Remove(voucher);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Result<CashVoucherBulkItemResponse>.Failure(Concurrency());
        }

        return Result<CashVoucherBulkItemResponse>.Success(
            new CashVoucherBulkItemResponse(
                Action: CashVoucherBulkAction.Delete,
                Status: "Deleted",
                Id: voucher.Id,
                Voucher: null));
    }

    private async Task<string> GenerateVoucherNumberAsync(
        CashDirection direction,
        CancellationToken cancellationToken)
    {
        var prefix = direction == CashDirection.Receipt ? "RCV" : "PAY";
        return await EntityIdentifierGenerator.GenerateUniqueAsync(
            dbContext,
            prefix,
            companyId,
            dbContext.CashVouchers
                .IgnoreQueryFilters()
                .Where(entity => entity.CompanyId == companyId)
                .Select(entity => entity.VoucherNumber),
            cancellationToken);
    }

    private static CashVoucherUpdateRequest ToUpdateRequest(
        CashVoucherBulkVoucherRequest request,
        byte[]? rowVersion) =>
        new(
            VoucherDate: request.VoucherDate,
            Direction: request.Direction,
            CashboxId: request.CashboxId,
            CashMovementTypeId: request.CashMovementTypeId,
            EmployeeId: request.EmployeeId,
            BusinessPartnerId: request.BusinessPartnerId,
            DriverId: request.DriverId,
            DriverTripId: request.DriverTripId,
            ExternalPartyName: request.ExternalPartyName,
            Amount: request.Amount,
            ReferenceNumber: request.ReferenceNumber,
            Description: request.Description,
            Notes: request.Notes,
            RowVersion: rowVersion,
            ExchangeRate: request.ExchangeRate,
            AccountId: request.AccountId);

    private async Task<Result<VoucherPreparation>> PrepareAsync(
        CashVoucherUpdateRequest request,
        CashVoucher? currentVoucher,
        bool enforceManualPostingTarget,
        CancellationToken cancellationToken)
    {
        if (!request.CashboxId.HasValue)
        {
            return Result<VoucherPreparation>.Failure(
                PostingReferencesMustBeTogether());
        }

        var cashboxId = request.CashboxId.Value;
        if (enforceManualPostingTarget &&
            !HasExactlyOnePostingTarget(request))
        {
            return Result<VoucherPreparation>.Failure(
                PartySelectionMustBeExclusive());
        }

        if (!enforceManualPostingTarget && !HasAtMostOneTarget(request))
        {
            return Result<VoucherPreparation>.Failure(
                PartySelectionMustBeExclusive());
        }

        if (request.DriverTripId.HasValue && !request.DriverId.HasValue)
        {
            return Result<VoucherPreparation>.Failure(
                DriverTripRequiresDriver());
        }

        var partyType = DerivePartyType(request);

        var cashbox = await dbContext.Cashboxes
            .FirstOrDefaultAsync(
                entity =>
                    entity.CompanyId == companyId &&
                    entity.Id == cashboxId,
                cancellationToken);
        if (cashbox is null)
        {
            return Result<VoucherPreparation>.Failure(
                CashboxNotFound(cashboxId));
        }

        if (!cashbox.IsActive &&
            (currentVoucher is null ||
             currentVoucher.CashboxId != cashbox.Id))
        {
            return Result<VoucherPreparation>.Failure(CashboxInactive());
        }

        var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
            cashbox.Currency,
            request.VoucherDate,
            request.ExchangeRate,
            cancellationToken);
        if (exchangeRateResult.IsFailure)
        {
            return Result<VoucherPreparation>.Failure(
                exchangeRateResult.Error);
        }

        CashMovementType? movementType = null;
        if (request.CashMovementTypeId is int cashMovementTypeId)
        {
            movementType = await dbContext.CashMovementTypes
                .FirstOrDefaultAsync(
                    entity =>
                        entity.CompanyId == companyId &&
                        entity.Id == cashMovementTypeId,
                    cancellationToken);
            if (movementType is null)
            {
                return Result<VoucherPreparation>.Failure(
                    MovementTypeNotFound(cashMovementTypeId));
            }

            if (!movementType.IsActive &&
                (enforceManualPostingTarget ||
                 currentVoucher is null ||
                 currentVoucher.CashMovementTypeId != movementType.Id))
            {
                return Result<VoucherPreparation>.Failure(
                    MovementTypeInactive());
            }

            if (movementType.Direction != request.Direction)
            {
                return Result<VoucherPreparation>.Failure(
                    MovementTypeDirectionMismatch());
            }

        }

        Account? account = null;
        if (request.AccountId is int accountId)
        {
            account = await dbContext.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    entity =>
                        entity.CompanyId == companyId &&
                        entity.Id == accountId,
                    cancellationToken);
            if (account is null)
            {
                return Result<VoucherPreparation>.Failure(
                    AccountNotFound(accountId));
            }

            if (!account.IsActive || !account.IsPosting)
            {
                return Result<VoucherPreparation>.Failure(
                    AccountInactiveOrNotPosting());
            }

            var accountMatchesDirection =
                (request.Direction == CashDirection.Payment &&
                 account.AccountType == AccountType.Expense) ||
                (request.Direction == CashDirection.Receipt &&
                 account.AccountType == AccountType.Revenue);
            if (!accountMatchesDirection)
            {
                return Result<VoucherPreparation>.Failure(
                    AccountDirectionMismatch());
            }
        }

        BusinessPartner? partner = null;
        if (partyType == CashPartyType.Partner)
        {
            partner = await dbContext.BusinessPartners
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    entity =>
                        entity.CompanyId == companyId &&
                        entity.Id == request.BusinessPartnerId &&
                        entity.IsActive,
                    cancellationToken);
            if (partner is null)
            {
                return Result<VoucherPreparation>.Failure(
                    PartnerNotFound(request.BusinessPartnerId));
            }

            if (partner.Currency != cashbox.Currency)
            {
                return Result<VoucherPreparation>.Failure(
                    PartnerCurrencyMismatch());
            }
        }
        else if (movementType is not null &&
                 movementType.PartnerEffect != PartnerAccountEffect.None)
        {
            return Result<VoucherPreparation>.Failure(
                MovementTypeForPartnerOnly());
        }

        if (partyType == CashPartyType.Employee)
        {
            var employeeExists = await dbContext.Employees
                .AsNoTracking()
                .AnyAsync(
                    entity =>
                        entity.CompanyId == companyId &&
                        entity.Id == request.EmployeeId &&
                        entity.IsActive,
                    cancellationToken);
            if (!employeeExists)
            {
                return Result<VoucherPreparation>.Failure(
                    EmployeeNotFound(request.EmployeeId));
            }
        }

        Driver? driver = null;
        if (partyType == CashPartyType.Driver)
        {
            driver = await dbContext.Drivers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    entity =>
                        entity.CompanyId == companyId &&
                        entity.Id == request.DriverId &&
                        entity.IsActive,
                    cancellationToken);
            if (driver is null)
            {
                return Result<VoucherPreparation>.Failure(
                    DriverNotFound(request.DriverId));
            }

            if (request.DriverTripId.HasValue)
            {
                var tripExists = await dbContext.DriverTrips
                    .AsNoTracking()
                    .AnyAsync(
                        trip =>
                            trip.CompanyId == companyId &&
                            trip.Id == request.DriverTripId.Value &&
                            trip.DriverId == driver.Id,
                        cancellationToken);
                if (!tripExists)
                {
                    return Result<VoucherPreparation>.Failure(
                        DriverTripNotFound(request.DriverTripId.Value));
                }
            }
        }

        var balanceError = await ValidateFinalBalancesAsync(
            currentVoucher,
            proposedCashboxId: cashboxId,
            proposedDirection: request.Direction,
            proposedAmount: request.Amount,
            cancellationToken);
        if (balanceError is not null)
        {
            return Result<VoucherPreparation>.Failure(balanceError);
        }

        return Result<VoucherPreparation>.Success(
            new VoucherPreparation(
                Cashbox: cashbox,
                PartyType: partyType,
                BusinessPartner: partner,
                Driver: driver,
                ExchangeRate: exchangeRateResult.Value));
    }

    private async Task<Error?> ValidateFinalBalancesAsync(
        CashVoucher? currentVoucher,
        int? proposedCashboxId,
        CashDirection? proposedDirection,
        decimal? proposedAmount,
        CancellationToken cancellationToken)
    {
        var affectedCashboxIds = new HashSet<int>();
        if (currentVoucher is
            {
                CashboxId: int currentCashboxId,
                IsPosted: true
            })
        {
            affectedCashboxIds.Add(currentCashboxId);
        }

        if (proposedCashboxId.HasValue)
        {
            affectedCashboxIds.Add(proposedCashboxId.Value);
        }

        foreach (var cashboxId in affectedCashboxIds)
        {
            var excludedVoucherId = currentVoucher?.Id;
            var balance = await dbContext.Cashboxes
                .AsNoTracking()
                .Where(cashbox =>
                    cashbox.CompanyId == companyId &&
                    cashbox.Id == cashboxId)
                .Select(cashbox =>
                    cashbox.OpeningBalance +
                    (cashbox.Vouchers
                        .Where(voucher =>
                            voucher.IsPosted &&
                            (!excludedVoucherId.HasValue ||
                             voucher.Id != excludedVoucherId.Value))
                        .Sum(voucher =>
                            (decimal?)(voucher.Direction ==
                                CashDirection.Receipt
                                ? voucher.Amount
                                : -voucher.Amount)) ?? 0m))
                .SingleAsync(cancellationToken);

            if (proposedCashboxId == cashboxId &&
                proposedDirection.HasValue &&
                proposedAmount.HasValue)
            {
                balance += proposedDirection == CashDirection.Receipt
                    ? proposedAmount.Value
                    : -proposedAmount.Value;
            }

            if (balance < 0m)
            {
                return InsufficientCashboxBalance(cashboxId);
            }
        }

        return null;
    }

    private BusinessPartnerMovement CreatePartnerMovement(
        CashVoucher voucher)
    {
        var debit = voucher.Direction == CashDirection.Payment
            ? voucher.Amount
            : 0m;
        var credit = voucher.Direction == CashDirection.Receipt
            ? voucher.Amount
            : 0m;

        var movement = new BusinessPartnerMovement
        {
            CompanyId = companyId,
            BusinessPartnerId = voucher.BusinessPartnerId!.Value,
            CashVoucherId = voucher.Id,
            CashVoucher = voucher,
            MovementType = voucher.Direction == CashDirection.Receipt
                ? BusinessPartnerMovementType.CashReceipt
                : BusinessPartnerMovementType.CashPayment,
            MovementDate = voucher.VoucherDate,
            Currency = voucher.Currency,
            Debit = debit,
            Credit = credit,
            Description = voucher.Description ??
                $"Cash voucher {voucher.VoucherNumber}"
        };
        movement.ApplyExchangeRate(voucher.ExchangeRate);
        return movement;
    }

    private async Task SynchronizePartnerMovementAsync(
        CashVoucher voucher,
        bool shouldExist,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.BusinessPartnerMovements
            .FirstOrDefaultAsync(
                movement =>
                movement.CompanyId == companyId &&
                movement.CashVoucherId == voucher.Id,
                cancellationToken);

        if (!shouldExist)
        {
            if (existing is not null)
            {
                dbContext.BusinessPartnerMovements.Remove(existing);
            }

            return;
        }

        if (existing is null)
        {
            dbContext.BusinessPartnerMovements.Add(
                CreatePartnerMovement(voucher));
            return;
        }

        existing.BusinessPartnerId = voucher.BusinessPartnerId!.Value;
        existing.MovementType =
            voucher.Direction == CashDirection.Receipt
                ? BusinessPartnerMovementType.CashReceipt
                : BusinessPartnerMovementType.CashPayment;
        existing.MovementDate = voucher.VoucherDate;
        existing.Currency = voucher.Currency;
        existing.Debit = voucher.Direction == CashDirection.Payment
            ? voucher.Amount
            : 0m;
        existing.Credit = voucher.Direction == CashDirection.Receipt
            ? voucher.Amount
            : 0m;
        existing.ApplyExchangeRate(voucher.ExchangeRate);
        existing.Description = voucher.Description ??
            $"Cash voucher {voucher.VoucherNumber}";
    }

    private IQueryable<CashVoucherResponse> ProjectResponseQuery(int id) =>
        dbContext.CashVouchers
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.Id == id)
            .ProjectToType<CashVoucherResponse>();

    private async Task ApplyPreparationAsync(
        CashVoucher voucher,
        VoucherPreparation preparation,
        CancellationToken cancellationToken)
    {
        if (preparation.Cashbox is not null &&
            preparation.ExchangeRate is not null)
        {
            voucher.Currency = preparation.Cashbox.Currency;
            voucher.ApplyExchangeRate(
                preparation.ExchangeRate.ExchangeRateId,
                preparation.ExchangeRate.Rate);
            return;
        }

        voucher.CashboxId = null;
        voucher.CashMovementTypeId = null;
        voucher.AccountId = null;
        voucher.PartyType = CashPartyType.None;
        voucher.EmployeeId = null;
        voucher.BusinessPartnerId = null;
        voucher.DriverId = null;
        voucher.DriverTripId = null;
        voucher.ExternalPartyName = null;

        voucher.Currency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;
        voucher.ApplyExchangeRate(exchangeRateId: null, exchangeRate: 1m);
    }

    private sealed record VoucherPreparation(
        Cashbox? Cashbox,
        CashPartyType PartyType,
        BusinessPartner? BusinessPartner,
        Driver? Driver,
        ResolvedExchangeRate? ExchangeRate);

    private static CashPartyType DerivePartyType(
        CashVoucherUpdateRequest request) =>
        request.EmployeeId.HasValue
            ? CashPartyType.Employee
            : request.BusinessPartnerId.HasValue
                ? CashPartyType.Partner
                : request.DriverId.HasValue
                    ? CashPartyType.Driver
                    : !string.IsNullOrWhiteSpace(request.ExternalPartyName)
                        ? CashPartyType.Other
                        : CashPartyType.None;

    private static bool HasAtMostOneTarget(CashVoucherUpdateRequest request)
    {
        var selectedPartyCount =
            (request.AccountId.HasValue ? 1 : 0) +
            (request.EmployeeId.HasValue ? 1 : 0) +
            (request.BusinessPartnerId.HasValue ? 1 : 0) +
            (request.DriverId.HasValue ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(request.ExternalPartyName) ? 1 : 0);

        return selectedPartyCount <= 1;
    }

    private static bool HasExactlyOnePostingTarget(
        CashVoucherUpdateRequest request)
    {
        var selectedTargetCount =
            (request.AccountId.HasValue ? 1 : 0) +
            (request.EmployeeId.HasValue ? 1 : 0) +
            (request.BusinessPartnerId.HasValue ? 1 : 0) +
            (request.DriverId.HasValue ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(request.ExternalPartyName) ? 1 : 0);

        return selectedTargetCount == 1;
    }

}
