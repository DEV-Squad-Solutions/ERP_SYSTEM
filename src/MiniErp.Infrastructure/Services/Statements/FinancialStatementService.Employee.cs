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
            .ThenBy(row => row.SourceId);
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
            runningBalance += EmployeeAccountRules.SignedAmount(row.Debit, row.Credit);
            return new EmployeeStatementItemResponse(
                SourceId: row.SourceId,
                SourceType: row.SourceType,
                Date: row.Date,
                DocumentNumber: row.DocumentNumber,
                MovementName: row.MovementName,
                Description: row.Description,
                DebitAmount: row.Debit,
                CreditAmount: row.Credit,
                BalanceAmount: Math.Abs(runningBalance),
                BalanceDescription: EmployeeAccountRules.GetBalanceDescription(runningBalance),
                ReferenceNumber: row.ReferenceNumber)
            {
                JournalEntryId = row.JournalEntryId,
                JournalEntryLineId = row.JournalEntryLineId,
                Currency = row.Currency,
                OriginalAmount = row.OriginalAmount,
                EgpAmount = row.EgpAmount,
                RunningBalance = runningBalance,
                CashVoucherId = row.CashVoucherId,
                CashVoucherNumber = row.CashVoucherNumber,
                ExchangeRate = row.ExchangeRate,
                BaseDebitAmount = row.BaseDebit,
                BaseCreditAmount = row.BaseCredit,
                BaseBalanceAmount = Math.Abs(runningBalance)
            };
        }).ToArray();

        var closingBalance = openingBalance + EmployeeAccountRules.CalculateBalance(totalCredit, totalDebit);
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
                        EmployeeAccountRules.GetBalanceDescription(openingBalance),
                    TotalDebits: totalDebit,
                    TotalCredits: totalCredit,
                    ClosingBalanceAmount: Math.Abs(closingBalance),
                    ClosingBalanceDescription:
                        EmployeeAccountRules.GetBalanceDescription(closingBalance))
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

        var allRows = CreateEmployeeRows(employee.Id);
        var totals = await allRows
            .GroupBy(_ => 1)
            .Select(rows => new
            {
                Debit = rows.Sum(row => row.Debit),
                Credit = rows.Sum(row => row.Credit),
                LastDate = rows.Max(row => (DateOnly?)row.Date)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var totalDebit = totals?.Debit ?? 0m;
        var totalCredit = totals?.Credit ?? 0m;
        var balance = EmployeeAccountRules.CalculateBalance(totalCredit, totalDebit);

        return Result<EmployeeAccountBalanceResponse>.Success(
            new EmployeeAccountBalanceResponse(
                EmployeeId: employee.Id,
                EmployeeCode: employee.Code,
                EmployeeName: employee.Name,
                Currency: employee.BaseCurrency,
                BalanceAmount: Math.Abs(balance),
                BalanceDescription: EmployeeAccountRules.GetBalanceDescription(balance),
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

        var allRows = CreateEmployeeRows(employeeId);
        var totals = await allRows
            .GroupBy(_ => 1)
            .Select(rows => new
            {
                TotalDebits = rows.Sum(row => row.Debit),
                TotalCredits = rows.Sum(row => row.Credit),
                LastDate = rows.Max(row => (DateOnly?)row.Date)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var totalDebits = totals?.TotalDebits ?? 0m;
        var totalCredits = totals?.TotalCredits ?? 0m;
        var currentBalance = EmployeeAccountRules.CalculateBalance(
            totalCredits,
            totalDebits);

        var openingBalances = await dbContext.EmployeeOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.EmployeeId == employeeId)
            .Select(balance => new
            {
                balance.PayrollEntryId,
                balance.BalanceType,
                balance.Amount
            })
            .ToListAsync(cancellationToken);

        var openingBalance = openingBalances
            .Where(b => !b.PayrollEntryId.HasValue)
            .Sum(b => b.BalanceType == EmployeeBalanceType.Credit ? b.Amount : -b.Amount);

        var totalSalaryMoved = openingBalances
            .Where(b => b.PayrollEntryId.HasValue)
            .Sum(b => b.Amount);

        var movementStats = await dbContext.EmployeeMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.EmployeeId == employeeId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalBonuses = group.Where(m => m.Type == EmployeeMovementType.Bonus).Sum(m => m.Credit),
                TotalDeductions = group.Where(m => m.Type == EmployeeMovementType.Deduction).Sum(m => m.Debit),
                TotalAdvances = group.Where(m => m.CashVoucherId.HasValue && m.Type == EmployeeMovementType.Debit).Sum(m => m.Debit)
            })
            .FirstOrDefaultAsync(cancellationToken);

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

        var recentMovements = await dbContext.EmployeeMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.EmployeeId == employeeId)
            .OrderByDescending(movement => movement.MovementDate)
            .ThenByDescending(movement => movement.Id)
            .Take(10)
            .Select(movement => new EmployeeAccountRecentMovementResponse(
                Id: movement.Id,
                Date: movement.MovementDate,
                Type: movement.Type,
                TypeName: EmployeeAccountRules.GetMovementTypeName(movement.Type),
                Amount: movement.Type == EmployeeMovementType.Credit || movement.Type == EmployeeMovementType.Bonus ? movement.Credit : movement.Debit,
                Debit: movement.Debit,
                Credit: movement.Credit,
                Currency: movement.Currency,
                ExchangeRate: movement.ExchangeRate,
                CashVoucherId: movement.CashVoucherId,
                CashVoucherNumber: movement.CashVoucher != null ? movement.CashVoucher.VoucherNumber : null,
                Notes: movement.Notes))
            .ToListAsync(cancellationToken);

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
                TotalAdvances: movementStats?.TotalAdvances ?? 0m,
                TotalDeductions: movementStats?.TotalDeductions ?? 0m,
                TotalBonuses: movementStats?.TotalBonuses ?? 0m,
                TotalSalaryPosted: payrollStats?.TotalSalaryPosted ?? 0m,
                TotalSalaryMoved: totalSalaryMoved,
                LastMovementDate: totals?.LastDate,
                TotalWithdrawals: 0m,
                RecentMovements: recentMovements,
                PayrollSalaryTransactions: payrollTransactions));
    }

    private IQueryable<EmployeeStatementRaw> CreateEmployeeRows(int employeeId)
    {
        var openingBalances = dbContext.EmployeeOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.EmployeeId == employeeId &&
                !balance.PayrollEntryId.HasValue)
            .Select(balance => new EmployeeStatementRaw
            {
                JournalEntryLineId = null,
                JournalEntryId = null,
                SourceId = balance.Id,
                SourceType = EmployeeStatementSourceType.OpeningBalance,
                MovementType = null,
                Date = balance.DocumentDate,
                CreatedOn = balance.CreatedOn,
                DocumentNumber = balance.DocumentNumber,
                MovementName = balance.BalanceType == EmployeeBalanceType.Credit
                    ? "رصيد دائن افتتاحي"
                    : "رصيد مدين افتتاحي",
                Description = balance.Notes,
                Debit = balance.BalanceType == EmployeeBalanceType.Debit ? balance.Amount : 0m,
                Credit = balance.BalanceType == EmployeeBalanceType.Credit ? balance.Amount : 0m,
                ExchangeRate = balance.ExchangeRate,
                BaseDebit = balance.BalanceType == EmployeeBalanceType.Debit
                    ? (balance.BaseAmount != 0m ? balance.BaseAmount : balance.Amount)
                    : 0m,
                BaseCredit = balance.BalanceType == EmployeeBalanceType.Credit
                    ? (balance.BaseAmount != 0m ? balance.BaseAmount : balance.Amount)
                    : 0m,
                ReferenceNumber = balance.DocumentNumber,
                Currency = balance.Currency,
                OriginalAmount = balance.Amount,
                EgpAmount = balance.BaseAmount != 0m ? balance.BaseAmount : balance.Amount,
                CashVoucherId = null,
                CashVoucherNumber = null
            });

        var salaryTransfers = dbContext.EmployeeOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.EmployeeId == employeeId &&
                balance.PayrollEntryId.HasValue)
            .Select(balance => new EmployeeStatementRaw
            {
                JournalEntryLineId = null,
                JournalEntryId = null,
                SourceId = balance.PayrollEntryId!.Value,
                SourceType = EmployeeStatementSourceType.SalaryTransfer,
                MovementType = null,
                Date = balance.DocumentDate,
                CreatedOn = balance.CreatedOn,
                DocumentNumber = balance.DocumentNumber,
                MovementName = "تحويل راتب مسير",
                Description = balance.Notes,
                Debit = 0m,
                Credit = balance.Amount,
                ExchangeRate = balance.ExchangeRate,
                BaseDebit = 0m,
                BaseCredit = balance.BaseAmount != 0m ? balance.BaseAmount : balance.Amount,
                ReferenceNumber = "PAY-" + balance.PayrollEntryId!.Value,
                Currency = balance.Currency,
                OriginalAmount = balance.Amount,
                EgpAmount = balance.BaseAmount != 0m ? balance.BaseAmount : balance.Amount,
                CashVoucherId = null,
                CashVoucherNumber = null
            });

        var movements = dbContext.EmployeeMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.EmployeeId == employeeId)
            .Select(movement => new EmployeeStatementRaw
            {
                JournalEntryLineId = null,
                JournalEntryId = null,
                SourceId = movement.CashVoucherId.HasValue ? movement.CashVoucherId.Value : movement.Id,
                SourceType = movement.CashVoucherId.HasValue
                    ? EmployeeStatementSourceType.CashVoucher
                    : EmployeeStatementSourceType.Movement,
                MovementType = (EmployeeMovementType?)movement.Type,
                Date = movement.MovementDate,
                CreatedOn = movement.CreatedOn,
                DocumentNumber = movement.CashVoucher != null
                    ? movement.CashVoucher.VoucherNumber
                    : ("MOV-" + movement.Id),
                MovementName = movement.CashVoucher != null
                    ? (movement.CashVoucher.Direction == CashDirection.Receipt ? "سند قبض نقدية" : "سند صرف نقدية")
                    : (movement.Type == EmployeeMovementType.Credit ? "حركة دائنة" :
                       movement.Type == EmployeeMovementType.Debit ? "حركة مدينة" :
                       movement.Type == EmployeeMovementType.Deduction ? "خصم مالي" :
                       movement.Type == EmployeeMovementType.Bonus ? "مكافأة مالية" : "حركة حساب موظف"),
                Description = movement.Notes ?? (movement.CashVoucher != null ? movement.CashVoucher.Description : null),
                Debit = movement.Debit,
                Credit = movement.Credit,
                ExchangeRate = movement.ExchangeRate,
                BaseDebit = movement.BaseDebit,
                BaseCredit = movement.BaseCredit,
                ReferenceNumber = movement.CashVoucher != null
                    ? (movement.CashVoucher.ReferenceNumber ?? movement.CashVoucher.VoucherNumber)
                    : ("MOV-" + movement.Id),
                Currency = movement.Currency,
                OriginalAmount = movement.Type == EmployeeMovementType.Credit || movement.Type == EmployeeMovementType.Bonus ? movement.Credit : movement.Debit,
                EgpAmount = movement.Type == EmployeeMovementType.Credit || movement.Type == EmployeeMovementType.Bonus ? movement.BaseCredit : movement.BaseDebit,
                CashVoucherId = movement.CashVoucherId,
                CashVoucherNumber = movement.CashVoucher != null ? movement.CashVoucher.VoucherNumber : null
            });

        var manualLedgerLines = PostedLedgerLines()
            .Where(line =>
                line.PartyType == JournalPartyType.Employee &&
                line.PartyId == employeeId &&
                line.JournalEntry.SourceType != JournalEntrySourceType.EmployeeOpeningBalance &&
                line.JournalEntry.SourceType != JournalEntrySourceType.CashVoucher)
            .Select(line => new EmployeeStatementRaw
            {
                JournalEntryLineId = line.Id,
                JournalEntryId = line.JournalEntryId,
                SourceId = line.JournalEntryId,
                SourceType = EmployeeStatementSourceType.JournalEntry,
                MovementType = null,
                Date = line.JournalEntry.EntryDate,
                CreatedOn = line.JournalEntry.PostedOn,
                DocumentNumber = line.JournalEntry.SourceNumber ?? line.JournalEntry.EntryNumber,
                MovementName = line.JournalEntry.EntryType == JournalEntryType.Adjustment
                    ? "قيد تسوية"
                    : line.JournalEntry.EntryType == JournalEntryType.Manual
                        ? "قيد يدوي"
                        : "قيد محاسبي",
                Description = line.Description ?? line.JournalEntry.Description,
                Debit = line.Debit,
                Credit = line.Credit,
                ExchangeRate = line.ExchangeRate,
                BaseDebit = line.Debit,
                BaseCredit = line.Credit,
                ReferenceNumber = line.JournalEntry.EntryNumber,
                Currency = line.Currency,
                OriginalAmount = line.Credit > 0m ? line.TransactionCredit : line.TransactionDebit,
                EgpAmount = line.Credit > 0m ? line.Credit : line.Debit,
                CashVoucherId = null,
                CashVoucherNumber = null
            });

        return openingBalances
            .Concat(salaryTransfers)
            .Concat(movements)
            .Concat(manualLedgerLines);
    }
}

internal sealed class EmployeeStatementRaw
{
    public int? JournalEntryLineId { get; init; }
    public int? JournalEntryId { get; init; }
    public int SourceId { get; init; }
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
    public decimal BaseDebit { get; init; }
    public decimal BaseCredit { get; init; }
    public string? ReferenceNumber { get; init; }
    public CurrencyCode Currency { get; init; } = CurrencyCode.EGP;
    public decimal OriginalAmount { get; init; }
    public decimal EgpAmount { get; init; }
    public int? CashVoucherId { get; init; }
    public string? CashVoucherNumber { get; init; }
}
