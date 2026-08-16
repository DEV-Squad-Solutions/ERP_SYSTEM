using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.EmployeeTransactions;
using MiniErp.Application.Features.PayrollEntries;
using MiniErp.Domain.Entities.Payroll;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.PayrollEntries;

public sealed partial class PayrollEntryService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext,
    IEmployeeTransactionService employeeTransactionService)
    : IPayrollEntryService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    // ─── GET ALL ────────────────────────────────────────────────────────────

    public async Task<Result<PagedResponse<PayrollEntriesListResponse>>> GetAllAsync(
        PaginationRequest pagination,
        PayrollEntryFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new PayrollEntryFilterRequest();

        var baseQuery = dbContext.PayrollEntries
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId);

        var sorted = ApplyFilters(baseQuery, filters);

        return await paginationService.PaginateAsync<
            PayrollEntry,
            PayrollEntriesListResponse>(
            sorted,
            pagination,
            cancellationToken);
    }

    // ─── GET BY ID ──────────────────────────────────────────────────────────

    public async Task<Result<PayrollEntryResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.PayrollEntries
            .AsNoTracking()
            .Include(e => e.Employee)
            .FirstOrDefaultAsync(
                e => e.Id == id && e.CompanyId == companyId,
                cancellationToken);

        if (entry is null)
            return Result<PayrollEntryResponse>.Failure(
                Error.NotFound(
                    "PayrollEntry.NotFound",
                    "لم يتم العثور على قيد الرواتب المطلوب."));

        return Result<PayrollEntryResponse>.Success(MapToResponse(entry));
    }

    // ─── ADD ────────────────────────────────────────────────────────────────

    public async Task<Result<PayrollEntryResponse>> AddAsync(
        PayrollEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(
                e => e.Id == request.EmployeeId && e.CompanyId == companyId,
                cancellationToken);

        if (employee is null)
            return Result<PayrollEntryResponse>.Failure(
                Error.NotFound("Employee.NotFound", "الموظف المحدد غير موجود."));

        var startDate = employee.LastDayOfReceivingSalary?.AddDays(1)
            ?? DateOnly.FromDateTime(employee.CreatedOn);
        var endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.Now);

        if (startDate > endDate)
            return Result<PayrollEntryResponse>.Failure(
                Error.Validation(
                    "PayrollEntry.InvalidDateRange",
                    "تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية."));

        // ── Attendance summary ──────────────────────────────────────────────
        var attendanceSummary = await GetAttendanceSummaryAsync(
            employee.Id, companyId, startDate, endDate, cancellationToken);

        // ── Salary calculation (operator-precedence safe) ───────────────────
        decimal grossSalary;
        decimal calculatedSalary;

        if (employee.Type == EmployeeType.Monthly && employee.MonthlySalary.HasValue)
        {
            grossSalary = employee.MonthlySalary.Value;

            if (employee.RequiredWorkingDaysPerMonth is > 0)
            {
                var salaryPerDay = grossSalary / employee.RequiredWorkingDaysPerMonth.Value;
                var workedUnits = attendanceSummary.TotalPresentDays
                    + (attendanceSummary.TotalOvertimeDays ?? 0m)
                    - (attendanceSummary.TotalDeductionDays ?? 0m);
                calculatedSalary = salaryPerDay * workedUnits;
            }
            else
            {
                calculatedSalary = grossSalary;
            }
        }
        else if (employee.Type == EmployeeType.Daily && employee.DailySalary.HasValue)
        {
            grossSalary = employee.DailySalary.Value;
            var workedUnits = attendanceSummary.TotalPresentDays
                + (attendanceSummary.TotalOvertimeDays ?? 0m)
                - (attendanceSummary.TotalDeductionDays ?? 0m);
            calculatedSalary = grossSalary * workedUnits;
        }
        else
        {
            return Result<PayrollEntryResponse>.Failure(
                Error.Validation(
                    "Employee.SalaryRequired",
                    "يجب تحديد الراتب أو اليومية للموظف."));
        }

        decimal netSalary = calculatedSalary + request.Bonus - request.Deduction;

        var entry = new PayrollEntry
        {
            StartDate = startDate,
            EndDate = endDate,
            CompanyId = companyId,
            EmployeeId = request.EmployeeId,
            EmployeeCode = employee.Code,
            EmployeeName = employee.Name,
            EmployeeType = employee.Type,
            PresentDays = attendanceSummary.PresentDays,
            AbsentDays = attendanceSummary.AbsentDays,
            WorkedDaysbydayunit = attendanceSummary.TotalPresentDays,
            Overtimebydayunit = attendanceSummary.TotalOvertimeDays,
            Deductionbydayunit = attendanceSummary.TotalDeductionDays,
            RequiredWorkingDays = employee.RequiredWorkingDaysPerMonth,
            SalaryPerDay = employee.Type == EmployeeType.Monthly && employee.RequiredWorkingDaysPerMonth is > 0
                ? employee.MonthlySalary!.Value / employee.RequiredWorkingDaysPerMonth.Value
                : employee.DailySalary,
            Bonus = request.Bonus,
            Deduction = request.Deduction,
            GrossSalary = grossSalary,
            CalculatedSalary = calculatedSalary,
            NetSalary = netSalary,
            IsTakeSalary = false
        };

        dbContext.PayrollEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<PayrollEntryResponse>.Success(MapToResponse(entry));
    }

    // ─── PAY SALARY ─────────────────────────────────────────────────────────

    public async Task<Result<PayrollEntryResponse>> PaySalaryAsync(
        int id,
        PayrollEntrySalaryPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Load the payroll entry
        var entry = await dbContext.PayrollEntries
            .Include(e => e.Employee)
            .FirstOrDefaultAsync(
                e => e.Id == id && e.CompanyId == companyId,
                cancellationToken);

        if (entry is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PayrollEntryResponse>.Failure(
                Error.NotFound(
                    "PayrollEntry.NotFound",
                    "لم يتم العثور على قيد الرواتب المطلوب."));
        }

        // Guard: already paid / no amount
        var guardError = ValidateForPayment(entry);
        if (guardError is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PayrollEntryResponse>.Failure(guardError);
        }

        var amount = entry.NetSalary ?? entry.CalculatedSalary;
        var payDate = request.PostingDate;

        // ── Credit the employee account ──────────────────────────────────────
        // Cash does NOT move yet — the employee must withdraw separately.
        var creditResult = await employeeTransactionService.PostSalaryCreditAsync(
            entry.EmployeeId,
            amount,
            entry.Id,
            payDate,
            cancellationToken);

        if (creditResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PayrollEntryResponse>.Failure(creditResult.Error);
        }

        // Mark entry as confirmed (salary posted to account)
        entry.IsTakeSalary = true;
        entry.Employee.LastDayOfReceivingSalary = entry.EndDate;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<PayrollEntryResponse>.Success(MapToResponse(entry));
    }

    // ─── DELETE ─────────────────────────────────────────────────────────────

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.PayrollEntries
            .FirstOrDefaultAsync(
                e => e.Id == id && e.CompanyId == companyId,
                cancellationToken);

        if (entry is null)
            return Result.Failure(
                Error.NotFound(
                    "PayrollEntry.NotFound",
                    "لم يتم العثور على قيد الرواتب المطلوب."));

        if (entry.IsTakeSalary)
            return Result.Failure(
                Error.Conflict(
                    "PayrollEntry.AlreadyPaid",
                    "لا يمكن حذف قيد راتب تم صرفه. يجب إلغاء سند الصرف أولًا."));

        dbContext.PayrollEntries.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    // ─── MAPPING ────────────────────────────────────────────────────────────

    private static PayrollEntryResponse MapToResponse(
        PayrollEntry entry,
        Domain.Entities.Employees.Employee? _ = null) =>
        new(
            entry.Id,
            entry.CompanyId,
            entry.StartDate,
            entry.EndDate,
            entry.EmployeeId,
            entry.EmployeeCode,
            entry.EmployeeName,
            entry.EmployeeType,
            entry.Bonus,
            entry.Deduction,
            entry.GrossSalary,
            entry.NetSalary,
            new AttendanceSummary(
                entry.PresentDays,
                entry.AbsentDays,
                entry.WorkedDaysbydayunit,
                entry.Overtimebydayunit,
                entry.Deductionbydayunit));
}
