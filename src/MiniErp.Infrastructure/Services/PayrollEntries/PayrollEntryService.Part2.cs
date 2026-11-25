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
        public async Task<Result<List<PayrollEntryResponse>>> MoveSalaryForEmployeeAccountBulkAsync(
            BulkPayrollEntrySalaryPaymentRequest request,
            CancellationToken cancellationToken = default)
        {
            var validationError = ValidateBulkPaymentAsync(request);
            if (validationError is not null)
                return Result<List<PayrollEntryResponse>>.Failure(validationError);

            var today = DateOnly.FromDateTime(DateTime.Now);
            var requestedItems = new List<(int PayrollEntryId, DateOnly PostingDate, string? Notes)>();

            if (request.Entries is { Count: > 0 })
            {
                foreach (var item in request.Entries)
                {
                    requestedItems.Add((
                        item.PayrollEntryId,
                        item.PostingDate ?? request.DefaultPostingDate ?? today,
                        !string.IsNullOrWhiteSpace(item.Notes) ? item.Notes : request.Notes));
                }
            }
            else if (request.PayrollEntryIds is { Count: > 0 })
            {
                var postingDate = request.DefaultPostingDate ?? today;
                foreach (var entryId in request.PayrollEntryIds)
                    requestedItems.Add((entryId, postingDate, request.Notes));
            }

            var entryIds = requestedItems.Select(x => x.PayrollEntryId).Distinct().ToList();

            await using var transaction = await dbContext.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var entries = await dbContext.PayrollEntries
                .Include(e => e.Employee)
                .Where(e => e.CompanyId == companyId && entryIds.Contains(e.Id))
                .ToListAsync(cancellationToken);

            var entriesMap = entries.ToDictionary(e => e.Id);

            if (entries.Count != entryIds.Count)
            {
                var missingIds = entryIds.Where(id => !entriesMap.ContainsKey(id)).ToList();
                await transaction.RollbackAsync(cancellationToken);
                return Result<List<PayrollEntryResponse>>.Failure(
                    Error.NotFound("PayrollEntry.NotFound",
                        $"بعض قيود الرواتب المحددة غير موجودة: {string.Join(", ", missingIds)}"));
            }

            foreach (var entry in entries)
            {
                if (entry.Employee is null || entry.Employee.CompanyId != companyId)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.NotFound("Employee.NotFound", $"الموظف المحدد للقيد رقم {entry.Id} غير موجود في الشركة الحالية."));
                }

                var guardError = ValidateForPayment(entry);
                if (guardError is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<List<PayrollEntryResponse>>.Failure(guardError);
                }

                var payrollFiscalYearGuard = await EnsurePayrollPeriodOpenAsync(
                    entry.StartDate,
                    entry.EndDate,
                    entry.FiscalYearId,
                    cancellationToken);
                if (payrollFiscalYearGuard.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Conflict(
                            "PayrollEntry.ClosedFiscalYearPaymentRequiresSettlement",
                            "لا يمكن تعديل مسير رواتب يخص سنة مالية مغلقة. يلزم تسوية الرصيد المرحل في السنة الحالية."));
                }
            }

            if (fiscalYearPeriodGuard is not null)
            {
                foreach (var item in requestedItems)
                {
                    var fiscalYearResult = await fiscalYearPeriodGuard
                        .EnsureOpenAsync(
                            item.PostingDate,
                            nameof(PayrollEntrySalaryPaymentRequest.PostingDate),
                            cancellationToken);
                    if (fiscalYearResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<List<PayrollEntryResponse>>.Failure(
                            fiscalYearResult.Errors);
                    }
                }
            }

            var employeeIds = entries.Select(e => e.EmployeeId).Distinct().ToList();
            var existingTransfers = await dbContext.EmployeeOpeningBalances
                .Where(b => b.CompanyId == companyId &&
                            b.PayrollEntryId.HasValue &&
                            entryIds.Contains(b.PayrollEntryId.Value))
                .Select(b => b.PayrollEntryId!.Value)
                .ToListAsync(cancellationToken);

            if (existingTransfers.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<List<PayrollEntryResponse>>.Failure(
                    Error.Conflict("PayrollEntry.AlreadyPaid",
                        $"تم تحويل راتب بعض القيود إلى حساب الموظف مسبقًا: {string.Join(", ", existingTransfers)}"));
            }

            var existingPeriodTransfers = await dbContext.EmployeeOpeningBalances
                .Where(b => b.CompanyId == companyId &&
                            b.PayrollEntryId.HasValue &&
                            employeeIds.Contains(b.EmployeeId))
                .Select(b => new
                {
                    b.EmployeeId,
                    StartDate = (DateOnly?)b.PayrollEntry!.StartDate,
                    EndDate = (DateOnly?)b.PayrollEntry.EndDate,
                    PayrollEntryId = b.PayrollEntryId!.Value
                })
                .ToListAsync(cancellationToken);

            var duplicatePeriodEntry = entries.FirstOrDefault(e =>
                existingPeriodTransfers.Any(b =>
                    b.EmployeeId == e.EmployeeId &&
                    b.StartDate == e.StartDate &&
                    b.EndDate == e.EndDate &&
                    b.PayrollEntryId != e.Id));

            if (duplicatePeriodEntry is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<List<PayrollEntryResponse>>.Failure(
                    Error.Conflict("PayrollEntry.AlreadyPaid",
                        $"تم تحويل راتب الموظف {duplicatePeriodEntry.EmployeeName} للفترة ({duplicatePeriodEntry.StartDate:yyyy-MM-dd} إلى {duplicatePeriodEntry.EndDate:yyyy-MM-dd}) إلى حسابه مسبقًا."));
            }

            var openingBalances = new List<EmployeeOpeningBalance>(requestedItems.Count);

            foreach (var item in requestedItems)
            {
                var entry = entriesMap[item.PayrollEntryId];

                var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
                    CurrencyCode.EGP,
                    item.PostingDate,
                    requestedRate: null,
                    cancellationToken: cancellationToken);

                if (exchangeRateResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<List<PayrollEntryResponse>>.Failure(exchangeRateResult.Error);
                }

                if (entry.NetSalary > 0)
                {
                    var documentNumber = await EntityIdentifierGenerator.GenerateUniqueAsync(
                        dbContext,
                        prefix: "EOB",
                        companyId: companyId,
                        existingIdentifiers: dbContext.EmployeeOpeningBalances
                            .IgnoreQueryFilters()
                            .Where(e => e.CompanyId == companyId)
                            .Select(e => e.DocumentNumber),
                        cancellationToken);

                    var openingBalance = new EmployeeOpeningBalance
                    {
                        CompanyId      = companyId,
                        EmployeeId     = entry.EmployeeId,
                        PayrollEntryId = entry.Id,
                        DocumentNumber = documentNumber,
                        DocumentDate   = item.PostingDate,
                        Currency       = CurrencyCode.EGP,
                        BalanceType    = EmployeeBalanceType.Credit,
                        Amount         = entry.NetSalary,
                        Notes          = item.Notes ?? $"تحويل راتب مسير رواتب #{entry.Id} للفترة من {entry.StartDate:yyyy-MM-dd} إلى {entry.EndDate:yyyy-MM-dd}"
                    };
                    openingBalance.ApplyExchangeRate(
                        exchangeRateResult.Value.ExchangeRateId,
                        exchangeRateResult.Value.Rate);

                    openingBalances.Add(openingBalance);
                }

                entry.IsSalaryMoveToEmployeeAccount = true;
                entry.SalaryMovedOn = item.PostingDate;

                if (entry.Employee is not null)
                {
                    entry.Employee.UpdateLastDayOfReceivingSalary(entry.EndDate);
                }
                else
                {
                    var employee = await dbContext.Employees
                        .FirstOrDefaultAsync(e => e.Id == entry.EmployeeId && e.CompanyId == companyId, cancellationToken);
                    employee?.UpdateLastDayOfReceivingSalary(entry.EndDate);
                }
            }

            dbContext.EmployeeOpeningBalances.AddRange(openingBalances);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<List<PayrollEntryResponse>>.Failure(
                    Error.Conflict(
                        "PayrollEntry.AlreadyPaid",
                        "تم تحويل راتب بعض القيود إلى حساب الموظف مسبقًا."));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                throw;
            }

            return Result<List<PayrollEntryResponse>>.Success(entries.Select(MapToResponse).ToList());
        }

        // ─── UPDATE (IN COMPANY) ────────────────────────────────────────────────

        public async Task<Result<PayrollEntryResponse>> UpdateAsync(
            int id,
            PayrollEntryUpdateRequest request,
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

            if (request.EmployeeId.HasValue && request.EmployeeId.Value != entry.EmployeeId)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("PayrollEntry.CannotChangeEmployee", "لا يمكن تغيير الموظف لقيد راتب مسجل مسبقًا."));

            var employee = entry.Employee;
            if (employee is null)
                return Result<PayrollEntryResponse>.Failure(
                    Error.NotFound("Employee.NotFound", "بيانات الموظف غير موجودة."));

            if (!employee.IsActive)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("Employee.Inactive", "لا يمكن تعديل مسير رواتب لموظف غير نشط."));

            if (employee.WorkPlaceStatus == WorkPlaceStatus.OutCompany)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("PayrollEntry.OutCompanyNotAllowedHere",
                        "الموظف يعمل خارج الشركة، يرجى استخدام نقطة نهاية مسير رواتب موظفي خارج الشركة."));

            var newEndDate = request.EndDate ?? entry.EndDate;
            var startDate = employee.GetEffectiveStartDate(newEndDate);

            if (startDate > newEndDate)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("PayrollEntry.InvalidDateRange",
                        "تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية.",
                        nameof(request.EndDate)));

            var fiscalYearGuard = await EnsurePayrollPeriodOpenAsync(
                startDate,
                newEndDate,
                entry.FiscalYearId,
                cancellationToken);
            if (fiscalYearGuard.IsFailure)
                return Result<PayrollEntryResponse>.Failure(
                    fiscalYearGuard.Errors);

            var hasOverlap = await dbContext.PayrollEntries
                .AnyAsync(p => p.CompanyId == companyId &&
                               p.EmployeeId == entry.EmployeeId &&
                               p.Id != entry.Id &&
                               p.StartDate <= newEndDate &&
                               p.EndDate >= startDate,
                          cancellationToken);
            if (hasOverlap)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Conflict("PayrollEntry.PeriodOverlap", "مسير الرواتب يتداخل مع مسير رواتب آخر مسجل لنفس الموظف."));

            var attendanceSummary = await GetAttendanceSummaryAsync(
                employee.Id, companyId, startDate, newEndDate, cancellationToken);

            var bonus = request.Bonus ?? entry.Bonus;
            var deduction = request.Deduction ?? entry.Deduction;

            var (grossSalary, calculatedSalary, netSalary, salaryPerDay) = CalculateSalaryWithExtras(
                employee,
                attendanceSummary,
                bonus,
                deduction);

            if (grossSalary < 0)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("Employee.SalaryRequired",
                        "يجب تحديد الراتب أو اليومية للموظف."));

            entry.StartDate                     = startDate;
            entry.EndDate                       = newEndDate;
            entry.PresentDays                   = attendanceSummary.PresentDays;
            entry.AbsentDays                    = attendanceSummary.AbsentDays;
            entry.WorkedDaysbydayunit           = attendanceSummary.TotalPresentDays;
            entry.Overtimebydayunit             = attendanceSummary.TotalOvertimeDays;
            entry.Deductionbydayunit            = attendanceSummary.TotalDeductionDays;
            entry.RequiredWorkingDays           = employee.Type == EmployeeType.Daily ? null : employee.RequiredWorkingDaysPerMonth;
            entry.SalaryPerDay                  = salaryPerDay;
            entry.Bonus                         = bonus;
            entry.Deduction                     = deduction;
            entry.GrossSalary                   = grossSalary;
            entry.CalculatedSalary              = calculatedSalary;
            entry.NetSalary                     = netSalary;

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<PayrollEntryResponse>.Success(MapToResponse(entry));
        }

        // ─── UPDATE BULK (IN COMPANY) ───────────────────────────────────────────

        public async Task<Result<List<PayrollEntryResponse>>> UpdateBulkAsync(
            BulkPayrollEntryUpdateRequest request,
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

            var ineligible = entries.Where(e => e.Employee == null || !e.Employee.IsActive || e.Employee.WorkPlaceStatus != WorkPlaceStatus.InCompany)
                .Select(e => e.EmployeeName)
                .ToList();
            if (ineligible.Count > 0)
            {
                return Result<List<PayrollEntryResponse>>.Failure(
                    Error.Validation("PayrollEntry.EmployeeNotEligible",
                        $"بعض الموظفين غير مؤهلين للتعديل داخل الشركة: {string.Join(", ", ineligible)}"));
            }

            var today = DateOnly.FromDateTime(DateTime.Now);
            var dateRanges = new Dictionary<int, (DateOnly StartDate, DateOnly EndDate)>();

            foreach (var item in request.Entries)
            {
                var entry = entriesMap[item.Id];
                var emp = entry.Employee!;
                var newEndDate = item.EndDate ?? request.DefaultEndDate ?? entry.EndDate;
                var startDate = emp.GetEffectiveStartDate(newEndDate);

                if (startDate > newEndDate)
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Validation("PayrollEntry.InvalidDateRange",
                            $"تاريخ البداية للموظف {emp.Name} ({startDate}) يجب أن يكون قبل أو يساوي تاريخ النهاية ({newEndDate})."));

                dateRanges[item.Id] = (startDate, newEndDate);
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

            var minStartDate = dateRanges.Values.Min(r => r.StartDate);
            var maxEndDate = dateRanges.Values.Max(r => r.EndDate);
            var employeeIds = entries.Select(e => e.EmployeeId).Distinct().ToList();

            var existingOverlaps = await dbContext.PayrollEntries
                .AsNoTracking()
                .Where(p => p.CompanyId == companyId &&
                            employeeIds.Contains(p.EmployeeId) &&
                            !ids.Contains(p.Id) &&
                            p.StartDate <= maxEndDate &&
                            p.EndDate >= minStartDate)
                .Select(p => new { p.EmployeeId, p.StartDate, p.EndDate })
                .ToListAsync(cancellationToken);

            foreach (var item in request.Entries)
            {
                var (s, e) = dateRanges[item.Id];
                if (existingOverlaps.Any(x => x.EmployeeId == entriesMap[item.Id].EmployeeId && x.StartDate <= e && x.EndDate >= s))
                {
                    var emp = entriesMap[item.Id].Employee!;
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Conflict("PayrollEntry.PeriodOverlap",
                            $"يوجد مسير رواتب مسجل مسبقًا للموظف {emp.Name} يغطي أو يتداخل مع الفترة المحددة ({s:yyyy-MM-dd} إلى {e:yyyy-MM-dd})."));
                }
            }

            var attendances = await dbContext.EmployeeAttendances
                .AsNoTracking()
                .Where(a =>
                    a.CompanyId == companyId &&
                    employeeIds.Contains(a.EmployeeId) &&
                    a.WorkDate >= minStartDate &&
                    a.WorkDate <= maxEndDate)
                .Select(a => new
                {
                    a.EmployeeId,
                    a.WorkDate,
                    a.Status,
                    a.WorkDayRatio,
                    a.WorkOverTimeRatio,
                    a.WorkDaysDeductionRatio
                })
                .ToListAsync(cancellationToken);

            var attendanceLookup = attendances.ToLookup(a => a.EmployeeId);

            await using var transaction = await dbContext.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            foreach (var item in request.Entries)
            {
                var entry = entriesMap[item.Id];
                var emp = entry.Employee!;
                var (startDate, endDate) = dateRanges[item.Id];

                var empAttendances = attendanceLookup[emp.Id]
                    .Where(a => a.WorkDate >= startDate && a.WorkDate <= endDate)
                    .ToList();

                var summary = new AttendanceSummary(
                    PresentDays:        empAttendances.Count(a => a.Status == EmployeeAttendanceStatus.Present),
                    AbsentDays:         empAttendances.Count(a => a.Status == EmployeeAttendanceStatus.Absent),
                    TotalPresentDays:   empAttendances.Where(a => a.Status == EmployeeAttendanceStatus.Present).Sum(a => GetRatioValue(a.WorkDayRatio)),
                    TotalOvertimeDays:  empAttendances.Where(a => a.Status == EmployeeAttendanceStatus.Present).Sum(a => GetRatioValue(a.WorkOverTimeRatio)),
                    TotalDeductionDays: empAttendances.Where(a => a.Status == EmployeeAttendanceStatus.Present).Sum(a => GetRatioValue(a.WorkDaysDeductionRatio)));

                var bonus = item.Bonus ?? entry.Bonus;
                var deduction = item.Deduction ?? entry.Deduction;

                var (grossSalary, calculatedSalary, netSalary, salaryPerDay) = CalculateSalaryWithExtras(
                    emp,
                    summary,
                    bonus,
                    deduction);

                if (grossSalary < 0)
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Validation("Employee.SalaryRequired",
                            $"يجب تحديد الراتب أو اليومية للموظف {emp.Name}."));

                entry.StartDate                     = startDate;
                entry.EndDate                       = endDate;
                entry.PresentDays                   = summary.PresentDays;
                entry.AbsentDays                    = summary.AbsentDays;
                entry.WorkedDaysbydayunit           = summary.TotalPresentDays;
                entry.Overtimebydayunit             = summary.TotalOvertimeDays;
                entry.Deductionbydayunit            = summary.TotalDeductionDays;
                entry.RequiredWorkingDays           = emp.Type == EmployeeType.Daily ? null : emp.RequiredWorkingDaysPerMonth;
                entry.SalaryPerDay                  = salaryPerDay;
                entry.Bonus                         = bonus;
                entry.Deduction                     = deduction;
                entry.GrossSalary                   = grossSalary;
                entry.CalculatedSalary              = calculatedSalary;
                entry.NetSalary                     = netSalary;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<List<PayrollEntryResponse>>.Success(entries.Select(MapToResponse).ToList());
        }
    }
}
