using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Application.Features.PayrollEntries;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Entities.Payroll;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.PayrollEntries
{
    public sealed partial class PayrollEntryService
    {
        public async Task<Result<PayrollEntryResponse>> UpdateOutCompanyAsync(
            int id,
            OutCompanyPayrollEntryUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            var entry = await dbContext.PayrollEntries
                .Include(e => e.Employee)
                .FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == companyId, cancellationToken);

            if (entry is null)
                return Result<PayrollEntryResponse>.Failure(
                    Error.NotFound("PayrollEntry.NotFound", "لم يتم العثور على قيد الراتب المطلوب."));

            var guardError = ValidateForUpdate(entry);
            if (guardError is not null)
                return Result<PayrollEntryResponse>.Failure(guardError);

            var existingFiscalYearGuard = await EnsurePayrollPeriodOpenAsync(
                entry.StartDate,
                entry.EndDate,
                entry.FiscalYearId,
                cancellationToken);
            if (existingFiscalYearGuard.IsFailure)
                return Result<PayrollEntryResponse>.Failure(
                    existingFiscalYearGuard.Errors);

            var employee = entry.Employee;
            if (employee is null)
                return Result<PayrollEntryResponse>.Failure(
                    Error.NotFound("Employee.NotFound", "بيانات الموظف غير موجودة."));

            var eligibilityError = PayrollEntryBusinessRules.ValidateOutCompanyEligibility(employee);
            if (eligibilityError is not null)
                return Result<PayrollEntryResponse>.Failure(eligibilityError);

            var dateError = PayrollEntryBusinessRules.ValidateOutCompanyDates(request.StartDate, request.EndDate, employee.Name);
            if (dateError is not null)
                return Result<PayrollEntryResponse>.Failure(dateError);

            var startDate = request.StartDate;
            var endDate = request.EndDate;

            var fiscalYearGuard = await EnsurePayrollPeriodOpenAsync(
                startDate,
                endDate,
                entry.FiscalYearId,
                cancellationToken);
            if (fiscalYearGuard.IsFailure)
                return Result<PayrollEntryResponse>.Failure(
                    fiscalYearGuard.Errors);

            var existingEntries = await dbContext.PayrollEntries
                .AsNoTracking()
                .Where(p => p.CompanyId == companyId &&
                            p.EmployeeId == entry.EmployeeId &&
                            p.Employee.WorkPlaceStatus == WorkPlaceStatus.OutCompany &&
                            p.Id != entry.Id)
                .Select(p => new PayrollPeriodRecord(p.Id, p.EmployeeId, p.StartDate, p.EndDate))
                .ToListAsync(cancellationToken);

            var periodError = PayrollEntryBusinessRules.ValidateOutCompanyPayrollPeriod(
                startDate,
                endDate,
                existingEntries,
                currentEntryId: entry.Id,
                employeeName: employee.Name);

            if (periodError is not null)
                return Result<PayrollEntryResponse>.Failure(periodError);

            var totalDays = PayrollEntryBusinessRules.CalculateTotalDays(startDate, endDate);
            var presentDaysError = PayrollEntryBusinessRules.ValidatePresentDays(request.PresentDays, totalDays);
            if (presentDaysError is not null)
                return Result<PayrollEntryResponse>.Failure(presentDaysError);

            var absentDays = PayrollEntryBusinessRules.CalculateAbsentDays(totalDays, request.PresentDays);

            var workedDaysUnit = request.WorkedDaysByDayUnit;
            var overtimeUnit = request.OvertimeByDayUnit ?? entry.Overtimebydayunit ?? 0m;
            var deductionUnit = request.DeductionByDayUnit ?? entry.Deductionbydayunit ?? 0m;
            var workedUnits = workedDaysUnit + overtimeUnit - deductionUnit;

            var bonus = request.Bonus ?? entry.Bonus;
            var deduction = request.Deduction ?? entry.Deduction;

            var calc = PayrollCalculator.Calculate(
                employee,
                workedUnits,
                bonus,
                deduction);

            if (calc.GrossSalary < 0)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("Employee.SalaryRequired", "يجب تحديد الراتب أو اليومية للموظف."));

            entry.StartDate                     = startDate;
            entry.EndDate                       = endDate;
            entry.PresentDays                   = request.PresentDays;
            entry.AbsentDays                    = absentDays;
            entry.WorkedDaysbydayunit           = workedDaysUnit;
            entry.Overtimebydayunit             = overtimeUnit;
            entry.Deductionbydayunit            = deductionUnit;
            entry.RequiredWorkingDays           = employee.Type == EmployeeType.Daily ? null : employee.RequiredWorkingDaysPerMonth;
            entry.SalaryPerDay                  = calc.SalaryPerDay;
            entry.Bonus                         = bonus;
            entry.Deduction                     = deduction;
            entry.GrossSalary                   = calc.GrossSalary;
            entry.CalculatedSalary              = calc.CalculatedSalary;
            entry.NetSalary                     = calc.NetSalary;

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<PayrollEntryResponse>.Success(MapToResponse(entry));
        }

        public async Task<Result<List<PayrollEntryResponse>>> UpdateOutCompanyBulkAsync(
            BulkOutCompanyPayrollEntryUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Entries is null || request.Entries.Count == 0)
                return Result<List<PayrollEntryResponse>>.Failure(
                    Error.Validation("PayrollEntry.EmptyBulkRequest", "يجب إرسال قيد راتب واحد على الأقل للتعديل."));

            var ids = request.Entries.Select(e => e.Id).Distinct().ToList();

            var entries = await dbContext.PayrollEntries
                .Include(e => e.Employee)
                .Where(e => e.CompanyId == companyId && ids.Contains(e.Id))
                .ToListAsync(cancellationToken);

            var entriesMap = entries.ToDictionary(e => e.Id);
            if (entries.Count != ids.Count)
            {
                var missingIds = ids.Where(id => !entriesMap.ContainsKey(id)).ToList();
                return Result<List<PayrollEntryResponse>>.Failure(
                    Error.NotFound("PayrollEntry.NotFound",
                        $"بعض قيود الرواتب المحددة غير موجودة: {string.Join(", ", missingIds)}"));
            }

            // Atomic validation: ensure all entries are editable
            foreach (var entry in entries)
            {
                var guardError = ValidateForUpdate(entry);
                if (guardError is not null)
                    return Result<List<PayrollEntryResponse>>.Failure(guardError);

                var existingFiscalYearGuard = await EnsurePayrollPeriodOpenAsync(
                    entry.StartDate,
                    entry.EndDate,
                    entry.FiscalYearId,
                    cancellationToken);
                if (existingFiscalYearGuard.IsFailure)
                    return Result<List<PayrollEntryResponse>>.Failure(
                        existingFiscalYearGuard.Errors);
            }

            var ineligible = entries.Where(e => e.Employee == null || !e.Employee.IsActive || e.Employee.WorkPlaceStatus != WorkPlaceStatus.OutCompany)
                .Select(e => e.EmployeeName)
                .ToList();
            if (ineligible.Count > 0)
            {
                return Result<List<PayrollEntryResponse>>.Failure(
                    Error.Validation("PayrollEntry.EmployeeNotEligible",
                        $"بعض الموظفين غير مؤهلين للتعديل خارج الشركة: {string.Join(", ", ineligible)}"));
            }

            var dateRanges = new Dictionary<int, (DateOnly StartDate, DateOnly EndDate)>();

            foreach (var item in request.Entries)
            {
                var entry = entriesMap[item.Id];
                var emp = entry.Employee!;
                var startDate = item.StartDate ?? request.DefaultStartDate ?? default;
                var endDate = item.EndDate ?? request.DefaultEndDate ?? default;

                var dateError = PayrollEntryBusinessRules.ValidateOutCompanyDates(startDate, endDate, emp.Name);
                if (dateError is not null)
                    return Result<List<PayrollEntryResponse>>.Failure(dateError);

                dateRanges[item.Id] = (startDate, endDate);
            }

            foreach (var item in request.Entries)
            {
                var range = dateRanges[item.Id];
                var fiscalYearGuard = await EnsurePayrollPeriodOpenAsync(
                    range.StartDate,
                    range.EndDate,
                    entriesMap[item.Id].FiscalYearId,
                    cancellationToken);
                if (fiscalYearGuard.IsFailure)
                    return Result<List<PayrollEntryResponse>>.Failure(
                        fiscalYearGuard.Errors);
            }

            var employeeIds = entries.Select(e => e.EmployeeId).Distinct().ToList();

            var existingEntries = await dbContext.PayrollEntries
                .AsNoTracking()
                .Where(p => p.CompanyId == companyId &&
                            employeeIds.Contains(p.EmployeeId) &&
                            p.Employee.WorkPlaceStatus == WorkPlaceStatus.OutCompany &&
                            !ids.Contains(p.Id))
                .Select(p => new PayrollPeriodRecord(p.Id, p.EmployeeId, p.StartDate, p.EndDate))
                .ToListAsync(cancellationToken);

            var existingByEmployee = existingEntries
                .GroupBy(p => p.EmployeeId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<PayrollPeriodRecord>)g.ToList());

            foreach (var item in request.Entries)
            {
                var entry = entriesMap[item.Id];
                var emp = entry.Employee!;
                var (startDate, endDate) = dateRanges[item.Id];

                existingByEmployee.TryGetValue(entry.EmployeeId, out var empExisting);
                var periodError = PayrollEntryBusinessRules.ValidateOutCompanyPayrollPeriod(
                    startDate,
                    endDate,
                    empExisting ?? Array.Empty<PayrollPeriodRecord>(),
                    currentEntryId: entry.Id,
                    employeeName: emp.Name);

                if (periodError is not null)
                    return Result<List<PayrollEntryResponse>>.Failure(periodError);

                var totalDays = PayrollEntryBusinessRules.CalculateTotalDays(startDate, endDate);
                var presentDaysError = PayrollEntryBusinessRules.ValidatePresentDays(item.PresentDays, totalDays);
                if (presentDaysError is not null)
                    return Result<List<PayrollEntryResponse>>.Failure(presentDaysError);
            }

            await using var transaction = await dbContext.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            foreach (var item in request.Entries)
            {
                var entry = entriesMap[item.Id];
                var emp = entry.Employee!;
                var (startDate, endDate) = dateRanges[item.Id];

                var totalDays = PayrollEntryBusinessRules.CalculateTotalDays(startDate, endDate);
                var absentDays = PayrollEntryBusinessRules.CalculateAbsentDays(totalDays, item.PresentDays);

                var workedDaysUnit = item.WorkedDaysByDayUnit;
                var overtimeUnit = item.OvertimeByDayUnit ?? entry.Overtimebydayunit ?? 0m;
                var deductionUnit = item.DeductionByDayUnit ?? entry.Deductionbydayunit ?? 0m;
                var workedUnits = workedDaysUnit + overtimeUnit - deductionUnit;

                var bonus = item.Bonus ?? entry.Bonus;
                var deduction = item.Deduction ?? entry.Deduction;

                var calc = PayrollCalculator.Calculate(
                    emp,
                    workedUnits,
                    bonus,
                    deduction);

                if (calc.GrossSalary < 0)
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Validation("Employee.SalaryRequired",
                            $"يجب تحديد الراتب أو اليومية للموظف {emp.Name}."));

                entry.StartDate                     = startDate;
                entry.EndDate                       = endDate;
                entry.PresentDays                   = item.PresentDays;
                entry.AbsentDays                    = absentDays;
                entry.WorkedDaysbydayunit           = workedDaysUnit;
                entry.Overtimebydayunit             = overtimeUnit;
                entry.Deductionbydayunit            = deductionUnit;
                entry.RequiredWorkingDays           = emp.Type == EmployeeType.Daily ? null : emp.RequiredWorkingDaysPerMonth;
                entry.SalaryPerDay                  = calc.SalaryPerDay;
                entry.Bonus                         = bonus;
                entry.Deduction                     = deduction;
                entry.GrossSalary                   = calc.GrossSalary;
                entry.CalculatedSalary              = calc.CalculatedSalary;
                entry.NetSalary                     = calc.NetSalary;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<List<PayrollEntryResponse>>.Success(entries.Select(MapToResponse).ToList());
        }

        private static PayrollEntryResponse MapToResponse(PayrollEntry entry) =>
            new(
                Id:                             entry.Id,
                CompanyId:                      entry.CompanyId,
                FiscalYearId:                   entry.FiscalYearId,
                StartDate:                      entry.StartDate,
                EndDate:                        entry.EndDate,
                EmployeeId:                     entry.EmployeeId,
                EmployeeCode:                   entry.EmployeeCode,
                EmployeeName:                   entry.EmployeeName,
                EmployeeType:                   entry.EmployeeType,
                Bonus:                          entry.Bonus,
                Deduction:                      entry.Deduction,
                GrossSalary:                    entry.GrossSalary,
                CalculatedSalary:               entry.CalculatedSalary,
                NetSalary:                      entry.NetSalary,
                IsSalaryMoveToEmployeeAccount:  entry.IsSalaryMoveToEmployeeAccount,
                SalaryMovedOn:                  entry.SalaryMovedOn,
                AttendanceSummary: new AttendanceSummary(
                    PresentDays:        entry.PresentDays,
                    AbsentDays:         entry.AbsentDays,
                    TotalPresentDays:   entry.WorkedDaysbydayunit,
                    TotalOvertimeDays:  entry.Overtimebydayunit,
                    TotalDeductionDays: entry.Deductionbydayunit),
                WorkPlaceStatus: entry.Employee != null ? entry.Employee.WorkPlaceStatus : WorkPlaceStatus.InCompany);
    }
}
