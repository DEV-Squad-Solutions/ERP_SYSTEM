using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.Statements.StatementErrors;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;

namespace MiniErp.Infrastructure.Services.Statements;

public sealed partial class FinancialStatementService
{
    public async Task<Result<EmployeeStatementResponse>> GetEmployeeStatementAsync(
        PaginationRequest pagination,
        EmployeeStatementFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        var paginationError = ValidatePagination(pagination);
        if (paginationError is not null)
        {
            return Result<EmployeeStatementResponse>.Failure(paginationError);
        }

        var employee = await dbContext.Employees
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == filters.EmployeeId)
            .Select(entity => new
            {
                entity.Id,
                entity.Code,
                entity.Name,
                BaseCurrency = entity.Company.Settings == null
                    ? CurrencyCode.EGP
                    : entity.Company.Settings.BaseCurrency
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (employee is null)
        {
            return Result<EmployeeStatementResponse>.Failure(
                EmployeeNotFound(filters.EmployeeId));
        }

        var allRows = CreateEmployeeRows(employee.Id);
        var openingBalance = filters.FromDate.HasValue
            ? await allRows
                .Where(row => row.Date < filters.FromDate.Value)
                .SumAsync(row => (decimal?)(row.Credit - row.Debit),
                    cancellationToken) ?? 0m
            : 0m;
        var search = filters.Search?.Trim();
        var query = allRows
            .Where(row => !filters.FromDate.HasValue ||
                row.Date >= filters.FromDate.Value)
            .Where(row => !filters.ToDate.HasValue ||
                row.Date <= filters.ToDate.Value)
            .Where(row => !filters.SourceType.HasValue ||
                row.SourceType == filters.SourceType.Value)
            .Where(row => !filters.MovementType.HasValue ||
                row.MovementType == filters.MovementType.Value)
            .Where(row => string.IsNullOrEmpty(search) ||
                row.DocumentNumber.Contains(search) ||
                (row.Description != null && row.Description.Contains(search)) ||
                (row.ReferenceNumber != null &&
                 row.ReferenceNumber.Contains(search)));

        var totalCount = await query.CountAsync(cancellationToken);
        var totals = await query
            .GroupBy(_ => 1)
            .Select(rows => new
            {
                Debit = rows.Sum(row => row.Debit),
                Credit = rows.Sum(row => row.Credit)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var totalDebit = totals?.Debit ?? 0m;
        var totalCredit = totals?.Credit ?? 0m;
        var ordered = query
            .OrderBy(row => row.Date)
            .ThenBy(row => row.CreatedOn)
            .ThenBy(row => row.DocumentNumber)
            .ThenBy(row => row.JournalEntryLineId);
        var offset = GetOffset(pagination, totalCount);
        var precedingEffect = offset == 0
            ? 0m
            : await ordered.Take(offset).SumAsync(
                row => (decimal?)(row.Credit - row.Debit), cancellationToken)
                ?? 0m;
        var pageRows = offset >= totalCount
            ? []
            : await ordered
                .Skip(offset)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);

        var runningBalance = openingBalance + precedingEffect;
        var items = pageRows.Select(row =>
        {
            runningBalance += row.Credit - row.Debit;
            return new EmployeeStatementItemResponse(
                SourceId: row.JournalEntryLineId,
                SourceType: row.SourceType,
                Date: row.Date,
                DocumentNumber: row.DocumentNumber,
                MovementName: row.MovementName,
                Description: row.Description,
                DebitAmount: row.Debit,
                CreditAmount: row.Credit,
                BalanceAmount: Math.Abs(runningBalance),
                BalanceDescription: EmployeeBalanceDescription(runningBalance),
                ReferenceNumber: row.ReferenceNumber)
            {
                ExchangeRate = row.ExchangeRate,
                BaseDebitAmount = row.Debit,
                BaseCreditAmount = row.Credit,
                BaseBalanceAmount = Math.Abs(runningBalance),
                JournalEntryId = row.JournalEntryId,
                JournalEntryLineId = row.JournalEntryLineId
            };
        }).ToArray();

        var closingBalance = openingBalance + totalCredit - totalDebit;
        return Result<EmployeeStatementResponse>.Success(
            new EmployeeStatementResponse(
                EmployeeId: employee.Id,
                EmployeeCode: employee.Code,
                EmployeeName: employee.Name,
                Currency: employee.BaseCurrency,
                Items: items,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: GetTotalPages(totalCount, pagination.PageSize),
                Summary: new EmployeeStatementSummaryResponse(
                    OpeningBalanceAmount: Math.Abs(openingBalance),
                    OpeningBalanceDescription:
                        EmployeeBalanceDescription(openingBalance),
                    TotalDebits: totalDebit,
                    TotalCredits: totalCredit,
                    ClosingBalanceAmount: Math.Abs(closingBalance),
                    ClosingBalanceDescription:
                        EmployeeBalanceDescription(closingBalance))
                {
                    BaseOpeningBalanceAmount = Math.Abs(openingBalance),
                    BaseTotalDebits = totalDebit,
                    BaseTotalCredits = totalCredit,
                    BaseClosingBalanceAmount = Math.Abs(closingBalance)
                })
            {
                BaseCurrency = employee.BaseCurrency
            });
    }

    public async Task<Result<EmployeeAccountBalanceResponse>> GetEmployeeBalanceAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var employee = await dbContext.Employees
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == employeeId)
            .Select(entity => new
            {
                entity.Id,
                entity.Code,
                entity.Name,
                BaseCurrency = entity.Company.Settings == null
                    ? CurrencyCode.EGP
                    : entity.Company.Settings.BaseCurrency
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (employee is null)
        {
            return Result<EmployeeAccountBalanceResponse>.Failure(
                EmployeeNotFound(employeeId));
        }

        var totals = await PostedLedgerLines()
            .Where(line =>
                line.PartyType == JournalPartyType.Employee &&
                line.PartyId == employeeId)
            .GroupBy(_ => 1)
            .Select(lines => new
            {
                Debit = lines.Sum(line => line.Debit),
                Credit = lines.Sum(line => line.Credit),
                LastDate = lines.Max(line =>
                    (DateOnly?)line.JournalEntry.EntryDate)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var totalDebit = totals?.Debit ?? 0m;
        var totalCredit = totals?.Credit ?? 0m;
        var balance = totalCredit - totalDebit;

        return Result<EmployeeAccountBalanceResponse>.Success(
            new EmployeeAccountBalanceResponse(
                EmployeeId: employee.Id,
                EmployeeCode: employee.Code,
                EmployeeName: employee.Name,
                Currency: employee.BaseCurrency,
                BalanceAmount: Math.Abs(balance),
                BalanceDescription: EmployeeBalanceDescription(balance),
                TotalCredits: totalCredit,
                TotalDebits: totalDebit,
                LastMovementDate: totals?.LastDate));
    }

    public async Task<Result<EmployeeAccountSummaryResponse>>
        GetEmployeeAccountSummaryAsync(
            int employeeId,
            CancellationToken cancellationToken = default)
    {
        if (employeeId <= 0)
        {
            return Result<EmployeeAccountSummaryResponse>.Failure(
                EmployeeNotFound(employeeId));
        }

        var employee = await dbContext.Employees
            .AsNoTracking()
            .Where(entity =>
                entity.CompanyId == companyId &&
                entity.Id == employeeId)
            .FirstOrDefaultAsync(cancellationToken);
        if (employee is null)
        {
            return Result<EmployeeAccountSummaryResponse>.Failure(
                EmployeeNotFound(employeeId));
        }
        var baseCurrency = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(settings => settings.CompanyId == companyId)
            .Select(settings => (CurrencyCode?)settings.BaseCurrency)
            .FirstOrDefaultAsync(cancellationToken) ?? CurrencyCode.EGP;

        var ledgerRows = await PostedLedgerLines()
            .Where(line =>
                line.PartyType == JournalPartyType.Employee &&
                line.PartyId == employeeId)
            .Select(line => new
            {
                line.Debit,
                line.Credit,
                line.ExchangeRate,
                line.JournalEntry.EntryDate,
                line.JournalEntry.SourceType,
                line.JournalEntry.SourceId
            })
            .ToListAsync(cancellationToken);
        var totalDebits = ledgerRows.Sum(row => row.Debit);
        var totalCredits = ledgerRows.Sum(row => row.Credit);
        var currentBalance = EmployeeAccountRules.CalculateBalance(
            totalCredits,
            totalDebits);
        var openingBalanceMetadata = await dbContext.EmployeeOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.EmployeeId == employeeId)
            .Select(balance => new
            {
                balance.Id,
                balance.PayrollEntryId
            })
            .ToListAsync(cancellationToken);
        var payrollBalanceIds = openingBalanceMetadata
            .Where(balance => balance.PayrollEntryId.HasValue)
            .Select(balance => balance.Id)
            .ToHashSet();
        var openingBalanceIds = openingBalanceMetadata
            .Where(balance => !balance.PayrollEntryId.HasValue)
            .Select(balance => balance.Id)
            .ToHashSet();
        var openingBalance = ledgerRows
            .Where(row =>
                row.SourceType == JournalEntrySourceType.EmployeeOpeningBalance &&
                row.SourceId.HasValue &&
                openingBalanceIds.Contains(row.SourceId.Value))
            .Sum(row => row.Credit - row.Debit);
        var totalSalaryMoved = ledgerRows
            .Where(row =>
                row.SourceType == JournalEntrySourceType.EmployeeOpeningBalance &&
                row.SourceId.HasValue &&
                payrollBalanceIds.Contains(row.SourceId.Value))
            .Sum(row => row.Credit - row.Debit);
        var lastDate = ledgerRows.Count == 0
            ? (DateOnly?)null
            : ledgerRows.Max(row => row.EntryDate);

        // Operational employee movements provide category labels only. The
        // amounts in each category are still summed from the exact matching
        // posted cash-voucher journal lines.
        var movementClasses = await dbContext.EmployeeMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.EmployeeId == employeeId &&
                movement.CashVoucherId.HasValue)
            .Select(movement => new
            {
                movement.CashVoucherId,
                movement.Type,
            })
            .ToListAsync(cancellationToken);
        var movementTypeByVoucher = movementClasses
            .GroupBy(movement => movement.CashVoucherId.GetValueOrDefault())
            .ToDictionary(group => group.Key, group => group.First().Type);
        var categorizedLedgerRows = ledgerRows
            .Where(row =>
                row.SourceType == JournalEntrySourceType.CashVoucher &&
                row.SourceId.HasValue &&
                movementTypeByVoucher.ContainsKey(row.SourceId.Value))
            .Select(row => new
            {
                Type = movementTypeByVoucher[row.SourceId.GetValueOrDefault()],
                row.Debit,
                row.Credit
            })
            .ToList();
        var totalAdvances = categorizedLedgerRows
            .Where(row => row.Type == EmployeeMovementType.Advance)
            .Sum(row => row.Debit);
        var totalWithdrawals = categorizedLedgerRows
            .Where(row => row.Type == EmployeeMovementType.Withdrawal)
            .Sum(row => row.Debit);
        var totalDeductions = categorizedLedgerRows
            .Where(row => row.Type == EmployeeMovementType.Deduction)
            .Sum(row => row.Debit);
        var totalBonuses = categorizedLedgerRows
            .Where(row => row.Type == EmployeeMovementType.Bonus)
            .Sum(row => row.Credit);

        var payrollStats = await dbContext.PayrollEntries
            .AsNoTracking()
            .Where(payroll =>
                payroll.CompanyId == companyId &&
                payroll.EmployeeId == employeeId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalSalaryPosted = group.Sum(payroll => payroll.NetSalary)
            })
            .FirstOrDefaultAsync(cancellationToken);
        var cashVoucherMovementMetadata = await dbContext.EmployeeMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.EmployeeId == employeeId &&
                movement.CashVoucherId.HasValue)
            .Select(movement => new
            {
                movement.Id,
                movement.CashVoucherId,
                movement.Type,
                movement.Notes
            })
            .ToListAsync(cancellationToken);
        var movementMetadataByVoucher = cashVoucherMovementMetadata
            .GroupBy(movement => movement.CashVoucherId.GetValueOrDefault())
            .ToDictionary(group => group.Key, group => group.First());
        var voucherIds = movementMetadataByVoucher.Keys.ToArray();
        var voucherNumbers = voucherIds.Length == 0
            ? new Dictionary<int, string>()
            : await dbContext.CashVouchers
                .AsNoTracking()
                .Where(voucher =>
                    voucher.CompanyId == companyId &&
                    voucherIds.Contains(voucher.Id))
                .Select(voucher => new { voucher.Id, voucher.VoucherNumber })
                .ToDictionaryAsync(
                    voucher => voucher.Id,
                    voucher => voucher.VoucherNumber,
                    cancellationToken);
        var recentMovements = ledgerRows
            .Where(row =>
                row.SourceType == JournalEntrySourceType.CashVoucher &&
                row.SourceId.HasValue &&
                movementMetadataByVoucher.ContainsKey(row.SourceId.Value))
            .GroupBy(row => row.SourceId!.Value)
            .Select(group =>
            {
                var movement = movementMetadataByVoucher[group.Key];
                var debit = group.Sum(row => row.Debit);
                var credit = group.Sum(row => row.Credit);
                return new EmployeeAccountRecentMovementResponse(
                    Id: movement.Id,
                    Date: group.Min(row => row.EntryDate),
                    Type: movement.Type,
                    TypeName: EmployeeMovementTypeName(movement.Type),
                    Amount: debit > 0m ? debit : credit,
                    Debit: debit,
                    Credit: credit,
                    Currency: baseCurrency,
                    ExchangeRate: group.Max(row => row.ExchangeRate),
                    CashVoucherId: movement.CashVoucherId,
                    CashVoucherNumber: voucherNumbers.GetValueOrDefault(group.Key),
                    Notes: movement.Notes);
            })
            .OrderByDescending(movement => movement.Date)
            .ThenByDescending(movement => movement.Id)
            .Take(10)
            .ToList();

        var payrollTransactions = await dbContext.PayrollEntries
            .AsNoTracking()
            .Where(payroll =>
                payroll.CompanyId == companyId &&
                payroll.EmployeeId == employeeId)
            .OrderByDescending(payroll => payroll.EndDate)
            .ThenByDescending(payroll => payroll.Id)
            .Take(10)
            .Select(payroll => new EmployeeAccountPayrollTransactionResponse(
                Id: payroll.Id,
                StartDate: payroll.StartDate,
                EndDate: payroll.EndDate,
                NetSalary: payroll.NetSalary,
                IsSalaryMoveToEmployeeAccount:
                    payroll.IsSalaryMoveToEmployeeAccount,
                SalaryMovedOn: payroll.SalaryMovedOn,
                Notes: null))
            .ToListAsync(cancellationToken);

        var profile = new EmployeeProfileResponse(
            Id: employee.Id,
            CompanyId: employee.CompanyId,
            Code: employee.Code,
            Name: employee.Name,
            JobTitle: employee.JobTitle,
            PhoneNumber: employee.PhoneNumber,
            Email: employee.Email,
            Address: employee.Address,
            Type: employee.Type,
            DailySalary: employee.DailySalary,
            MonthlySalary: employee.MonthlySalary,
            RequiredWorkingDaysPerMonth: employee.RequiredWorkingDaysPerMonth,
            LastDayOfReceivingSalary: employee.LastDayOfReceivingSalary,
            IsActive: employee.IsActive,
            CreatedOn: employee.CreatedOn);

        return Result<EmployeeAccountSummaryResponse>.Success(
            new EmployeeAccountSummaryResponse(
                Employee: profile,
                Currency: baseCurrency,
                OpeningBalance: openingBalance,
                CurrentBalance: currentBalance,
                BalanceDescription:
                    EmployeeAccountRules.GetBalanceDescription(currentBalance),
                TotalCredits: totalCredits,
                TotalDebits: totalDebits,
                TotalAdvances: totalAdvances,
                TotalDeductions: totalDeductions,
                TotalBonuses: totalBonuses,
                TotalSalaryPosted: payrollStats?.TotalSalaryPosted ?? 0m,
                TotalSalaryMoved: totalSalaryMoved,
                LastMovementDate: lastDate,
                TotalWithdrawals: totalWithdrawals,
                RecentMovements: recentMovements,
                PayrollSalaryTransactions: payrollTransactions));
    }

