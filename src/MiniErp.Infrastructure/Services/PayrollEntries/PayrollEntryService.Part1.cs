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
        public async Task<Result<PagedResponse<PayrollEntriesListResponse>>> GetAllAsync(
            PaginationRequest pagination,
            PayrollEntryFilterRequest? filters = null,
            CancellationToken cancellationToken = default)
        {
            filters ??= new PayrollEntryFilterRequest();
            var validationError = ValidateFilters(filters, cancellationToken);
            if (validationError != null)
                return Result<PagedResponse<PayrollEntriesListResponse>>.Failure(validationError);

            var fiscalYearResult = await fiscalYearQueryScopeResolver.ResolveAsync(
                filters.FiscalYearId,
                filters.StartDate,
                filters.EndDate,
                cancellationToken);
            if (fiscalYearResult.IsFailure)
                return Result<PagedResponse<PayrollEntriesListResponse>>.Failure(
                    fiscalYearResult.Errors);

            var baseQuery = dbContext.PayrollEntries
                .AsNoTracking()
                .Where(e =>
                    e.CompanyId == companyId &&
                    e.FiscalYearId == fiscalYearResult.Value.FiscalYearId);

            var sorted = ApplyFilters(baseQuery, filters);

            return await paginationService.PaginateAsync<PayrollEntry, PayrollEntriesListResponse>(
                sorted,
                pagination,
                cancellationToken);
        }

        public async Task<Result<PayrollEntryResponse>> GetByIdAsync(
            int id,
            int? fiscalYearId = null,
            CancellationToken cancellationToken = default)
        {
            var fiscalYearResult = await fiscalYearQueryScopeResolver.ResolveAsync(
                fiscalYearId,
                cancellationToken: cancellationToken);
            if (fiscalYearResult.IsFailure)
                return Result<PayrollEntryResponse>.Failure(fiscalYearResult.Errors);

            var entry = await dbContext.PayrollEntries
                .AsNoTracking()
                .Include(e => e.Employee)
                .FirstOrDefaultAsync(
                    e => e.Id == id &&
                         e.CompanyId == companyId &&
                         e.FiscalYearId == fiscalYearResult.Value.FiscalYearId,
                    cancellationToken);

            if (entry is null)
                return Result<PayrollEntryResponse>.Failure(
                    Error.NotFound(
                        "PayrollEntry.NotFound",
                        "لم يتم العثور على قيد الراتب المطلوب."));

            return Result<PayrollEntryResponse>.Success(MapToResponse(entry));
        }

        // ─── ADD (IN COMPANY) ───────────────────────────────────────────────────

        public async Task<Result<PayrollEntryResponse>> AddAsync(
            PayrollEntryCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            var validationError = ValidateAddAsync(request, cancellationToken);
            if (validationError != null)
                return Result<PayrollEntryResponse>.Failure(validationError);

            var employee = await dbContext.Employees
                .FirstOrDefaultAsync(
                    e => e.Id == request.EmployeeId && e.CompanyId == companyId,
                    cancellationToken);

            if (employee is null)
                return Result<PayrollEntryResponse>.Failure(
                    Error.NotFound("Employee.NotFound", "الموظف المحدد غير موجود."));

            if (!employee.IsActive)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("Employee.Inactive", "لا يمكن إنشاء مسير رواتب لموظف غير نشط."));

            if (employee.WorkPlaceStatus == WorkPlaceStatus.OutCompany)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation(
                        "PayrollEntry.OutCompanyNotAllowedHere",
                        "الموظف يعمل خارج الشركة، يرجى استخدام نقطة نهاية مسير رواتب موظفي خارج الشركة."));

            if (employee.WorkPlaceStatus != WorkPlaceStatus.InCompany)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation(
                        "PayrollEntry.InvalidWorkPlaceStatus",
                        "يجب تحديد حالة مكان العمل للموظف كـ داخل الشركة لإنشاء مسير رواتب عادي."));

            var endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.Now);
            var startDate = employee.GetEffectiveStartDate(endDate);

            if (startDate > endDate)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation(
                        "PayrollEntry.InvalidDateRange",
                        "تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية."));

            var fiscalYearGuard = await EnsurePayrollPeriodOpenAsync(
                startDate,
                endDate,
                fiscalYearId: null,
                cancellationToken);
            if (fiscalYearGuard.IsFailure)
                return Result<PayrollEntryResponse>.Failure(
                    fiscalYearGuard.Errors);

            var hasOverlap = await dbContext.PayrollEntries
                .AnyAsync(p => p.CompanyId == companyId &&
                               p.EmployeeId == employee.Id &&
                               p.StartDate <= endDate &&
                               p.EndDate >= startDate,
                          cancellationToken);
            if (hasOverlap)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Conflict(
                        "PayrollEntry.PeriodOverlap",
                        $"يوجد مسير رواتب مسجل مسبقًا للموظف {employee.Name} يغطي أو يتداخل مع هذه الفترة ({startDate:yyyy-MM-dd} إلى {endDate:yyyy-MM-dd})."));

            var attendanceSummary = await GetAttendanceSummaryAsync(
                employee.Id, companyId, startDate, endDate, cancellationToken);

            var (grossSalary, calculatedSalary, netSalary, salaryPerDay) = CalculateSalaryWithExtras(
                employee,
                attendanceSummary,
                request.Bonus,
                request.Deduction);

            if (grossSalary < 0)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("Employee.SalaryRequired",
                        "يجب تحديد الراتب أو اليومية للموظف."));

            var entry = new PayrollEntry
            {
                StartDate                       = startDate,
                EndDate                         = endDate,
                CompanyId                       = companyId,
                EmployeeId                      = request.EmployeeId,
                EmployeeCode                    = employee.Code,
                EmployeeName                    = employee.Name,
                EmployeeType                    = employee.Type,
                PresentDays                     = attendanceSummary.PresentDays,
                AbsentDays                      = attendanceSummary.AbsentDays,
                WorkedDaysbydayunit             = attendanceSummary.TotalPresentDays,
                Overtimebydayunit               = attendanceSummary.TotalOvertimeDays,
                Deductionbydayunit              = attendanceSummary.TotalDeductionDays,
                RequiredWorkingDays             = employee.Type == EmployeeType.Daily ? null : employee.RequiredWorkingDaysPerMonth,
                SalaryPerDay                    = salaryPerDay,
                Bonus                           = request.Bonus,
                Deduction                       = request.Deduction,
                GrossSalary                     = grossSalary,
                CalculatedSalary                = calculatedSalary,
                NetSalary                       = netSalary,
                IsSalaryMoveToEmployeeAccount   = false,
                SalaryMovedOn                   = null
            };

            dbContext.PayrollEntries.Add(entry);
            await dbContext.SaveChangesAsync(cancellationToken);

            entry.Employee = employee;
            return Result<PayrollEntryResponse>.Success(MapToResponse(entry));
        }

        // ─── ADD BULK (IN COMPANY) ──────────────────────────────────────────────

        public async Task<Result<List<PayrollEntryResponse>>> AddBulkAsync(
            BulkPayrollEntryCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            var validationError = ValidateAddBulkAsync(request);
            if (validationError is not null)
                return Result<List<PayrollEntryResponse>>.Failure(validationError);

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
                .Where(e => !e.IsActive || e.WorkPlaceStatus != WorkPlaceStatus.InCompany)
                .Select(e => e.Name)
                .ToList();

            if (ineligibleEmployees.Count > 0)
            {
                return Result<List<PayrollEntryResponse>>.Failure(
                    Error.Validation(
                        "PayrollEntry.EmployeeNotEligible",
                        $"لا يمكن إنشاء مسير رواتب عادي إلا للموظفين النشطين داخل الشركة. الموظفون غير المؤهلين: {string.Join(", ", ineligibleEmployees)}"));
            }

            var today = DateOnly.FromDateTime(DateTime.Now);
            var dateRanges = new Dictionary<int, (DateOnly StartDate, DateOnly EndDate)>();

            foreach (var item in request.Entries)
            {
                var emp = employees[item.EmployeeId];
                var endDate = item.EndDate ?? request.DefaultEndDate ?? today;
                var startDate = emp.GetEffectiveStartDate(endDate);

                if (startDate > endDate)
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Validation("PayrollEntry.InvalidDateRange",
                            $"تاريخ البداية للموظف {emp.Name} ({startDate}) يجب أن يكون قبل أو يساوي تاريخ النهاية ({endDate})."));

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

            var minStartDate = dateRanges.Values.Min(r => r.StartDate);
            var maxEndDate   = dateRanges.Values.Max(r => r.EndDate);

            var existingOverlaps = await dbContext.PayrollEntries
                .AsNoTracking()
                .Where(p => p.CompanyId == companyId &&
                            employeeIds.Contains(p.EmployeeId) &&
                            p.StartDate <= maxEndDate &&
                            p.EndDate >= minStartDate)
                .Select(p => new { p.EmployeeId, p.StartDate, p.EndDate })
                .ToListAsync(cancellationToken);

            foreach (var item in request.Entries)
            {
                var (s, e) = dateRanges[item.EmployeeId];
                if (existingOverlaps.Any(x => x.EmployeeId == item.EmployeeId && x.StartDate <= e && x.EndDate >= s))
                {
                    var emp = employees[item.EmployeeId];
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Conflict("PayrollEntry.PeriodOverlap",
                            $"يوجد مسير رواتب مسجل مسبقًا للموظف {emp.Name} يغطي أو يتداخل مع الفترة المحددة."));
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
            var entries = new List<PayrollEntry>(request.Entries.Count);

            foreach (var item in request.Entries)
            {
                var emp = employees[item.EmployeeId];
                var (startDate, endDate) = dateRanges[item.EmployeeId];

                var empAttendances = attendanceLookup[emp.Id]
                    .Where(a => a.WorkDate >= startDate && a.WorkDate <= endDate)
                    .ToList();

                var summary = new AttendanceSummary(
                    PresentDays:        empAttendances.Count(a => a.Status == EmployeeAttendanceStatus.Present),
                    AbsentDays:         empAttendances.Count(a => a.Status == EmployeeAttendanceStatus.Absent),
                    TotalPresentDays:   empAttendances.Where(a => a.Status == EmployeeAttendanceStatus.Present).Sum(a => GetRatioValue(a.WorkDayRatio)),
                    TotalOvertimeDays:  empAttendances.Where(a => a.Status == EmployeeAttendanceStatus.Present).Sum(a => GetRatioValue(a.WorkOverTimeRatio)),
                    TotalDeductionDays: empAttendances.Where(a => a.Status == EmployeeAttendanceStatus.Present).Sum(a => GetRatioValue(a.WorkDaysDeductionRatio)));

                var (grossSalary, calculatedSalary, netSalary, salaryPerDay) = CalculateSalaryWithExtras(
                    emp,
                    summary,
                    item.Bonus,
                    item.Deduction);

                if (grossSalary < 0)
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
                    PresentDays                     = summary.PresentDays,
                    AbsentDays                      = summary.AbsentDays,
                    WorkedDaysbydayunit             = summary.TotalPresentDays,
                    Overtimebydayunit               = summary.TotalOvertimeDays,
                    Deductionbydayunit              = summary.TotalDeductionDays,
                    RequiredWorkingDays             = emp.Type == EmployeeType.Daily ? null : emp.RequiredWorkingDaysPerMonth,
                    SalaryPerDay                    = salaryPerDay,
                    Bonus                           = item.Bonus,
                    Deduction                       = item.Deduction,
                    GrossSalary                     = grossSalary,
                    CalculatedSalary                = calculatedSalary,
                    NetSalary                       = netSalary,
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

        public async Task<Result<PayrollEntryResponse>> MoveSalaryForEmployeeAccountAsync(
            int id,
            PayrollEntrySalaryPaymentRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await dbContext.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var entry = await dbContext.PayrollEntries
                .Include(e => e.Employee)
                .FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == companyId, cancellationToken);

            if (entry is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PayrollEntryResponse>.Failure(
                    Error.NotFound("PayrollEntry.NotFound", "لم يتم العثور على قيد الرواتب المطلوب."));
            }

            var employee = entry.Employee ?? await dbContext.Employees
                .FirstOrDefaultAsync(e => e.Id == entry.EmployeeId && e.CompanyId == companyId, cancellationToken);

            if (employee is null || employee.CompanyId != companyId)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PayrollEntryResponse>.Failure(
                    Error.NotFound("Employee.NotFound", "الموظف المحدد غير موجود في الشركة الحالية."));
            }
            entry.Employee = employee;

            var guardError = ValidateForPayment(entry);
            if (guardError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PayrollEntryResponse>.Failure(guardError);
            }

            var payrollFiscalYearGuard = await EnsurePayrollPeriodOpenAsync(
                entry.StartDate,
                entry.EndDate,
                entry.FiscalYearId,
                cancellationToken);
            if (payrollFiscalYearGuard.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PayrollEntryResponse>.Failure(
                    Error.Conflict(
                        "PayrollEntry.ClosedFiscalYearPaymentRequiresSettlement",
                        "لا يمكن تعديل مسير رواتب يخص سنة مالية مغلقة. يلزم تسوية الرصيد المرحل في السنة الحالية."));
            }

            if (fiscalYearPeriodGuard is not null)
            {
                var fiscalYearResult = await fiscalYearPeriodGuard
                    .EnsureOpenAsync(
                        request.PostingDate,
                        nameof(PayrollEntrySalaryPaymentRequest.PostingDate),
                        cancellationToken);
                if (fiscalYearResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<PayrollEntryResponse>.Failure(
                        fiscalYearResult.Errors);
                }
            }

            // Check if this payroll entry or another payroll entry for the same employee + period has already been transferred
            var isAlreadyTransferred = await dbContext.EmployeeOpeningBalances
                .AnyAsync(b =>
                    b.CompanyId == companyId &&
                    b.EmployeeId == entry.EmployeeId &&
                    b.PayrollEntryId.HasValue &&
                    (b.PayrollEntryId == id ||
                     (b.PayrollEntry!.StartDate == entry.StartDate && b.PayrollEntry.EndDate == entry.EndDate)),
                    cancellationToken);

            if (isAlreadyTransferred)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PayrollEntryResponse>.Failure(
                    Error.Conflict(
                        "PayrollEntry.AlreadyPaid",
                        $"تم تحويل راتب الموظف {employee.Name} لهذه الفترة ({entry.StartDate:yyyy-MM-dd} إلى {entry.EndDate:yyyy-MM-dd}) إلى حسابه مسبقًا."));
            }

            var hasDuplicatePaidEntry = await dbContext.PayrollEntries
                .AnyAsync(p =>
                    p.CompanyId == companyId &&
                    p.EmployeeId == entry.EmployeeId &&
                    p.StartDate == entry.StartDate &&
                    p.EndDate == entry.EndDate &&
                    p.IsSalaryMoveToEmployeeAccount &&
                    p.Id != entry.Id,
                    cancellationToken);

            if (hasDuplicatePaidEntry)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PayrollEntryResponse>.Failure(
                    Error.Conflict(
                        "PayrollEntry.AlreadyPaid",
                        $"تم تحويل راتب الموظف {employee.Name} لهذه الفترة ({entry.StartDate:yyyy-MM-dd} إلى {entry.EndDate:yyyy-MM-dd}) إلى حسابه مسبقًا."));
            }

            var exchangeRateResult = await exchangeRateResolver.ResolveAsync(
                CurrencyCode.EGP,
                request.PostingDate,
                requestedRate: null,
                cancellationToken: cancellationToken);

            if (exchangeRateResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PayrollEntryResponse>.Failure(exchangeRateResult.Error);
            }

            // Only NetSalary affects the Employee Account credit
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
                    DocumentDate   = request.PostingDate,
                    Currency       = CurrencyCode.EGP,
                    BalanceType    = EmployeeBalanceType.Credit,
                    Amount         = entry.NetSalary,
                    Notes          = request.Notes ?? $"تحويل راتب مسير رواتب #{entry.Id} للفترة من {entry.StartDate:yyyy-MM-dd} إلى {entry.EndDate:yyyy-MM-dd}"
                };
                openingBalance.ApplyExchangeRate(
                    exchangeRateResult.Value.ExchangeRateId,
                    exchangeRateResult.Value.Rate);

                dbContext.EmployeeOpeningBalances.Add(openingBalance);
            }

            entry.IsSalaryMoveToEmployeeAccount = true;
            entry.SalaryMovedOn = request.PostingDate;

            // Atomically update LastDayOfReceivingSalary
            employee.UpdateLastDayOfReceivingSalary(entry.EndDate);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<PayrollEntryResponse>.Failure(
                    Error.Conflict(
                        "PayrollEntry.AlreadyPaid",
                        $"تم تحويل راتب القيد رقم {entry.Id} إلى حساب الموظف مسبقًا."));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                throw;
            }

            return Result<PayrollEntryResponse>.Success(MapToResponse(entry));
        }
    }
}
