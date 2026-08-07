using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.PayrollEntries;
using MiniErp.Domain.Entities.Payroll;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.PayrollEntries;

public sealed partial class PayrollEntryService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : IPayrollEntryService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<PayrollEntryResponse>>> GetAllAsync(
        PaginationRequest pagination,
        PayrollEntryFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new PayrollEntryFilterRequest();

        var baseQuery = dbContext.PayrollEntries
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId);

        if (filters.StartDate.HasValue)
        {
            baseQuery = baseQuery.Where(e => e.StartDate >= filters.StartDate);
        }

        if (filters.EndDate.HasValue)
        {
            baseQuery = baseQuery.Where(e => e.EndDate <= filters.EndDate);
        }

        if (filters.EmployeeId.HasValue)
        {
            baseQuery = baseQuery.Where(e => e.EmployeeId == filters.EmployeeId);
        }

        var search = filters.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            baseQuery = baseQuery.Where(e =>
                e.EmployeeCode.Contains(search) ||
                e.EmployeeName.Contains(search));
        }

        var query = baseQuery
            .OrderBy(e => e.EmployeeName)
            .ThenBy(e => e.Id);

        var pageResult = await paginationService.PaginateAsync<
            PayrollEntry,
            PayrollEntryResponse>(
            query,
            pagination,
            cancellationToken);

        return pageResult;
    }

    public async Task<Result<PayrollEntryResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.PayrollEntries
            .AsNoTracking()
            .Include(e => e.Employee)
            .FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == companyId, cancellationToken);

        if (entry is null)
        {
            return Result<PayrollEntryResponse>.Failure(
                Error.NotFound(
                    "PayrollEntry.NotFound",
                    "لم يتم العثور على قيد الرواتب المطلوب."));
        }

        return Result<PayrollEntryResponse>.Success(
            MapToResponse(entry));
    }

    public async Task<Result<PayrollEntryResponse>> AddAsync(
        PayrollEntryRequest request,
        CancellationToken cancellationToken = default)
    {

        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.CompanyId == companyId, cancellationToken);
        if (employee == null)
        {
            return Result<PayrollEntryResponse>.Failure(
                Error.NotFound(
                    "Employee.NotFound",
                    "الموظف المحدد غير موجود."));
        }
        var StartDate =employee.LastDayOfReceivingSalary?.AddDays(1) ?? DateOnly.FromDateTime(employee.CreatedOn);
        var EndDate = request.EndDate??DateOnly.FromDateTime(DateTime.Now);
        // Calculate present days from attendance records during the period
        var requiredWorkingDays = employee.RequiredWorkingDaysPerMonth ?? null;
        var attendanceSummary = await GetAttendanceSummaryAsync(
            employee.Id,
            companyId,
            StartDate,
            EndDate,
            cancellationToken);
        // Calculate gross salary based on employee type
        decimal grossSalary = 0;
        decimal calculatedSalary = 0;
        if (employee.Type == EmployeeType.Monthly && employee.MonthlySalary.HasValue)
        {
            grossSalary = employee.MonthlySalary.Value;
            //caculate net salary based on present days, overtime, and deductions by day unit (days, half days, quarter days, third days)
            if (employee.RequiredWorkingDaysPerMonth.HasValue && employee.RequiredWorkingDaysPerMonth.Value > 0)
            {
                calculatedSalary = (grossSalary / employee.RequiredWorkingDaysPerMonth ?? 1) *
                    (attendanceSummary.TotalPresentDays
                    + (attendanceSummary.TotalOvertimeDays) ?? 0m
                    - (attendanceSummary.TotalDeductionDays) ?? 0m);
            }
            else {
                calculatedSalary = grossSalary;
            }
        }
        else if (employee.Type == EmployeeType.Daily && employee.DailySalary.HasValue)
        {   
            grossSalary = employee.DailySalary.Value;
            //caculate net salary based on present days, overtime, and deductions by day unit (days, half days, quarter days, third days)
            calculatedSalary = grossSalary * ( 
                attendanceSummary.TotalPresentDays
                + (attendanceSummary.TotalOvertimeDays) ?? 0m
                - (attendanceSummary.TotalDeductionDays) ?? 0m);
        }
        else
        {
            return Result<PayrollEntryResponse>.Failure(
                Error.Validation(
                    "Employee.SalaryRequired",
                    "يجب تحديد الراتب أو اليوميه للموظف."));
        }

        // Calculate net salary
        decimal netSalary = calculatedSalary + request.Bonus - request.Deduction;
        
        var entry = new PayrollEntry
        {
            StartDate = StartDate,
            EndDate = EndDate,
            CompanyId = companyId,
            EmployeeId = request.EmployeeId,
            EmployeeCode = employee.Code,
            EmployeeName = employee.Name,
            EmployeeType = employee.Type,
            PresentDays = attendanceSummary.PresentDays, //the total of actual appearance in campany
            AbsentDays = attendanceSummary.AbsentDays,//the total of actual absence in campany
            WorkedDaysbydayunit = attendanceSummary.TotalPresentDays,
            Overtimebydayunit = attendanceSummary.TotalOvertimeDays,
            RequiredWorkingDays = requiredWorkingDays,
            Deductionbydayunit = attendanceSummary.TotalDeductionDays,
            Bonus = request.Bonus,
            Deduction = request.Deduction,
            GrossSalary = grossSalary,
            NetSalary = netSalary
        };

        dbContext.PayrollEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<PayrollEntryResponse>.Success(
            MapToResponse(entry, employee));
    }
/*
    //public async Task<Result<PayrollEntryResponse>> UpdateAsync(
    //    int id,
    //    PayrollEntryRequest request,
    //    CancellationToken cancellationToken = default)
    //{
    //    var payrollEntry = await dbContext.PayrollEntries
    //        .FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == companyId, cancellationToken);
    //    if (payrollEntry == null) {
    //        return Result<PayrollEntryResponse>.Failure(
    //            Error.NotFound(
    //                "PayrollEntry.NotFound",
    //                "قيد الرواتب المحدد غير موجود."));
    //    }
    //    payrollEntry.Bonus = request.Bonus;
    //    payrollEntry.Deduction = request.Deduction;
        

    //    dbContext.PayrollEntries.Update(payrollEntry).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
    //    await dbContext.SaveChangesAsync(cancellationToken);

    //    return Result<PayrollEntryResponse>.Success(
    //        MapToResponse(payrollEntry));
    //}
*/
    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var payrollEntry = await dbContext.PayrollEntries
            .FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == companyId, cancellationToken);

        if (payrollEntry is null)
        {
            return Result.Failure(
                Error.NotFound(
                    "PayrollEntry.NotFound",
                    "لم يتم العثور على قيد الرواتب المطلوب."));
        }

        dbContext.PayrollEntries.Remove(payrollEntry);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static PayrollEntryResponse MapToResponse(PayrollEntry entry, Domain.Entities.Employees.Employee? employee = null)
    {
        return new PayrollEntryResponse(
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
                entry.Deductionbydayunit
            )
        );
    }
}