    private IQueryable<EmployeeStatementRaw> CreateEmployeeRows(int employeeId)
    {
        var openingBalances = dbContext.EmployeeOpeningBalances
            .AsNoTracking()
            .Where(balance => balance.CompanyId == companyId);
        var vouchers = dbContext.CashVouchers
            .AsNoTracking()
            .Where(voucher => voucher.CompanyId == companyId);

        return
            from line in PostedLedgerLines()
            where line.PartyType == JournalPartyType.Employee &&
                  line.PartyId == employeeId
            join balance in openingBalances
                on new
                {
                    line.JournalEntry.SourceId,
                    line.JournalEntry.SourceType
                }
                equals new
                {
                    SourceId = (int?)balance.Id,
                    SourceType = (JournalEntrySourceType?)
                        JournalEntrySourceType.EmployeeOpeningBalance
                }
                into balanceRows
            from balance in balanceRows.DefaultIfEmpty()
            join voucher in vouchers
                on new
                {
                    line.JournalEntry.SourceId,
                    line.JournalEntry.SourceType
                }
                equals new
                {
                    SourceId = (int?)voucher.Id,
                    SourceType = (JournalEntrySourceType?)
                        JournalEntrySourceType.CashVoucher
                }
                into voucherRows
            from voucher in voucherRows.DefaultIfEmpty()
            select new EmployeeStatementRaw
            {
                JournalEntryLineId = line.Id,
                JournalEntryId = line.JournalEntryId,
                SourceType = balance != null
                    ? balance.PayrollEntryId.HasValue
                        ? EmployeeStatementSourceType.SalaryTransfer
                        : EmployeeStatementSourceType.OpeningBalance
                    : voucher != null
                        ? EmployeeStatementSourceType.CashVoucher
                        : EmployeeStatementSourceType.JournalEntry,
                MovementType = voucher == null
                    ? null
                    : dbContext.EmployeeMovements
                        .Where(movement =>
                            movement.CompanyId == companyId &&
                            movement.EmployeeId == employeeId &&
                            movement.CashVoucherId == voucher.Id)
                        .Select(movement =>
                            (EmployeeMovementType?)movement.Type)
                        .FirstOrDefault(),
                Date = line.JournalEntry.EntryDate,
                CreatedOn = line.JournalEntry.PostedOn,
                DocumentNumber = balance != null
                    ? balance.DocumentNumber
                    : voucher != null
                        ? voucher.VoucherNumber
                        : line.JournalEntry.SourceNumber ??
                          line.JournalEntry.EntryNumber,
                MovementName = balance != null
                    ? balance.PayrollEntryId.HasValue
                        ? "تحويل راتب مسير"
                        : balance.BalanceType == EmployeeBalanceType.Credit
                            ? "رصيد دائن افتتاحي"
                            : "رصيد مدين افتتاحي"
                    : voucher != null
                        ? voucher.Direction == CashDirection.Payment
                            ? "سند صرف نقدية"
                            : "سند قبض نقدية"
                        : line.JournalEntry.EntryType ==
                          JournalEntryType.Adjustment
                            ? "قيد تسوية"
                            : line.JournalEntry.EntryType ==
                              JournalEntryType.Manual
                                ? "قيد يدوي"
                                : line.JournalEntry.EntryType ==
                                  JournalEntryType.Opening
                                    ? "قيد افتتاحي"
                                    : "قيد محاسبي",
                Description = line.Description ??
                    (balance != null
                        ? balance.Notes
                        : line.JournalEntry.Description),
                Debit = line.Debit,
                Credit = line.Credit,
                ExchangeRate = line.ExchangeRate,
                ReferenceNumber = balance != null
                    ? balance.PayrollEntryId.HasValue
                        ? "PAY-" + balance.PayrollEntryId.Value
                        : null
                    : voucher == null
                        ? null
                        : voucher.ReferenceNumber
            };
    }

    private static string EmployeeBalanceDescription(decimal netBalance) =>
        EmployeeAccountRules.GetBalanceDescription(netBalance);

    private static string EmployeeMovementTypeName(EmployeeMovementType type) =>
        EmployeeAccountRules.GetMovementTypeName(type);
}

internal sealed class EmployeeStatementRaw
{
    public int JournalEntryLineId { get; init; }
    public int JournalEntryId { get; init; }
    public EmployeeStatementSourceType SourceType { get; init; }
    public EmployeeMovementType? MovementType { get; init; }
    public DateOnly Date { get; init; }
    public DateTime CreatedOn { get; init; }
    public string DocumentNumber { get; init; } = string.Empty;
    public string MovementName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    public decimal ExchangeRate { get; init; }
    public string? ReferenceNumber { get; init; }
}
