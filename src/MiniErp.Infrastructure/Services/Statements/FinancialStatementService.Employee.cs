using Microsoft.EntityFrameworkCore;
using static MiniErp.Application.Features.Statements.StatementErrors;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Statements;
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

        var allRows = CreateEmployeeRows(filters.EmployeeId);

        var openingBalance = filters.FromDate.HasValue
            ? await allRows
                .Where(row => row.Date < filters.FromDate.Value)
                .SumAsync(
                    row => (decimal?)(row.Credit - row.Debit),
                    cancellationToken) ?? 0m
            : 0m;

        var baseOpeningBalance = filters.FromDate.HasValue
            ? await allRows
                .Where(row => row.Date < filters.FromDate.Value)
                .SumAsync(
                    row => (decimal?)(row.BaseCredit - row.BaseDebit),
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
                string.IsNullOrEmpty(search) ||
                row.DocumentNumber.Contains(search) ||
                (row.Description != null && row.Description.Contains(search)) ||
                (row.ReferenceNumber != null && row.ReferenceNumber.Contains(search)));

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
                    row => (decimal?)(row.Credit - row.Debit),
                    cancellationToken) ?? 0m;

        var precedingBaseEffect = offset == 0
            ? 0m
            : await ordered
                .Take(offset)
                .SumAsync(
                    row => (decimal?)(row.BaseCredit - row.BaseDebit),
                    cancellationToken) ?? 0m;

        var pageRows = offset >= totalCount
            ? []
            : await ordered
                .Skip(offset)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);

        var runningBalance = openingBalance + precedingEffect;
        var runningBaseBalance = baseOpeningBalance + precedingBaseEffect;

        var items = pageRows.Select(row =>
        {
            runningBalance += row.Credit - row.Debit;
            runningBaseBalance += row.BaseCredit - row.BaseDebit;

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
                BalanceDescription: EmployeeBalanceDescription(runningBalance),
                ReferenceNumber: row.ReferenceNumber)
            {
                ExchangeRate = row.ExchangeRate,
                BaseDebitAmount = row.BaseDebit,
                BaseCreditAmount = row.BaseCredit,
                BaseBalanceAmount = Math.Abs(runningBaseBalance)
            };
        }).ToList();

        var closingBalance = openingBalance + totalCredit - totalDebit;
        var baseClosingBalance = baseOpeningBalance + totalBaseCredit - totalBaseDebit;

        return Result<EmployeeStatementResponse>.Success(
            new EmployeeStatementResponse(
                EmployeeId: employee.Id,
                EmployeeCode: employee.Code,
                EmployeeName: employee.Name,
                Currency: CurrencyCode.EGP,
                Items: items,
                PageNumber: pagination.PageNumber,
                PageSize: pagination.PageSize,
                TotalCount: totalCount,
                TotalPages: GetTotalPages(totalCount, pagination.PageSize),
                Summary: new EmployeeStatementSummaryResponse(
                    OpeningBalanceAmount: Math.Abs(openingBalance),
                    OpeningBalanceDescription: EmployeeBalanceDescription(openingBalance),
                    TotalDebits: totalDebit,
                    TotalCredits: totalCredit,
                    ClosingBalanceAmount: Math.Abs(closingBalance),
                    ClosingBalanceDescription: EmployeeBalanceDescription(closingBalance))
                {
                    BaseOpeningBalanceAmount = Math.Abs(baseOpeningBalance),
                    BaseTotalDebits = totalBaseDebit,
                    BaseTotalCredits = totalBaseCredit,
                    BaseClosingBalanceAmount = Math.Abs(baseClosingBalance)
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
                entity.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (employee is null)
        {
            return Result<EmployeeAccountBalanceResponse>.Failure(
                EmployeeNotFound(employeeId));
        }

        var allRows = CreateEmployeeRows(employeeId);

        var totals = await allRows
            .GroupBy(_ => 1)
            .Select(rows => new
            {
                TotalDebit = rows.Sum(r => r.Debit),
                TotalCredit = rows.Sum(r => r.Credit),
                LastDate = rows.Max(r => (DateOnly?)r.Date)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalDebit = totals?.TotalDebit ?? 0m;
        var totalCredit = totals?.TotalCredit ?? 0m;
        var netBalance = totalCredit - totalDebit;

        return Result<EmployeeAccountBalanceResponse>.Success(
            new EmployeeAccountBalanceResponse(
                EmployeeId: employee.Id,
                EmployeeCode: employee.Code,
                EmployeeName: employee.Name,
                Currency: CurrencyCode.EGP,
                BalanceAmount: Math.Abs(netBalance),
                BalanceDescription: EmployeeBalanceDescription(netBalance),
                TotalCredits: totalCredit,
                TotalDebits: totalDebit,
                LastMovementDate: totals?.LastDate));
    }

    private IQueryable<EmployeeStatementRaw> CreateEmployeeRows(int employeeId)
    {
        var openingBalances = dbContext.EmployeeOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.EmployeeId == employeeId)
            .Select(balance => new EmployeeStatementRaw
            {
                SourceId = balance.Id,
                SourceType = balance.PayrollEntryId.HasValue
                    ? EmployeeStatementSourceType.SalaryTransfer
                    : EmployeeStatementSourceType.OpeningBalance,
                MovementType = null,
                Date = balance.DocumentDate,
                CreatedOn = balance.CreatedOn,
                DocumentNumber = balance.DocumentNumber,
                MovementName = balance.PayrollEntryId.HasValue
                    ? "تحويل راتب مسير"
                    : balance.BalanceType == EmployeeBalanceType.Credit
                        ? "رصيد دائن افتتاحي"
                        : "رصيد مدين افتتاحي",
                Description = balance.Notes,
                Debit = balance.BalanceType == EmployeeBalanceType.Debit
                    ? balance.Amount
                    : 0m,
                Credit = balance.BalanceType == EmployeeBalanceType.Credit
                    ? balance.Amount
                    : 0m,
                ExchangeRate = balance.ExchangeRate,
                BaseDebit = balance.BalanceType == EmployeeBalanceType.Debit
                    ? balance.BaseAmount
                    : 0m,
                BaseCredit = balance.BalanceType == EmployeeBalanceType.Credit
                    ? balance.BaseAmount
                    : 0m,
                ReferenceNumber = balance.PayrollEntryId.HasValue
                    ? $"PAY-{balance.PayrollEntryId.Value}"
                    : null
            });

        var movements = dbContext.EmployeeMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.EmployeeId == employeeId)
            .Select(movement => new EmployeeStatementRaw
            {
                SourceId = movement.Id,
                SourceType = movement.CashVoucherId.HasValue
                    ? EmployeeStatementSourceType.CashVoucher
                    : EmployeeStatementSourceType.Movement,
                MovementType = movement.Type,
                Date = movement.MovementDate,
                CreatedOn = movement.CreatedOn,
                DocumentNumber = movement.CashVoucherId.HasValue
                    ? movement.CashVoucher!.VoucherNumber
                    : $"MOV-{movement.Id}",
                MovementName = movement.CashVoucherId.HasValue
                    ? (movement.CashVoucher!.Direction == CashDirection.Payment ? "سند صرف نقدية" : "سند قبض نقدية")
                    : EmployeeMovementTypeName(movement.Type),
                Description = movement.Notes,
                Debit = movement.Debit,
                Credit = movement.Credit,
                ExchangeRate = movement.ExchangeRate,
                BaseDebit = movement.BaseDebit,
                BaseCredit = movement.BaseCredit,
                ReferenceNumber = movement.CashVoucherId.HasValue
                    ? movement.CashVoucher!.ReferenceNumber
                    : null
            });

        return openingBalances.Concat(movements);
    }

    private static string EmployeeBalanceDescription(decimal netBalance) =>
        netBalance > 0
            ? "دائن (مستحق للموظف)"
            : netBalance < 0
                ? "مدين (مستحق على الموظف)"
                : "متزن (صفر)";

    private static string EmployeeMovementTypeName(EmployeeMovementType type) =>
        type switch
        {
            EmployeeMovementType.Credit     => "حركة دائنة",
            EmployeeMovementType.Debit      => "حركة مدينة",
            EmployeeMovementType.Advance    => "سلفة نقدية",
            EmployeeMovementType.Deduction  => "خصم مالي",
            EmployeeMovementType.Bonus      => "مكافأة مالية",
            EmployeeMovementType.Withdrawal => "سحب نقدي",
            _                               => "حركة حساب موظف"
        };
}

public sealed class EmployeeStatementRaw
{
    public int SourceId { get; set; }
    public EmployeeStatementSourceType SourceType { get; set; }
    public EmployeeMovementType? MovementType { get; set; }
    public DateOnly Date { get; set; }
    public DateTime CreatedOn { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string MovementName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal BaseDebit { get; set; }
    public decimal BaseCredit { get; set; }
    public string? ReferenceNumber { get; set; }
}
