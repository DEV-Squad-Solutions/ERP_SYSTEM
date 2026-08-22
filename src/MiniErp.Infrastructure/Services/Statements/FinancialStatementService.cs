using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.Statements.StatementErrors;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.Statements;

public sealed partial class FinancialStatementService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext)
    : IFinancialStatementService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<CashboxStatementResponse>>
        GetCashboxStatementAsync(
            PaginationRequest pagination,
            CashboxStatementFilterRequest filters,
            CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(pagination);
        if (paginationError is not null)
        {
            return Result<CashboxStatementResponse>.Failure(paginationError);
        }

        var cashbox = await dbContext.Cashboxes
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == filters.CashboxId)
            .Select(entity => new
            {
                entity.Id,
                entity.Name,
                entity.Currency,
                entity.OpeningBalance,
                entity.OpeningBalanceDate,
                entity.OpeningExchangeRate,
                entity.BaseOpeningBalance,
                BaseCurrency = entity.Company.Settings == null
                    ? CurrencyCode.EGP
                    : entity.Company.Settings.BaseCurrency
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (cashbox is null)
        {
            return Result<CashboxStatementResponse>.Failure(
                CashboxNotFound(filters.CashboxId));
        }

        var openingBalance = cashbox.OpeningBalance;
        var baseOpeningBalance = cashbox.BaseOpeningBalance;
        if (filters.FromDate.HasValue)
        {
            openingBalance += await dbContext.CashVouchers
                .AsNoTracking()
                .Where(voucher =>
                    voucher.CompanyId == companyId &&
                    voucher.CashboxId == cashbox.Id &&
                    voucher.IsPosted &&
                    voucher.VoucherDate < filters.FromDate.Value)
                .SumAsync(
                    voucher =>
                        (decimal?)(voucher.Direction ==
                            CashDirection.Receipt
                            ? voucher.Amount
                            : -voucher.Amount),
                    cancellationToken) ?? 0m;

            baseOpeningBalance += await dbContext.CashVouchers
                .AsNoTracking()
                .Where(voucher =>
                    voucher.CompanyId == companyId &&
                    voucher.CashboxId == cashbox.Id &&
                    voucher.IsPosted &&
                    voucher.VoucherDate < filters.FromDate.Value)
                .SumAsync(
                    voucher =>
                        (decimal?)(voucher.Direction ==
                            CashDirection.Receipt
                            ? voucher.BaseAmount
                            : -voucher.BaseAmount),
                    cancellationToken) ?? 0m;
        }

        var search = filters.Search?.Trim();
        var voucherNumber = filters.VoucherNumber?.Trim();
        var query = dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.CashboxId == cashbox.Id &&
                voucher.IsPosted)
            .Where(voucher =>
                !filters.FromDate.HasValue ||
                voucher.VoucherDate >= filters.FromDate.Value)
            .Where(voucher =>
                !filters.ToDate.HasValue ||
                voucher.VoucherDate <= filters.ToDate.Value)
            .Where(voucher =>
                !filters.Direction.HasValue ||
                voucher.Direction == filters.Direction.Value)
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
                string.IsNullOrEmpty(voucherNumber) ||
                voucher.VoucherNumber.Contains(voucherNumber))
            .Where(voucher =>
                string.IsNullOrEmpty(search) ||
                voucher.VoucherNumber.Contains(search) ||
                (voucher.CashMovementType != null &&
                 voucher.CashMovementType.Name.Contains(search)) ||
                (voucher.BusinessPartner != null &&
                 voucher.BusinessPartner.Name.Contains(search)) ||
                (voucher.Driver != null &&
                 voucher.Driver.Name.Contains(search)) ||
                (voucher.Employee != null &&
                 (voucher.Employee.Code.Contains(search) ||
                  voucher.Employee.Name.Contains(search))) ||
                (voucher.ExternalPartyName != null &&
                 voucher.ExternalPartyName.Contains(search)) ||
                (voucher.ReferenceNumber != null &&
                 voucher.ReferenceNumber.Contains(search)) ||
                (voucher.Description != null &&
                 voucher.Description.Contains(search)));

        var totalCount = await query.CountAsync(cancellationToken);
        var totals = await query
            .GroupBy(_ => 1)
            .Select(vouchers => new
            {
                Receipts = vouchers.Sum(voucher =>
                    voucher.Direction == CashDirection.Receipt
                        ? voucher.Amount
                        : 0m),
                Payments = vouchers.Sum(voucher =>
                    voucher.Direction == CashDirection.Payment
                        ? voucher.Amount
                        : 0m),
                BaseReceipts = vouchers.Sum(voucher =>
                    voucher.Direction == CashDirection.Receipt
                        ? voucher.BaseAmount
                        : 0m),
                BasePayments = vouchers.Sum(voucher =>
                    voucher.Direction == CashDirection.Payment
                        ? voucher.BaseAmount
                        : 0m)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var totalReceipts = totals?.Receipts ?? 0m;
        var totalPayments = totals?.Payments ?? 0m;
        var totalBaseReceipts = totals?.BaseReceipts ?? 0m;
        var totalBasePayments = totals?.BasePayments ?? 0m;
        var ordered = query
            .OrderBy(voucher => voucher.VoucherDate)
            .ThenBy(voucher => voucher.CreatedOn)
            .ThenBy(voucher => voucher.VoucherNumber)
            .ThenBy(voucher => voucher.Id);
        var offset = GetOffset(pagination, totalCount);

        var precedingEffect = offset == 0
            ? 0m
            : await ordered
                .Take(offset)
                .SumAsync(
                    voucher =>
                        (decimal?)(voucher.Direction ==
                            CashDirection.Receipt
                            ? voucher.Amount
                            : -voucher.Amount),
                    cancellationToken) ?? 0m;

        var precedingBaseEffect = offset == 0
            ? 0m
            : await ordered
                .Take(offset)
                .SumAsync(
                    voucher =>
                        (decimal?)(voucher.Direction ==
                            CashDirection.Receipt
                            ? voucher.BaseAmount
                            : -voucher.BaseAmount),
                    cancellationToken) ?? 0m;

        var pageRows = offset >= totalCount
            ? []
            : await ordered
                .Skip(offset)
                .Take(pagination.PageSize)
                .Select(voucher => new CashboxStatementRaw
                {
                    CashVoucherId = voucher.Id,
                    Date = voucher.VoucherDate,
                    VoucherNumber = voucher.VoucherNumber,
                    MovementName = voucher.CashMovementType != null
                        ? voucher.CashMovementType.Name
                        : voucher.CashboxTransferId.HasValue
                            ? voucher.Direction == CashDirection.Payment
                                ? "تحويل خزائن صادر"
                                : "تحويل خزائن وارد"
                            : voucher.Direction == CashDirection.Payment
                                ? "سند صرف"
                                : "سند قبض",
                    Description = voucher.Description,
                    PartyName = voucher.BusinessPartner != null
                        ? voucher.BusinessPartner.Name
                        : voucher.Driver != null
                            ? voucher.Driver.Name
                            : voucher.Employee != null
                                ? voucher.Employee.Name
                                : voucher.ExternalPartyName,
                    Currency = voucher.Currency,
                    ExchangeRate = voucher.ExchangeRate,
                    ReceiptAmount =
                        voucher.Direction == CashDirection.Receipt
                            ? voucher.Amount
                            : 0m,
                    PaymentAmount =
                        voucher.Direction == CashDirection.Payment
                            ? voucher.Amount
                            : 0m,
                    BaseReceiptAmount =
                        voucher.Direction == CashDirection.Receipt
                            ? voucher.BaseAmount
                            : 0m,
                    BasePaymentAmount =
                        voucher.Direction == CashDirection.Payment
                            ? voucher.BaseAmount
                            : 0m,
                    ReferenceNumber = voucher.ReferenceNumber
                })
                .ToListAsync(cancellationToken);

        var runningBalance = openingBalance + precedingEffect;
        var runningBaseBalance =
            baseOpeningBalance + precedingBaseEffect;
        var items = pageRows.Select(row =>
        {
            runningBalance += row.ReceiptAmount - row.PaymentAmount;
            runningBaseBalance +=
                row.BaseReceiptAmount - row.BasePaymentAmount;
            return new CashboxStatementItemResponse(
                CashVoucherId: row.CashVoucherId,
                Date: row.Date,
                VoucherNumber: row.VoucherNumber,
                MovementName: row.MovementName,
                Description: row.Description,
                PartyName: row.PartyName,
                ReceiptAmount: row.ReceiptAmount,
                PaymentAmount: row.PaymentAmount,
                Balance: runningBalance,
                ReferenceNumber: row.ReferenceNumber)
            {
                Currency = row.Currency,
                BaseCurrency = cashbox.BaseCurrency,
                ExchangeRate = row.ExchangeRate,
                IsBaseCurrency = row.Currency == cashbox.BaseCurrency,
                BaseReceiptAmount = row.BaseReceiptAmount,
                BasePaymentAmount = row.BasePaymentAmount,
                BaseBalance = runningBaseBalance
            };
        }).ToList();

        return Result<CashboxStatementResponse>.Success(
            new CashboxStatementResponse(
                CashboxId: cashbox.Id,
                CashboxName: cashbox.Name,
                Currency: cashbox.Currency,
                Items: items,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: GetTotalPages(totalCount, pagination.PageSize),
                Summary: new CashboxStatementSummaryResponse(
                    OpeningBalance: openingBalance,
                    TotalReceipts: totalReceipts,
                    TotalPayments: totalPayments,
                    ClosingBalance: openingBalance + totalReceipts - totalPayments)
                {
                    BaseOpeningBalance = baseOpeningBalance,
                    BaseTotalReceipts = totalBaseReceipts,
                    BaseTotalPayments = totalBasePayments,
                    BaseClosingBalance =
                        baseOpeningBalance +
                        totalBaseReceipts -
                        totalBasePayments
                })
            {
                BaseCurrency = cashbox.BaseCurrency,
                OpeningBalanceDate = cashbox.OpeningBalanceDate,
                OpeningExchangeRate = cashbox.OpeningExchangeRate,
                IsBaseCurrency = cashbox.Currency == cashbox.BaseCurrency
            });
    }

    public async Task<Result<PartnerStatementResponse>>
        GetPartnerStatementAsync(
            PaginationRequest pagination,
            PartnerStatementFilterRequest filters,
            CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(pagination);
        if (paginationError is not null)
        {
            return Result<PartnerStatementResponse>.Failure(paginationError);
        }

        var partner = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == filters.BusinessPartnerId)
            .Select(entity => new
            {
                entity.Id,
                entity.Name,
                entity.Currency,
                BaseCurrency = entity.Company.Settings == null
                    ? CurrencyCode.EGP
                    : entity.Company.Settings.BaseCurrency
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (partner is null)
        {
            return Result<PartnerStatementResponse>.Failure(
                PartnerNotFound(filters.BusinessPartnerId));
        }

        var allRows = CreatePartnerRows(filters.BusinessPartnerId);
        var openingBalance = filters.FromDate.HasValue
            ? await allRows
                .Where(row => row.Date < filters.FromDate.Value)
                .SumAsync(
                    row => (decimal?)(row.Debit - row.Credit),
                    cancellationToken) ?? 0m
            : 0m;
        var baseOpeningBalance = filters.FromDate.HasValue
            ? await allRows
                .Where(row => row.Date < filters.FromDate.Value)
                .SumAsync(
                    row => (decimal?)
                        (row.BaseDebit - row.BaseCredit),
                    cancellationToken) ?? 0m
            : 0m;

        var search = filters.Search?.Trim();
        var query = allRows
            .Where(row =>
                !filters.FromDate.HasValue ||
                row.Date >= filters.FromDate.Value)
            .Where(row =>
                !filters.ToDate.HasValue ||
                row.Date <= filters.ToDate.Value)
            .Where(row =>
                !filters.SourceType.HasValue ||
                row.SourceType == filters.SourceType.Value)
            .Where(row =>
                !filters.MovementType.HasValue ||
                row.MovementType == filters.MovementType.Value)
            .Where(row =>
                !filters.CashMovementTypeId.HasValue ||
                row.CashMovementTypeId ==
                filters.CashMovementTypeId.Value)
            .Where(row =>
                !filters.Classification.HasValue ||
                row.Classification == filters.Classification.Value)
            .Where(row =>
                string.IsNullOrEmpty(search) ||
                row.DocumentNumber.Contains(search) ||
                (row.Description != null &&
                 row.Description.Contains(search)) ||
                (row.ReferenceNumber != null &&
                 row.ReferenceNumber.Contains(search)));

        var totalCount = await query.CountAsync(cancellationToken);
        var totals = await query
            .GroupBy(_ => 1)
            .Select(rows => new
            {
                Debit = rows.Sum(row => row.Debit),
                Credit = rows.Sum(row => row.Credit),
                BaseDebit = rows.Sum(row => row.BaseDebit),
                BaseCredit = rows.Sum(row => row.BaseCredit)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var totalDebit = totals?.Debit ?? 0m;
        var totalCredit = totals?.Credit ?? 0m;
        var totalBaseDebit = totals?.BaseDebit ?? 0m;
        var totalBaseCredit = totals?.BaseCredit ?? 0m;

        var ordered = query
            .OrderBy(row => row.Date)
            .ThenBy(row => row.CreatedOn)
            .ThenBy(row => row.DocumentNumber)
            .ThenBy(row => row.SourceId);
        var offset = GetOffset(pagination, totalCount);
        var precedingEffect = offset == 0
            ? 0m
            : await ordered
                .Take(offset)
                .SumAsync(
                    row => (decimal?)(row.Debit - row.Credit),
                    cancellationToken) ?? 0m;

        var precedingBaseEffect = offset == 0
            ? 0m
            : await ordered
                .Take(offset)
                .SumAsync(
                    row => (decimal?)
                        (row.BaseDebit - row.BaseCredit),
                    cancellationToken) ?? 0m;
        var pageRows = offset >= totalCount
            ? []
            : await ordered
                .Skip(offset)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);

        var runningBalance = openingBalance + precedingEffect;
        var runningBaseBalance =
            baseOpeningBalance + precedingBaseEffect;
        var items = pageRows.Select(row =>
        {
            runningBalance += row.Debit - row.Credit;
            runningBaseBalance += row.BaseDebit - row.BaseCredit;
            return new PartnerStatementItemResponse(
                Date: row.Date,
                DocumentNumber: row.DocumentNumber,
                MovementName: PartnerMovementName(row.MovementType),
                Description: row.Description,
                DebitAmount: row.Debit,
                CreditAmount: row.Credit,
                BalanceAmount: Math.Abs(runningBalance),
                BalanceDescription: PartnerBalanceDescription(runningBalance),
                ReferenceNumber: row.ReferenceNumber)
            {
                ExchangeRate = row.ExchangeRate,
                BaseDebitAmount = row.BaseDebit,
                BaseCreditAmount = row.BaseCredit,
                BaseBalanceAmount = Math.Abs(runningBaseBalance)
            };
        }).ToList();

        var closingBalance =
            openingBalance + totalDebit - totalCredit;
        var baseClosingBalance =
            baseOpeningBalance + totalBaseDebit - totalBaseCredit;
        return Result<PartnerStatementResponse>.Success(
            new PartnerStatementResponse(
                BusinessPartnerId: partner.Id,
                BusinessPartnerName: partner.Name,
                Currency: partner.Currency,
                Items: items,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: GetTotalPages(totalCount, pagination.PageSize),
                Summary: new PartnerStatementSummaryResponse(
                    OpeningBalanceAmount: Math.Abs(openingBalance),
                    OpeningBalanceDescription: PartnerBalanceDescription(openingBalance),
                    ClosingBalanceAmount: Math.Abs(closingBalance),
                    ClosingBalanceDescription: PartnerBalanceDescription(closingBalance))
                {
                    BaseOpeningBalanceAmount =
                        Math.Abs(baseOpeningBalance),
                    BaseClosingBalanceAmount =
                        Math.Abs(baseClosingBalance)
                })
            {
                BaseCurrency = partner.BaseCurrency
            });
    }

    public async Task<Result<DriverStatementResponse>>
        GetDriverStatementAsync(
            PaginationRequest pagination,
            DriverStatementFilterRequest filters,
            CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(pagination);
        if (paginationError is not null)
        {
            return Result<DriverStatementResponse>.Failure(paginationError);
        }

        var driver = await dbContext.Drivers
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == filters.DriverId)
            .Select(entity => new
            {
                entity.Id,
                entity.Name
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (driver is null)
        {
            return Result<DriverStatementResponse>.Failure(
                DriverNotFound(filters.DriverId));
        }

        var allRows = CreateDriverRows(filters.DriverId);
        var openingBalance = filters.FromDate.HasValue
            ? await allRows
                .Where(row => row.Date < filters.FromDate.Value)
                .SumAsync(
                    row => (decimal?)(row.CashPaid -
                        row.CashReceived -
                        row.TripCost),
                    cancellationToken) ?? 0m
            : 0m;

        var search = filters.Search?.Trim();
        var invoiceNumber = filters.InvoiceNumber?.Trim();
        var query = allRows
            .Where(row =>
                !filters.FromDate.HasValue ||
                row.Date >= filters.FromDate.Value)
            .Where(row =>
                !filters.ToDate.HasValue ||
                row.Date <= filters.ToDate.Value)
            .Where(row =>
                !filters.Direction.HasValue ||
                row.Direction == filters.Direction.Value)
            .Where(row =>
                !filters.CashMovementTypeId.HasValue ||
                row.CashMovementTypeId ==
                filters.CashMovementTypeId.Value)
            .Where(row =>
                !filters.Classification.HasValue ||
                row.Classification == filters.Classification.Value)
            .Where(row =>
                !filters.DriverTripId.HasValue ||
                row.DriverTripId == filters.DriverTripId.Value)
            .Where(row =>
                string.IsNullOrEmpty(invoiceNumber) ||
                (row.InvoiceNumber != null &&
                 row.InvoiceNumber.Contains(invoiceNumber)))
            .Where(row =>
                !filters.TransactionsWithoutTrip.HasValue ||
                (filters.TransactionsWithoutTrip.Value
                    ? row.SourceType ==
                      DriverStatementSourceType.CashVoucher &&
                      row.DriverTripId == null
                    : row.DriverTripId != null))
            .Where(row =>
                !filters.HasCost.HasValue ||
                (row.SourceType == DriverStatementSourceType.DriverTrip &&
                 (row.TripCost > 0m) == filters.HasCost.Value))
            .Where(row =>
                string.IsNullOrEmpty(search) ||
                row.SourceNumber.Contains(search) ||
                (row.InvoiceNumber != null &&
                 row.InvoiceNumber.Contains(search)) ||
                (row.MovementTypeName != null &&
                 row.MovementTypeName.Contains(search)) ||
                (row.Description != null &&
                 row.Description.Contains(search)) ||
                (row.ReferenceNumber != null &&
                 row.ReferenceNumber.Contains(search)));

        var totalCount = await query.CountAsync(cancellationToken);
        var totals = await query
            .GroupBy(_ => 1)
            .Select(rows => new
            {
                CashPaid = rows.Sum(row => row.CashPaid),
                CashReceived = rows.Sum(row => row.CashReceived),
                TripCost = rows.Sum(row => row.TripCost)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var totalCashPaid = totals?.CashPaid ?? 0m;
        var totalCashReceived = totals?.CashReceived ?? 0m;
        var totalTripCost = totals?.TripCost ?? 0m;

        var ordered = query
            .OrderBy(row => row.Date)
            .ThenBy(row => row.CreatedOn)
            .ThenBy(row => row.SourceNumber)
            .ThenBy(row => row.SourceId);
        var offset = GetOffset(pagination, totalCount);
        var precedingEffect = offset == 0
            ? 0m
            : await ordered
                .Take(offset)
                .SumAsync(
                    row => (decimal?)(row.CashPaid -
                        row.CashReceived -
                        row.TripCost),
                    cancellationToken) ?? 0m;
        var pageRows = offset >= totalCount
            ? []
            : await ordered
                .Skip(offset)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);

        var runningBalance = openingBalance + precedingEffect;
        var items = pageRows.Select(row =>
        {
            runningBalance +=
                row.CashPaid - row.CashReceived - row.TripCost;
            return new DriverStatementItemResponse(
                SourceId: row.SourceId,
                Date: row.Date,
                DocumentNumber: row.SourceNumber,
                SourceName: DriverSourceName(row.SourceType),
                InvoiceNumber: row.InvoiceNumber,
                DriverTripId: row.DriverTripId,
                DriverTripNumber: row.DriverTripId.HasValue
                    ? $"TR-{row.DriverTripId.Value}"
                    : null,
                MovementName: row.SourceType == DriverStatementSourceType.DriverTrip
                    ? "تكلفة رحلة"
                    : row.MovementTypeName ?? "سند نقدية",
                Description: row.Description,
                AmountPaidToDriver: row.CashPaid,
                AmountReceivedFromDriver: row.CashReceived,
                TripCost: row.TripCost,
                BalanceAmount: Math.Abs(runningBalance),
                BalanceDescription: DriverBalanceDescription(runningBalance),
                CashboxName: row.CashboxName,
                ReferenceNumber: row.ReferenceNumber)
            {
                BusinessPartnerId = row.BusinessPartnerId,
                BusinessPartnerName = row.BusinessPartnerName,
                CountryName = row.CountryName
            };
        }).ToList();

        var closingBalance =
            openingBalance +
            totalCashPaid -
            totalCashReceived -
            totalTripCost;
        return Result<DriverStatementResponse>.Success(
            new DriverStatementResponse(
                DriverId: driver.Id,
                DriverName: driver.Name,
                Items: items,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: GetTotalPages(totalCount, pagination.PageSize),
                Summary: new DriverStatementSummaryResponse(
                    OpeningBalanceAmount: Math.Abs(openingBalance),
                    OpeningBalanceDescription: DriverBalanceDescription(openingBalance),
                    TotalPaidToDriver: totalCashPaid,
                    TotalReceivedFromDriver: totalCashReceived,
                    TotalTripCost: totalTripCost,
                    ClosingBalanceAmount: Math.Abs(closingBalance),
                    ClosingBalanceDescription: DriverBalanceDescription(closingBalance))));
    }

    private IQueryable<PartnerStatementRaw> CreatePartnerRows(
        int businessPartnerId)
    {
        var movements = dbContext.BusinessPartnerMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.BusinessPartnerId == businessPartnerId)
            .Select(movement => new PartnerStatementRaw
            {
                SourceId = movement.InvoiceId ??
                    movement.CashVoucherId!.Value,
                SourceType = movement.InvoiceId.HasValue
                    ? PartnerStatementSourceType.Invoice
                    : PartnerStatementSourceType.CashVoucher,
                Date = movement.MovementDate,
                CreatedOn = movement.CreatedOn,
                DocumentNumber = movement.InvoiceId.HasValue
                    ? movement.Invoice!.InvoiceNumber
                    : movement.CashVoucher!.VoucherNumber,
                MovementType = movement.MovementType,
                Description = movement.Description,
                Debit = movement.Debit,
                Credit = movement.Credit,
                ExchangeRate = movement.ExchangeRate,
                BaseDebit = movement.BaseDebit,
                BaseCredit = movement.BaseCredit,
                ReferenceNumber = movement.CashVoucherId.HasValue
                    ? movement.CashVoucher!.ReferenceNumber
                    : null,
                CashMovementTypeId = movement.CashVoucherId.HasValue
                    ? movement.CashVoucher!.CashMovementTypeId
                    : null,
                Classification = movement.CashVoucherId.HasValue &&
                    movement.CashVoucher!.CashMovementType != null
                        ? movement.CashVoucher.CashMovementType.Classification
                        : null
            });

        var openingBalances = dbContext.PartnerOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.BusinessPartnerId == businessPartnerId)
            .Select(balance => new PartnerStatementRaw
            {
                SourceId = balance.Id,
                SourceType = PartnerStatementSourceType.OpeningBalance,
                Date = balance.DocumentDate,
                CreatedOn = balance.CreatedOn,
                DocumentNumber = balance.DocumentNumber,
                MovementType = null,
                Description = balance.Notes,
                Debit = balance.BalanceType ==
                    PartnerBalanceType.Receivable
                    ? balance.Amount
                    : 0m,
                Credit = balance.BalanceType ==
                    PartnerBalanceType.Payable
                    ? balance.Amount
                    : 0m,
                ExchangeRate = balance.ExchangeRate,
                BaseDebit = balance.BalanceType ==
                    PartnerBalanceType.Receivable
                    ? balance.BaseAmount
                    : 0m,
                BaseCredit = balance.BalanceType ==
                    PartnerBalanceType.Payable
                    ? balance.BaseAmount
                    : 0m,
                ReferenceNumber = null,
                CashMovementTypeId = null,
                Classification = null
            });

        return movements.Concat(openingBalances);
    }

    private IQueryable<DriverStatementRaw> CreateDriverRows(int driverId)
    {
        var vouchers = dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher =>
                voucher.CompanyId == companyId &&
                voucher.PartyType == CashPartyType.Driver &&
                voucher.DriverId == driverId)
            .Select(voucher => new DriverStatementRaw
            {
                SourceId = voucher.Id,
                SourceType = DriverStatementSourceType.CashVoucher,
                Date = voucher.VoucherDate,
                CreatedOn = voucher.CreatedOn,
                SourceNumber = voucher.VoucherNumber,
                InvoiceNumber = voucher.DriverTripId.HasValue
                    ? voucher.DriverTrip!.InvoiceNumber
                    : null,
                DriverTripId = voucher.DriverTripId,
                BusinessPartnerId = voucher.DriverTripId.HasValue
                    ? voucher.DriverTrip!.BusinessPartnerId
                    : null,
                BusinessPartnerName = voucher.DriverTripId.HasValue
                    ? voucher.DriverTrip!.BusinessPartner.Name
                    : null,
                CountryName = voucher.DriverTripId.HasValue &&
                    voucher.DriverTrip!.Invoice.Country != null
                        ? voucher.DriverTrip.Invoice.Country!.Name
                        : null,
                MovementTypeName = voucher.CashMovementType!.Name,
                Description = voucher.Description,
                CashPaid = voucher.Direction == CashDirection.Payment
                    ? voucher.Amount
                    : 0m,
                CashReceived = voucher.Direction == CashDirection.Receipt
                    ? voucher.Amount
                    : 0m,
                TripCost = 0m,
                CashboxName = voucher.Cashbox!.Name,
                ReferenceNumber = voucher.ReferenceNumber,
                Direction = voucher.Direction,
                CashMovementTypeId = voucher.CashMovementTypeId,
                Classification = voucher.CashMovementType!.Classification
            });

        var trips = dbContext.DriverTrips
            .AsNoTracking()
            .Where(trip =>
                trip.CompanyId == companyId &&
                trip.DriverId == driverId)
            .Select(trip => new DriverStatementRaw
            {
                SourceId = trip.Id,
                SourceType = DriverStatementSourceType.DriverTrip,
                Date = trip.TripDate,
                CreatedOn = trip.CreatedOn,
                SourceNumber = "TR-" + trip.Id,
                InvoiceNumber = trip.InvoiceNumber,
                DriverTripId = (int?)trip.Id,
                BusinessPartnerId = trip.BusinessPartnerId,
                BusinessPartnerName = trip.BusinessPartner.Name,
                CountryName = trip.Invoice.Country == null
                    ? null
                    : trip.Invoice.Country.Name,
                MovementTypeName = null,
                Description = trip.CostNotes,
                CashPaid = 0m,
                CashReceived = 0m,
                TripCost = trip.Cost ?? 0m,
                CashboxName = null,
                ReferenceNumber = null,
                Direction = null,
                CashMovementTypeId = null,
                Classification = null
            });

        return vouchers.Concat(trips);
    }

    private static Error? ValidatePagination(PaginationRequest pagination) =>
        pagination.PageNumber <= 0 ||
        pagination.PageSize is <= 0 or > PaginationRequest.MaxPageSize
            ? PaginationErrors.Invalid()
            : null;

    private static int GetOffset(
        PaginationRequest pagination,
        int totalCount)
    {
        var offset =
            (long)(pagination.PageNumber - 1) * pagination.PageSize;
        return offset >= totalCount ? totalCount : (int)offset;
    }

    private static int GetTotalPages(int totalCount, int pageSize) =>
        (int)Math.Ceiling(totalCount / (double)pageSize);

    private static string PartnerMovementName(
        BusinessPartnerMovementType? movementType) =>
        movementType switch
        {
            null => "رصيد افتتاحي",
            BusinessPartnerMovementType.Sales => "فاتورة بيع",
            BusinessPartnerMovementType.SalesReturn => "مرتجع بيع",
            BusinessPartnerMovementType.Purchase => "فاتورة شراء",
            BusinessPartnerMovementType.PurchaseReturn => "مرتجع شراء",
            BusinessPartnerMovementType.CashReceipt => "سند قبض",
            BusinessPartnerMovementType.CashPayment => "سند صرف",
            _ => "حركة حساب"
        };

    private static string DriverSourceName(
        DriverStatementSourceType sourceType) =>
        sourceType == DriverStatementSourceType.DriverTrip
            ? "رحلة سائق"
            : "سند نقدية";

    private static string PartnerBalanceDescription(decimal balance) =>
        balance switch
        {
            > 0m => "عليه",
            < 0m => "له",
            _ => "مسدد"
        };

    private static string DriverBalanceDescription(decimal balance) =>
        balance switch
        {
            > 0m => "مبلغ مطلوب من السائق",
            < 0m => "مبلغ مطلوب دفعه للسائق",
            _ => "لا يوجد مبلغ مستحق"
        };

    private sealed class CashboxStatementRaw
    {
        public int CashVoucherId { get; init; }
        public DateOnly Date { get; init; }
        public string VoucherNumber { get; init; } = string.Empty;
        public string MovementName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? PartyName { get; init; }
        public CurrencyCode Currency { get; init; }
        public decimal ExchangeRate { get; init; }
        public decimal ReceiptAmount { get; init; }
        public decimal PaymentAmount { get; init; }
        public decimal BaseReceiptAmount { get; init; }
        public decimal BasePaymentAmount { get; init; }
        public string? ReferenceNumber { get; init; }
    }

    private sealed class PartnerStatementRaw
    {
        public int SourceId { get; init; }
        public PartnerStatementSourceType SourceType { get; init; }
        public DateOnly Date { get; init; }
        public DateTime CreatedOn { get; init; }
        public string DocumentNumber { get; init; } = string.Empty;
        public BusinessPartnerMovementType? MovementType { get; init; }
        public string? Description { get; init; }
        public decimal Debit { get; init; }
        public decimal Credit { get; init; }

        public decimal ExchangeRate { get; init; }

        public decimal BaseDebit { get; init; }

        public decimal BaseCredit { get; init; }
        public string? ReferenceNumber { get; init; }
        public int? CashMovementTypeId { get; init; }
        public CashMovementClassification? Classification { get; init; }
    }

    private sealed class DriverStatementRaw
    {
        public int SourceId { get; init; }
        public DriverStatementSourceType SourceType { get; init; }
        public DateOnly Date { get; init; }
        public DateTime CreatedOn { get; init; }
        public string SourceNumber { get; init; } = string.Empty;
        public string? InvoiceNumber { get; init; }
        public int? DriverTripId { get; init; }
        public int? BusinessPartnerId { get; init; }
        public string? BusinessPartnerName { get; init; }
        public string? CountryName { get; init; }
        public string? MovementTypeName { get; init; }
        public string? Description { get; init; }
        public decimal CashPaid { get; init; }
        public decimal CashReceived { get; init; }
        public decimal TripCost { get; init; }
        public string? CashboxName { get; init; }
        public string? ReferenceNumber { get; init; }
        public CashDirection? Direction { get; init; }
        public int? CashMovementTypeId { get; init; }
        public CashMovementClassification? Classification { get; init; }
    }
}
