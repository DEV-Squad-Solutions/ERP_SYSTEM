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
        public async Task<Result<PayrollEntryResponse>> RecalculateAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var entry = await dbContext.PayrollEntries
                .Include(e => e.Employee)
                .FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == companyId, cancellationToken);

            if (entry is null)
                return Result<PayrollEntryResponse>.Failure(
                    Error.NotFound("PayrollEntry.NotFound", "لم يتم العثور على قيد الراتب المطلوب."));
            
            if(entry.IsSalaryMovedToEmployeeAccount == true)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Conflict("PayrollEntry.AlreadyPaid", "لا يمكن إعادة حساب قيد الراتب بعد تحويل الراتب إلى حساب الموظف."));
            
            var guardError = ValidateForUpdate(entry);
            if (guardError is not null)
                return Result<PayrollEntryResponse>.Failure(guardError);

            var fiscalYearGuard = await EnsurePayrollPeriodOpenAsync(
                entry.StartDate,
                entry.EndDate,
                entry.FiscalYearId,
                cancellationToken);
            if (fiscalYearGuard.IsFailure)
                return Result<PayrollEntryResponse>.Failure(
                    fiscalYearGuard.Errors);

            if (entry.Employee is null)
                return Result<PayrollEntryResponse>.Failure(
                    Error.NotFound("Employee.NotFound", "لم يتم العثور على بيانات الموظف المرتبطة بقيد الراتب."));

            var employee = entry.Employee;

            if (employee.WorkPlaceStatus == WorkPlaceStatus.OutCompany)
            {
                var workedUnits = entry.WorkedDaysbydayunit
                    + (entry.Overtimebydayunit ?? 0m)
                    - (entry.Deductionbydayunit ?? 0m);

                var calc = PayrollCalculator.Calculate(
                    employee,
                    workedUnits,
                    entry.Bonus,
                    entry.Deduction);

                if (calc.GrossSalary < 0)
                    return Result<PayrollEntryResponse>.Failure(
                        Error.Validation("Employee.SalaryRequired", "يجب تحديد الراتب أو اليومية للموظف."));

                entry.GrossSalary       = calc.GrossSalary;
                entry.CalculatedSalary  = calc.CalculatedSalary;
                entry.NetSalary         = calc.NetSalary;
                entry.SalaryPerDay      = calc.SalaryPerDay;
            }
            else
            {
                var attendanceSummary = await GetAttendanceSummaryAsync(
                    employee.Id, companyId, entry.StartDate, entry.EndDate, cancellationToken);

                var (grossSalary, calculatedSalary, netSalary, salaryPerDay) = CalculateSalaryWithExtras(
                    employee,
                    attendanceSummary,
                    entry.Bonus,
                    entry.Deduction);

                if (grossSalary < 0)
                    return Result<PayrollEntryResponse>.Failure(
                        Error.Validation("Employee.SalaryRequired",
                            "يجب تحديد الراتب أو اليومية للموظف."));

                entry.PresentDays                   = attendanceSummary.PresentDays;
                entry.AbsentDays                    = attendanceSummary.AbsentDays;
                entry.WorkedDaysbydayunit           = attendanceSummary.TotalPresentDays;
                entry.Overtimebydayunit             = attendanceSummary.TotalOvertimeDays;
                entry.Deductionbydayunit            = attendanceSummary.TotalDeductionDays;
                entry.RequiredWorkingDays           = employee.Type == EmployeeType.Daily ? null : employee.RequiredWorkingDaysPerMonth;
                entry.SalaryPerDay                  = salaryPerDay;
                entry.GrossSalary                   = grossSalary;
                entry.CalculatedSalary              = calculatedSalary;
                entry.NetSalary                     = netSalary;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<PayrollEntryResponse>.Success(MapToResponse(entry));
        }

        public async Task<Result> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var entry = await dbContext.PayrollEntries
                .FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == companyId, cancellationToken);

            if (entry is null)
                return Result.Failure(
                    Error.NotFound("PayrollEntry.NotFound", "لم يتم العثور على قيد الرواتب المطلوب."));

            var guardError = ValidateForUpdate(entry);
            if (guardError is not null)
                return Result.Failure(guardError);

            var fiscalYearGuard = await EnsurePayrollPeriodOpenAsync(
                entry.StartDate,
                entry.EndDate,
                entry.FiscalYearId,
                cancellationToken);
            if (fiscalYearGuard.IsFailure)
                return Result.Failure(fiscalYearGuard.Errors);

            dbContext.PayrollEntries.Remove(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> DeleteBulkAsync(
            BulkPayrollEntryDeleteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.PayrollEntryIds is null || request.PayrollEntryIds.Count == 0)
                return Result.Failure(
                    Error.Validation("PayrollEntry.EmptyBulkRequest", "يجب تحديد معرفات قيود الرواتب المراد حذفها."));

            var ids = request.PayrollEntryIds.Distinct().ToList();

            var entries = await dbContext.PayrollEntries
                .Where(e => e.CompanyId == companyId && ids.Contains(e.Id))
                .ToListAsync(cancellationToken);

            if (entries.Count != ids.Count)
            {
                var existingIds = entries.Select(e => e.Id).ToHashSet();
                var missingIds = ids.Where(id => !existingIds.Contains(id)).ToList();
                return Result.Failure(
                    Error.NotFound("PayrollEntry.NotFound",
                        $"بعض قيود الرواتب المحددة غير موجودة: {string.Join(", ", missingIds)}"));
            }

            foreach (var entry in entries)
            {
                var guardError = ValidateForUpdate(entry);
                if (guardError is not null)
                    return Result.Failure(guardError);

                var fiscalYearGuard = await EnsurePayrollPeriodOpenAsync(
                    entry.StartDate,
                    entry.EndDate,
                    entry.FiscalYearId,
                    cancellationToken);
                if (fiscalYearGuard.IsFailure)
                    return Result.Failure(fiscalYearGuard.Errors);
            }

            dbContext.PayrollEntries.RemoveRange(entries);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result<PayrollDashboardResponse>> GetDashboardAsync(
            PayrollDashboardFilterRequest? filters = null,
            CancellationToken cancellationToken = default)
        {
            filters ??= new PayrollDashboardFilterRequest();

            var fiscalYearResult = await fiscalYearQueryScopeResolver.ResolveAsync(
                filters.FiscalYearId,
                filters.FromDate,
                filters.ToDate,
                cancellationToken);
            if (fiscalYearResult.IsFailure)
                return Result<PayrollDashboardResponse>.Failure(
                    fiscalYearResult.Errors);
            var fiscalYear = fiscalYearResult.Value;

            var payrollQuery = dbContext.PayrollEntries
                .AsNoTracking()
                .Where(p =>
                    p.CompanyId == companyId &&
                    p.FiscalYearId == fiscalYear.FiscalYearId);

            if (filters.FromDate.HasValue)
                payrollQuery = payrollQuery.Where(p => p.StartDate >= filters.FromDate.Value);

            if (filters.ToDate.HasValue)
                payrollQuery = payrollQuery.Where(p => p.EndDate <= filters.ToDate.Value);

            if (filters.EmployeeId.HasValue)
                payrollQuery = payrollQuery.Where(p => p.EmployeeId == filters.EmployeeId.Value);

            if (filters.EmployeeType.HasValue)
                payrollQuery = payrollQuery.Where(p => p.EmployeeType == filters.EmployeeType.Value);

            var payrollStats = await payrollQuery
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalGross = g.Sum(p => p.GrossSalary),
                    TotalNet = g.Sum(p => p.NetSalary),
                    TotalMoved = g.Where(p => p.IsSalaryMoveToEmployeeAccount).Sum(p => p.NetSalary),
                    TotalDeduction = g.Sum(p => p.Deduction ?? 0m),
                    TotalBonus = g.Sum(p => p.Bonus ?? 0m)
                })
                .FirstOrDefaultAsync(cancellationToken);

            var movementQuery = dbContext.EmployeeMovements
                .AsNoTracking()
                .Where(m =>
                    m.CompanyId == companyId &&
                    m.FiscalYearId == fiscalYear.FiscalYearId);

            if (filters.FromDate.HasValue)
                movementQuery = movementQuery.Where(m => m.MovementDate >= filters.FromDate.Value);

            if (filters.ToDate.HasValue)
                movementQuery = movementQuery.Where(m => m.MovementDate <= filters.ToDate.Value);

            if (filters.EmployeeId.HasValue)
                movementQuery = movementQuery.Where(m => m.EmployeeId == filters.EmployeeId.Value);

            var movementStats = await movementQuery
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalDebits = g.Where(m => m.Type == EmployeeMovementType.Debit).Sum(m => m.Debit),
                    TotalMovementDeductions = g.Where(m => m.Type == EmployeeMovementType.Deduction).Sum(m => m.Debit)
                })
                .FirstOrDefaultAsync(cancellationToken);

            var totalEmployeeCount = await dbContext.Employees
                .AsNoTracking()
                .Where(e => e.CompanyId == companyId && (!filters.EmployeeId.HasValue || e.Id == filters.EmployeeId.Value) && (!filters.EmployeeType.HasValue || e.Type == filters.EmployeeType.Value))
                .CountAsync(cancellationToken);

            var pendingPayrolls = await payrollQuery
                .Where(p => !p.IsSalaryMoveToEmployeeAccount)
                .OrderByDescending(p => p.EndDate)
                .ThenByDescending(p => p.Id)
                .Take(20)
                .Select(p => new PayrollDashboardPendingEntryResponse(
                    p.Id,
                    p.EmployeeId,
                    p.EmployeeCode,
                    p.EmployeeName,
                    p.EmployeeType,
                    p.StartDate,
                    p.EndDate,
                    p.GrossSalary,
                    p.NetSalary,
                    p.Bonus,
                    p.Deduction,
                    p.IsSalaryMoveToEmployeeAccount))
                .ToListAsync(cancellationToken);

            var recentMovements = await movementQuery
                .OrderByDescending(m => m.MovementDate)
                .ThenByDescending(m => m.Id)
                .Take(10)
                .Select(m => new PayrollDashboardRecentOperationResponse(
                    m.CashVoucherId.HasValue ? m.CashVoucherId.Value : m.Id,
                    m.CashVoucherId.HasValue ? "CashVoucher" : m.Type.ToString(),
                    m.CashVoucherId.HasValue
                        ? (m.CashVoucher != null && m.CashVoucher.Direction == CashDirection.Receipt ? "سند قبض نقدية" : "سند صرف نقدية")
                        : (m.Type == EmployeeMovementType.Deduction ? "خصم مالي" :
                           m.Type == EmployeeMovementType.Bonus ? "مكافأة مالية" :
                           m.Type == EmployeeMovementType.Credit ? "حركة دائنة" : "حركة مدينة"),
                    m.EmployeeId,
                    m.Employee.Code,
                    m.Employee.Name,
                    m.MovementDate,
                    m.Type == EmployeeMovementType.Credit || m.Type == EmployeeMovementType.Bonus ? m.Credit : m.Debit,
                    m.Currency,
                    m.CashVoucher != null ? m.CashVoucher.VoucherNumber : $"MOV-{m.Id}",
                    m.Notes))
                .ToListAsync(cancellationToken);

            var recentSalaryTransfers = await dbContext.EmployeeOpeningBalances
                .AsNoTracking()
                .Where(b =>
                    b.CompanyId == companyId &&
                    b.FiscalYearId == fiscalYear.FiscalYearId &&
                    b.PayrollEntryId.HasValue)
                .OrderByDescending(b => b.DocumentDate)
                .ThenByDescending(b => b.Id)
                .Take(10)
                .Select(b => new PayrollDashboardRecentOperationResponse(
                    b.Id,
                    "SalaryTransfer",
                    "تحويل راتب مسير",
                    b.EmployeeId,
                    b.Employee.Code,
                    b.Employee.Name,
                    b.DocumentDate,
                    b.Amount,
                    b.Currency,
                    b.DocumentNumber,
                    b.Notes))
                .ToListAsync(cancellationToken);

            var recentOpeningBalances = await dbContext.EmployeeOpeningBalances
                .AsNoTracking()
                .Where(b =>
                    b.CompanyId == companyId &&
                    b.FiscalYearId == fiscalYear.FiscalYearId &&
                    !b.PayrollEntryId.HasValue)
                .OrderByDescending(b => b.DocumentDate)
                .ThenByDescending(b => b.Id)
                .Take(5)
                .Select(b => new PayrollDashboardRecentOperationResponse(
                    b.Id,
                    "OpeningBalance",
                    b.BalanceType == EmployeeBalanceType.Credit ? "رصيد دائن افتتاحي" : "رصيد مدين افتتاحي",
                    b.EmployeeId,
                    b.Employee.Code,
                    b.Employee.Name,
                    b.DocumentDate,
                    b.Amount,
                    b.Currency,
                    b.DocumentNumber,
                    b.Notes))
                .ToListAsync(cancellationToken);

            var recentOperations = recentMovements
                .Concat(recentSalaryTransfers)
                .Concat(recentOpeningBalances)
                .OrderByDescending(r => r.Date)
                .Take(15)
                .ToList();

            var totalDeductions = (payrollStats?.TotalDeduction ?? 0m) + (movementStats?.TotalMovementDeductions ?? 0m);

            var response = new PayrollDashboardResponse(
                FiscalYearId:    fiscalYear.FiscalYearId,
                FiscalYearName:  fiscalYear.FiscalYearName,
                TotalPayrolls:   payrollStats?.TotalGross ?? 0m,
                NetPayable:      payrollStats?.TotalNet ?? 0m,
                TotalPaid:       payrollStats?.TotalMoved ?? 0m,
                TotalDeductions: totalDeductions,
                TotalDebits:     movementStats?.TotalDebits ?? 0m,
                EmployeeCount:   totalEmployeeCount,
                PendingPayrolls: pendingPayrolls,
                RecentOperations: recentOperations);

            return Result<PayrollDashboardResponse>.Success(response);
        }

        public async Task<Result<PayrollEntryResponse>> AddOutCompanyAsync(
            OutCompanyPayrollEntryRequest request,
            CancellationToken cancellationToken = default)
        {
            var employee = await dbContext.Employees
                .FirstOrDefaultAsync(
                    e => e.Id == request.EmployeeId && e.CompanyId == companyId,
                    cancellationToken);

            if (employee is null)
                return Result<PayrollEntryResponse>.Failure(
                    Error.NotFound("Employee.NotFound", "الموظف المحدد غير موجود."));

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
                fiscalYearId: null,
                cancellationToken);
            if (fiscalYearGuard.IsFailure)
                return Result<PayrollEntryResponse>.Failure(
                    fiscalYearGuard.Errors);

            var existingEntries = await dbContext.PayrollEntries
                .AsNoTracking()
                .Where(p => p.CompanyId == companyId &&
                            p.EmployeeId == employee.Id &&
                            p.Employee.WorkPlaceStatus == WorkPlaceStatus.OutCompany)
                .Select(p => new PayrollPeriodRecord(p.Id, p.EmployeeId, p.StartDate, p.EndDate))
                .ToListAsync(cancellationToken);

            var periodError = PayrollEntryBusinessRules.ValidateOutCompanyPayrollPeriod(
                startDate,
                endDate,
                existingEntries,
                currentEntryId: null,
                employeeName: employee.Name);

            if (periodError is not null)
                return Result<PayrollEntryResponse>.Failure(periodError);

            var totalDays = PayrollEntryBusinessRules.CalculateTotalDays(startDate, endDate);
            var presentDaysError = PayrollEntryBusinessRules.ValidatePresentDays(request.PresentDays, totalDays);
            if (presentDaysError is not null)
                return Result<PayrollEntryResponse>.Failure(presentDaysError);

            var absentDays = PayrollEntryBusinessRules.CalculateAbsentDays(totalDays, request.PresentDays);

            var workedDaysUnit = request.WorkedDaysByDayUnit;
            var overtimeUnit = request.OvertimeByDayUnit ?? 0m;
            var deductionUnit = request.DeductionByDayUnit ?? 0m;

            var workedUnits = workedDaysUnit + overtimeUnit - deductionUnit;

            var calc = PayrollCalculator.Calculate(
                employee,
                workedUnits,
                request.Bonus,
                request.Deduction);

            if (calc.GrossSalary < 0)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("Employee.SalaryRequired", "يجب تحديد الراتب أو اليومية للموظف."));

            var entry = new PayrollEntry
            {
                StartDate                       = startDate,
                EndDate                         = endDate,
                CompanyId                       = companyId,
                EmployeeId                      = request.EmployeeId,
                EmployeeCode                    = employee.Code,
                EmployeeName                    = employee.Name,
                EmployeeType                    = employee.Type,
                PresentDays                     = request.PresentDays,
                AbsentDays                      = absentDays,
                WorkedDaysbydayunit             = workedDaysUnit,
                Overtimebydayunit               = overtimeUnit,
                Deductionbydayunit              = deductionUnit,
                RequiredWorkingDays             = employee.Type == EmployeeType.Daily ? null : employee.RequiredWorkingDaysPerMonth,
                SalaryPerDay                    = calc.SalaryPerDay,
                Bonus                           = request.Bonus,
                Deduction                       = request.Deduction,
                GrossSalary                     = calc.GrossSalary,
                CalculatedSalary                = calc.CalculatedSalary,
                NetSalary                       = calc.NetSalary,
                IsSalaryMoveToEmployeeAccount   = false,
                SalaryMovedOn                   = null
            };

            dbContext.PayrollEntries.Add(entry);
            await dbContext.SaveChangesAsync(cancellationToken);

            entry.Employee = employee;
            return Result<PayrollEntryResponse>.Success(MapToResponse(entry));
        }

        public async Task<Result<List<PayrollEntryResponse>>> AddOutCompanyBulkAsync(
            BulkOutCompanyPayrollEntryRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Entries is null || request.Entries.Count == 0)
                return Result<List<PayrollEntryResponse>>.Failure(
                    Error.Validation("PayrollEntry.EmptyBulkRequest", "يجب إرسال مدخل راتب واحد على الأقل."));

            var duplicateEmployees = request.Entries
                .GroupBy(e => e.EmployeeId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateEmployees.Count > 0)
                return Result<List<PayrollEntryResponse>>.Failure(
                    Error.Validation(
                        "PayrollEntry.DuplicateEmployee",
                        $"لا يجوز تكرار نفس الموظف داخل الطلب الواحد: {string.Join(", ", duplicateEmployees)}"));

            var employeeIds = request.Entries.Select(e => e.EmployeeId).Distinct().ToList();
            var employees = await dbContext.Employees
                .Where(e => e.CompanyId == companyId && employeeIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, cancellationToken);

            if (employees.Count != employeeIds.Count)
            {
                var missingIds = employeeIds.Where(id => !employees.ContainsKey(id)).ToList();
                return Result<List<PayrollEntryResponse>>.Failure(
                    Error.NotFound("Employee.NotFound",
                        $"بعض الموظفين المحددين غير موجودين: {string.Join(", ", missingIds)}"));
            }

            var ineligibleEmployees = employees.Values
                .Where(e => !e.IsActive || e.WorkPlaceStatus != WorkPlaceStatus.OutCompany)
                .Select(e => e.Name)
                .ToList();

            if (ineligibleEmployees.Count > 0)
            {
                return Result<List<PayrollEntryResponse>>.Failure(
                    Error.Validation(
                        "PayrollEntry.EmployeeNotEligible",
                        $"لا يمكن إنشاء مسير رواتب خارج الشركة إلا للموظفين النشطين خارج الشركة. الموظفون غير المؤهلين: {string.Join(", ", ineligibleEmployees)}"));
            }

            var dateRanges = new Dictionary<int, (DateOnly StartDate, DateOnly EndDate)>();

            foreach (var item in request.Entries)
            {
                var emp = employees[item.EmployeeId];
                var startDate = item.StartDate ?? request.DefaultStartDate ?? default;
                var endDate = item.EndDate ?? request.DefaultEndDate ?? default;

                var dateError = PayrollEntryBusinessRules.ValidateOutCompanyDates(startDate, endDate, emp.Name);
                if (dateError is not null)
                    return Result<List<PayrollEntryResponse>>.Failure(dateError);

                dateRanges[item.EmployeeId] = (startDate, endDate);
            }

            foreach (var range in dateRanges.Values)
            {
                var fiscalYearGuard = await EnsurePayrollPeriodOpenAsync(
                    range.StartDate,
                    range.EndDate,
                    fiscalYearId: null,
                    cancellationToken);
                if (fiscalYearGuard.IsFailure)
                    return Result<List<PayrollEntryResponse>>.Failure(
                        fiscalYearGuard.Errors);
            }

            var existingEntries = await dbContext.PayrollEntries
                .AsNoTracking()
                .Where(p => p.CompanyId == companyId &&
                            employeeIds.Contains(p.EmployeeId) &&
                            p.Employee.WorkPlaceStatus == WorkPlaceStatus.OutCompany)
                .Select(p => new PayrollPeriodRecord(p.Id, p.EmployeeId, p.StartDate, p.EndDate))
                .ToListAsync(cancellationToken);

            var existingByEmployee = existingEntries
                .GroupBy(p => p.EmployeeId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<PayrollPeriodRecord>)g.ToList());

            foreach (var item in request.Entries)
            {
                var emp = employees[item.EmployeeId];
                var (startDate, endDate) = dateRanges[item.EmployeeId];

                existingByEmployee.TryGetValue(item.EmployeeId, out var empExisting);
                var periodError = PayrollEntryBusinessRules.ValidateOutCompanyPayrollPeriod(
                    startDate,
                    endDate,
                    empExisting ?? Array.Empty<PayrollPeriodRecord>(),
                    currentEntryId: null,
                    employeeName: emp.Name);

                if (periodError is not null)
                    return Result<List<PayrollEntryResponse>>.Failure(periodError);

                var totalDays = PayrollEntryBusinessRules.CalculateTotalDays(startDate, endDate);
                var presentDaysError = PayrollEntryBusinessRules.ValidatePresentDays(item.PresentDays, totalDays);
                if (presentDaysError is not null)
                    return Result<List<PayrollEntryResponse>>.Failure(presentDaysError);
            }

            var entries = new List<PayrollEntry>(request.Entries.Count);

            foreach (var item in request.Entries)
            {
                var emp = employees[item.EmployeeId];
                var (startDate, endDate) = dateRanges[item.EmployeeId];

                var totalDays = PayrollEntryBusinessRules.CalculateTotalDays(startDate, endDate);
                var absentDays = PayrollEntryBusinessRules.CalculateAbsentDays(totalDays, item.PresentDays);

                var workedDaysUnit = item.WorkedDaysByDayUnit;
                var overtimeUnit = item.OvertimeByDayUnit ?? 0m;
                var deductionUnit = item.DeductionByDayUnit ?? 0m;

                var workedUnits = workedDaysUnit + overtimeUnit - deductionUnit;

                var calc = PayrollCalculator.Calculate(
                    emp,
                    workedUnits,
                    item.Bonus,
                    item.Deduction);

                if (calc.GrossSalary < 0)
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Validation("Employee.SalaryRequired",
                            $"يجب تحديد الراتب أو اليومية للموظف {emp.Name}."));

                var entry = new PayrollEntry
                {
                    StartDate                       = startDate,
                    EndDate                         = endDate,
                    CompanyId                       = companyId,
                    EmployeeId                      = item.EmployeeId,
                    EmployeeCode                    = emp.Code,
                    EmployeeName                    = emp.Name,
                    EmployeeType                    = emp.Type,
                    PresentDays                     = item.PresentDays,
                    AbsentDays                      = absentDays,
                    WorkedDaysbydayunit             = workedDaysUnit,
                    Overtimebydayunit               = overtimeUnit,
                    Deductionbydayunit              = deductionUnit,
                    RequiredWorkingDays             = emp.Type == EmployeeType.Daily ? null : emp.RequiredWorkingDaysPerMonth,
                    SalaryPerDay                    = calc.SalaryPerDay,
                    Bonus                           = item.Bonus,
                    Deduction                       = item.Deduction,
                    GrossSalary                     = calc.GrossSalary,
                    CalculatedSalary                = calc.CalculatedSalary,
                    NetSalary                       = calc.NetSalary,
                    IsSalaryMoveToEmployeeAccount   = false,
                    SalaryMovedOn                   = null
                };
                entry.Employee = emp;
                entries.Add(entry);
            }

            dbContext.PayrollEntries.AddRange(entries);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<List<PayrollEntryResponse>>.Success(entries.Select(MapToResponse).ToList());
        }
    }
}
