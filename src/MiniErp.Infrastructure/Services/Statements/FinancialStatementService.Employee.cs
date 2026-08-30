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

        // Materialise first — EF cannot GroupBy over a Concat of two different
        // DbSets, so we aggregate in memory after fetching the lightweight rows.
        var rows = await CreateEmployeeRowsQuery(employeeId)
            .Select(r => new { r.Debit, r.Credit, r.Date })
            .ToListAsync(cancellationToken);

        var totalDebit  = rows.Sum(r => r.Debit);
        var totalCredit = rows.Sum(r => r.Credit);
        var netBalance  = totalCredit - totalDebit;
        var lastDate    = rows.Count > 0 ? (DateOnly?)rows.Max(r => r.Date) : null;

        return Result<EmployeeAccountBalanceResponse>.Success(
            new EmployeeAccountBalanceResponse(
                EmployeeId:          employee.Id,
                EmployeeCode:        employee.Code,
                EmployeeName:        employee.Name,
                Currency:            CurrencyCode.EGP,
                BalanceAmount:       Math.Abs(netBalance),
                BalanceDescription:  EmployeeBalanceDescription(netBalance),
                TotalCredits:        totalCredit,
                TotalDebits:         totalDebit,
                LastMovementDate:    lastDate));
    }

    public async Task<Result<EmployeeAccountSummaryResponse>> GetEmployeeAccountSummaryAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        if (employeeId <= 0)
        {
            return Result<EmployeeAccountSummaryResponse>.Failure(EmployeeNotFound(employeeId));
        }

        var employee = await dbContext.Employees
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.Id == employeeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (employee is null)
        {
            return Result<EmployeeAccountSummaryResponse>.Failure(EmployeeNotFound(employeeId));
        }

        // Materialise the lightweight raw rows — EF cannot GroupBy over a UNION
        // of two DbSets, and we need per-MovementType sums that also require
        // client-side filtering on nullable enum columns.
        var rows = await CreateEmployeeRowsQuery(employeeId)
            .Select(r => new
            {
                r.SourceKind,
                r.BalanceType,
                r.MovementType,
                r.Debit,
                r.Credit,
                r.Date
            })
            .ToListAsync(cancellationToken);

        var totalDebits   = rows.Sum(r => r.Debit);
        var totalCredits  = rows.Sum(r => r.Credit);
        var lastDate      = rows.Count > 0 ? (DateOnly?)rows.Max(r => r.Date) : null;

        var openingBalance =
            rows.Where(r => r.SourceKind == EmployeeStatementRawKind.OpeningBalance)
                .Sum(r => r.Credit - r.Debit);

        var totalAdvances =
            rows.Where(r => r.MovementType == EmployeeMovementType.Advance)
                .Sum(r => r.Debit);

        var totalDeductions =
            rows.Where(r => r.MovementType == EmployeeMovementType.Deduction)
                .Sum(r => r.Debit);

        var totalBonuses =
            rows.Where(r => r.MovementType == EmployeeMovementType.Bonus)
                .Sum(r => r.Credit);

        var currentBalance = totalCredits - totalDebits;

        // Payroll stats — single DbSet; GroupBy is safe here.
        var payrollStats = await dbContext.PayrollEntries
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.EmployeeId == employeeId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalSalaryPosted = g.Sum(p => p.NetSalary),
                TotalSalaryMoved  = g.Where(p => p.IsSalaryMoveToEmployeeAccount).Sum(p => p.NetSalary)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var profile = new EmployeeProfileResponse(
            Id:                             employee.Id,
            CompanyId:                      employee.CompanyId,
            Code:                           employee.Code,
            Name:                           employee.Name,
            JobTitle:                       employee.JobTitle,
            PhoneNumber:                    employee.PhoneNumber,
            Email:                          employee.Email,
            Address:                        employee.Address,
            Type:                           employee.Type,
            DailySalary:                    employee.DailySalary,
            MonthlySalary:                  employee.MonthlySalary,
            RequiredWorkingDaysPerMonth:    employee.RequiredWorkingDaysPerMonth,
            LastDayOfReceivingSalary:       employee.LastDayOfReceivingSalary,
            IsActive:                       employee.IsActive,
            CreatedOn:                      employee.CreatedOn);

        var response = new EmployeeAccountSummaryResponse(
            Employee:           profile,
            Currency:           CurrencyCode.EGP,
            OpeningBalance:     openingBalance,
            CurrentBalance:     currentBalance,
            BalanceDescription: EmployeeBalanceDescription(currentBalance),
            TotalCredits:       totalCredits,
            TotalDebits:        totalDebits,
            TotalAdvances:      totalAdvances,
            TotalDeductions:    totalDeductions,
            TotalBonuses:       totalBonuses,
            TotalSalaryPosted:  payrollStats?.TotalSalaryPosted ?? 0m,
            TotalSalaryMoved:   payrollStats?.TotalSalaryMoved  ?? 0m,
            LastMovementDate:   lastDate);

        return Result<EmployeeAccountSummaryResponse>.Success(response);
    }

    // ─── helpers called AFTER materialisation ───────────────────────────────

    private static string BuildDocumentNumber(EmployeeStatementRawDb raw)
    {
        if (raw.SourceKind == EmployeeStatementRawKind.OpeningBalance)
        {
            return raw.DocumentNumber;           // stored in DB
        }
        // Movement row
        return raw.HasCashVoucher ? raw.DocumentNumber : $"MOV-{raw.SourceId}";
    }

    private static string? BuildReferenceNumber(EmployeeStatementRawDb raw)
    {
        if (raw.SourceKind == EmployeeStatementRawKind.OpeningBalance)
        {
            return raw.PayrollEntryId.HasValue ? $"PAY-{raw.PayrollEntryId.Value}" : null;
        }
        // Movement row
        return raw.HasCashVoucher ? raw.CashVoucherReference : null;
    }

    private static EmployeeStatementSourceType BuildSourceType(EmployeeStatementRawDb raw)
    {
        if (raw.SourceKind == EmployeeStatementRawKind.OpeningBalance)
        {
            return raw.PayrollEntryId.HasValue
                ? EmployeeStatementSourceType.SalaryTransfer
                : EmployeeStatementSourceType.OpeningBalance;
        }
        return raw.HasCashVoucher
            ? EmployeeStatementSourceType.CashVoucher
            : EmployeeStatementSourceType.Movement;
    }

    private static string BuildMovementName(EmployeeStatementRawDb raw)
    {
        if (raw.SourceKind == EmployeeStatementRawKind.OpeningBalance)
        {
            if (raw.PayrollEntryId.HasValue)               return "تحويل راتب مسير";
            return raw.BalanceType == EmployeeBalanceType.Credit
                ? "رصيد دائن افتتاحي"
                : "رصيد مدين افتتاحي";
        }
        // Movement
        if (raw.HasCashVoucher)
        {
            return raw.CashDirection == CashDirection.Payment
                ? "سند صرف نقدية"
                : "سند قبض نقدية";
        }
        return EmployeeMovementTypeName(raw.MovementType!.Value);
    }

    private static EmployeeStatementRaw ToStatementRaw(EmployeeStatementRawDb raw) => new()
    {
        SourceId        = raw.SourceId,
        SourceType      = BuildSourceType(raw),
        MovementType    = raw.MovementType,
        Date            = raw.Date,
        CreatedOn       = raw.CreatedOn,
        DocumentNumber  = BuildDocumentNumber(raw),
        MovementName    = BuildMovementName(raw),
        Description     = raw.Description,
        Debit           = raw.Debit,
        Credit          = raw.Credit,
        ExchangeRate    = raw.ExchangeRate,
        BaseDebit       = raw.BaseDebit,
        BaseCredit      = raw.BaseCredit,
        ReferenceNumber = BuildReferenceNumber(raw)
    };

    // ─── DB query (no string interpolation, fully translatable) ─────────────

    private IQueryable<EmployeeStatementRawDb> CreateEmployeeRowsQuery(int employeeId)
    {
        var openingBalances = dbContext.EmployeeOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.EmployeeId == employeeId)
            .Select(balance => new EmployeeStatementRawDb
            {
                SourceKind          = EmployeeStatementRawKind.OpeningBalance,
                SourceId            = balance.Id,
                PayrollEntryId      = balance.PayrollEntryId,
                MovementType        = null,
                HasCashVoucher      = false,
                CashDirection       = null,
                CashVoucherReference = null,
                BalanceType         = balance.BalanceType,
                Date                = balance.DocumentDate,
                CreatedOn           = balance.CreatedOn,
                DocumentNumber      = balance.DocumentNumber,
                Description         = balance.Notes,
                Debit               = balance.BalanceType == EmployeeBalanceType.Debit  ? balance.Amount     : 0m,
                Credit              = balance.BalanceType == EmployeeBalanceType.Credit ? balance.Amount     : 0m,
                ExchangeRate        = balance.ExchangeRate,
                BaseDebit           = balance.BalanceType == EmployeeBalanceType.Debit  ? balance.BaseAmount : 0m,
                BaseCredit          = balance.BalanceType == EmployeeBalanceType.Credit ? balance.BaseAmount : 0m
            });

        var movements = dbContext.EmployeeMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.EmployeeId == employeeId)
            .Select(movement => new EmployeeStatementRawDb
            {
                SourceKind           = EmployeeStatementRawKind.Movement,
                SourceId             = movement.Id,
                PayrollEntryId       = null,
                MovementType         = movement.Type,
                HasCashVoucher       = movement.CashVoucherId.HasValue,
                CashDirection        = movement.CashVoucherId.HasValue ? (CashDirection?)movement.CashVoucher!.Direction : null,
                CashVoucherReference = movement.CashVoucherId.HasValue ? movement.CashVoucher!.ReferenceNumber : null,
                BalanceType          = null,
                Date                 = movement.MovementDate,
                CreatedOn            = movement.CreatedOn,
                DocumentNumber       = movement.CashVoucherId.HasValue ? movement.CashVoucher!.VoucherNumber : "",
                Description          = movement.Notes,
                Debit                = movement.Debit,
                Credit               = movement.Credit,
                ExchangeRate         = movement.ExchangeRate,
                BaseDebit            = movement.BaseDebit,
                BaseCredit           = movement.BaseCredit
            });

        return openingBalances.Concat(movements);
    }

    // ─── public helper used by statement (keeps paging in DB) ───────────────

    private IQueryable<EmployeeStatementRaw> CreateEmployeeRows(int employeeId)
    {
        // Used only by GetEmployeeStatementAsync which pages in DB.
        // String interpolation is avoided here via the same literals.
        var openingBalances = dbContext.EmployeeOpeningBalances
            .AsNoTracking()
            .Where(balance =>
                balance.CompanyId == companyId &&
                balance.EmployeeId == employeeId)
            .Select(balance => new EmployeeStatementRaw
            {
                SourceId       = balance.Id,
                SourceType     = balance.PayrollEntryId.HasValue
                                    ? EmployeeStatementSourceType.SalaryTransfer
                                    : EmployeeStatementSourceType.OpeningBalance,
                MovementType   = null,
                Date           = balance.DocumentDate,
                CreatedOn      = balance.CreatedOn,
                DocumentNumber = balance.DocumentNumber,
                MovementName   = balance.BalanceType == EmployeeBalanceType.Credit
                                    ? "رصيد دائن افتتاحي"
                                    : "رصيد مدين افتتاحي",
                Description    = balance.Notes,
                Debit          = balance.BalanceType == EmployeeBalanceType.Debit  ? balance.Amount     : 0m,
                Credit         = balance.BalanceType == EmployeeBalanceType.Credit ? balance.Amount     : 0m,
                ExchangeRate   = balance.ExchangeRate,
                BaseDebit      = balance.BalanceType == EmployeeBalanceType.Debit  ? balance.BaseAmount : 0m,
                BaseCredit     = balance.BalanceType == EmployeeBalanceType.Credit ? balance.BaseAmount : 0m,
                ReferenceNumber = null
            });

        var movements = dbContext.EmployeeMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.EmployeeId == employeeId)
            .Select(movement => new EmployeeStatementRaw
            {
                SourceId        = movement.Id,
                SourceType      = movement.CashVoucherId.HasValue
                                    ? EmployeeStatementSourceType.CashVoucher
                                    : EmployeeStatementSourceType.Movement,
                MovementType    = movement.Type,
                Date            = movement.MovementDate,
                CreatedOn       = movement.CreatedOn,
                DocumentNumber  = movement.CashVoucherId.HasValue
                                    ? movement.CashVoucher!.VoucherNumber
                                    : "",
                MovementName    = movement.CashVoucherId.HasValue
                                    ? (movement.CashVoucher!.Direction == CashDirection.Payment
                                        ? "سند صرف نقدية"
                                        : "سند قبض نقدية")
                                    : "",
                Description     = movement.Notes,
                Debit           = movement.Debit,
                Credit          = movement.Credit,
                ExchangeRate    = movement.ExchangeRate,
                BaseDebit       = movement.BaseDebit,
                BaseCredit      = movement.BaseCredit,
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
            _                               => "حركة حساب موظف"
        };
}

// ─── Raw DB projection (no computed strings — fully EF-translatable) ─────────

public sealed class EmployeeStatementRawDb
{
    public EmployeeStatementRawKind  SourceKind           { get; set; }
    public int                       SourceId              { get; set; }
    public int?                      PayrollEntryId        { get; set; }
    public EmployeeMovementType?     MovementType          { get; set; }
    public bool                      HasCashVoucher        { get; set; }
    public CashDirection?            CashDirection         { get; set; }
    public string?                   CashVoucherReference  { get; set; }
    public EmployeeBalanceType?      BalanceType           { get; set; }
    public DateOnly                  Date                  { get; set; }
    public DateTime                  CreatedOn             { get; set; }
    public string                    DocumentNumber        { get; set; } = string.Empty;
    public string?                   Description           { get; set; }
    public decimal                   Debit                 { get; set; }
    public decimal                   Credit                { get; set; }
    public decimal                   ExchangeRate          { get; set; }
    public decimal                   BaseDebit             { get; set; }
    public decimal                   BaseCredit            { get; set; }
}

public enum EmployeeStatementRawKind { OpeningBalance, Movement }

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
