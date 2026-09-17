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
    public sealed partial class PayrollEntryService(
        ApplicationDbContext dbContext,
        IPaginationService paginationService,
        ICurrentCompanyContext currentCompanyContext,
        IExchangeRateResolver exchangeRateResolver)
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
            var validationError = ValidateFilters(filters, cancellationToken);
            if (validationError != null)
                return Result<PagedResponse<PayrollEntriesListResponse>>.Failure(validationError);

            var baseQuery = dbContext.PayrollEntries
                .AsNoTracking()
                .Where(e => e.CompanyId == companyId);

            var sorted = ApplyFilters(baseQuery, filters);

            return await paginationService.PaginateAsync<PayrollEntry, PayrollEntriesListResponse>(
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
                .Where(e => !e.IsActive || e.WorkPlaceStatus != WorkPlaceStatus.OutCompany)
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

        // ─── MOVE SALARY TO EMPLOYEE ACCOUNT ────────────────────────────────────

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

            var guardError = ValidateForPayment(entry);
            if (guardError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PayrollEntryResponse>.Failure(guardError);
            }

            var isAlreadyTransferred = await dbContext.EmployeeOpeningBalances
                .AnyAsync(b => b.CompanyId == companyId && b.PayrollEntryId == id, cancellationToken);
            if (isAlreadyTransferred)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PayrollEntryResponse>.Failure(
                    Error.Conflict("PayrollEntry.AlreadyPaid", $"تم تحويل راتب القيد رقم {entry.Id} إلى حساب الموظف مسبقًا."));
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

            // Only create a financial ledger entry when there is an actual positive amount.
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

            if (entry.Employee is not null)
                entry.Employee.UpdateLastDayOfReceivingSalary(entry.EndDate);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PayrollEntryResponse>.Success(MapToResponse(entry));
        }

        // ─── MOVE SALARY BULK ────────────────────────────────────────────────────

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
                var guardError = ValidateForPayment(entry);
                if (guardError is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<List<PayrollEntryResponse>>.Failure(guardError);
                }
            }

            var existingTransfers = await dbContext.EmployeeOpeningBalances
                .Where(b => b.CompanyId == companyId && b.PayrollEntryId.HasValue && entryIds.Contains(b.PayrollEntryId.Value))
                .Select(b => b.PayrollEntryId!.Value)
                .ToListAsync(cancellationToken);
            if (existingTransfers.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<List<PayrollEntryResponse>>.Failure(
                    Error.Conflict("PayrollEntry.AlreadyPaid",
                        $"تم تحويل راتب بعض القيود إلى حساب الموظف مسبقًا: {string.Join(", ", existingTransfers)}"));
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
                    entry.Employee.UpdateLastDayOfReceivingSalary(entry.EndDate);
            }

            dbContext.EmployeeOpeningBalances.AddRange(openingBalances);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

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

        // ─── RECALCULATE ─────────────────────────────────────────────────────────

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

        // ─── DELETE ─────────────────────────────────────────────────────────────

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

            dbContext.PayrollEntries.Remove(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        // ─── DELETE BULK ────────────────────────────────────────────────────────

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
            }

            dbContext.PayrollEntries.RemoveRange(entries);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        // ─── DASHBOARD ──────────────────────────────────────────────────────────

        public async Task<Result<PayrollDashboardResponse>> GetDashboardAsync(
            PayrollDashboardFilterRequest? filters = null,
            CancellationToken cancellationToken = default)
        {
            filters ??= new PayrollDashboardFilterRequest();

            var payrollQuery = dbContext.PayrollEntries
                .AsNoTracking()
                .Where(p => p.CompanyId == companyId);

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
                .Where(m => m.CompanyId == companyId);

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
                    TotalAdvances = g.Where(m => m.Type == EmployeeMovementType.Advance).Sum(m => m.Debit),
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
                    m.Id,
                    m.Type.ToString(),
                    m.Type == EmployeeMovementType.Advance ? "سلفة نقدية" :
                    m.Type == EmployeeMovementType.Withdrawal ? "مسحوبات نقدية" :
                    m.Type == EmployeeMovementType.Deduction ? "خصم مالي" :
                    m.Type == EmployeeMovementType.Bonus ? "مكافأة مالية" :
                    m.Type == EmployeeMovementType.Credit ? "حركة دائنة" : "حركة مدينة",
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
                .Where(b => b.CompanyId == companyId && b.PayrollEntryId.HasValue)
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
                .Where(b => b.CompanyId == companyId && !b.PayrollEntryId.HasValue)
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
                TotalPayrolls:   payrollStats?.TotalGross ?? 0m,
                NetPayable:      payrollStats?.TotalNet ?? 0m,
                TotalPaid:       payrollStats?.TotalMoved ?? 0m,
                TotalDeductions: totalDeductions,
                TotalAdvances:   movementStats?.TotalAdvances ?? 0m,
                EmployeeCount:   totalEmployeeCount,
                PendingPayrolls: pendingPayrolls,
                RecentOperations: recentOperations);

            return Result<PayrollDashboardResponse>.Success(response);
        }

        // ─── ADD OUT COMPANY ───────────────────────────────────────────────────

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

            if (!employee.IsActive)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("Employee.Inactive", "لا يمكن إنشاء مسير رواتب لموظف غير نشط."));

            if (employee.WorkPlaceStatus != WorkPlaceStatus.OutCompany)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation(
                        "PayrollEntry.NotOutCompany",
                        "مسير رواتب خارج الشركة مخصص فقط للموظفين الذين يعملون خارج الشركة."));

            if (request.StartDate == default)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("PayrollEntry.StartDateRequired", "تاريخ البداية مطلوب."));

            if (request.EndDate == default)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("PayrollEntry.EndDateRequired", "تاريخ النهاية مطلوب."));

            var startDate = request.StartDate;
            var endDate = request.EndDate;

            if (startDate > endDate)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation(
                        "PayrollEntry.InvalidDateRange",
                        "تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية."));

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
                AbsentDays                      = 0,
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

        // ─── ADD OUT COMPANY BULK ───────────────────────────────────────────────

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
                var startDate = item.StartDate ?? request.DefaultStartDate;
                if (!startDate.HasValue || startDate.Value == default)
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Validation("PayrollEntry.StartDateRequired",
                            $"تاريخ البداية مطلوب للموظف {emp.Name}."));

                var endDate = item.EndDate ?? request.DefaultEndDate;
                if (!endDate.HasValue || endDate.Value == default)
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Validation("PayrollEntry.EndDateRequired",
                            $"تاريخ النهاية مطلوب للموظف {emp.Name}."));

                if (startDate.Value > endDate.Value)
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Validation("PayrollEntry.InvalidDateRange",
                            $"تاريخ البداية للموظف {emp.Name} ({startDate.Value}) يجب أن يكون قبل أو يساوي تاريخ النهاية ({endDate.Value})."));

                dateRanges[item.EmployeeId] = (startDate.Value, endDate.Value);
            }

            var minStartDate = dateRanges.Values.Min(r => r.StartDate);
            var maxEndDate = dateRanges.Values.Max(r => r.EndDate);

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
                            $"يوجد مسير رواتب مسجل مسبقًا للموظف {emp.Name} يغطي أو يتداخل مع الفترة المحددة ({s:yyyy-MM-dd} إلى {e:yyyy-MM-dd})."));
                }
            }

            var entries = new List<PayrollEntry>(request.Entries.Count);

            foreach (var item in request.Entries)
            {
                var emp = employees[item.EmployeeId];
                var (startDate, endDate) = dateRanges[item.EmployeeId];

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
                    AbsentDays                      = 0,
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

        // ─── UPDATE OUT COMPANY ────────────────────────────────────────────────

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

            var employee = entry.Employee;
            if (employee is null)
                return Result<PayrollEntryResponse>.Failure(
                    Error.NotFound("Employee.NotFound", "بيانات الموظف غير موجودة."));

            if (!employee.IsActive)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("Employee.Inactive", "لا يمكن تعديل مسير رواتب لموظف غير نشط."));

            if (employee.WorkPlaceStatus != WorkPlaceStatus.OutCompany)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("PayrollEntry.NotOutCompany",
                        "مسير رواتب خارج الشركة مخصص فقط للموظفين الذين يعملون خارج الشركة."));

            if (request.StartDate == default)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("PayrollEntry.StartDateRequired", "تاريخ البداية مطلوب."));

            if (request.EndDate == default)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("PayrollEntry.EndDateRequired", "تاريخ النهاية مطلوب."));

            var startDate = request.StartDate;
            var endDate = request.EndDate;

            if (startDate > endDate)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("PayrollEntry.InvalidDateRange",
                        "تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية.",
                        nameof(request.StartDate)));

            var hasOverlap = await dbContext.PayrollEntries
                .AnyAsync(p => p.CompanyId == companyId &&
                               p.EmployeeId == entry.EmployeeId &&
                               p.Id != entry.Id &&
                               p.StartDate <= endDate &&
                               p.EndDate >= startDate,
                          cancellationToken);
            if (hasOverlap)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Conflict("PayrollEntry.PeriodOverlap", "الفترة المحددة تتداخل مع مسير رواتب آخر مسجل لنفس الموظف."));

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

        // ─── UPDATE OUT COMPANY BULK ───────────────────────────────────────────

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
                var startDate = item.StartDate ?? request.DefaultStartDate;
                if (!startDate.HasValue || startDate.Value == default)
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Validation("PayrollEntry.StartDateRequired",
                            $"تاريخ البداية مطلوب للقيد رقم {entry.Id}."));

                var endDate = item.EndDate ?? request.DefaultEndDate;
                if (!endDate.HasValue || endDate.Value == default)
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Validation("PayrollEntry.EndDateRequired",
                            $"تاريخ النهاية مطلوب للقيد رقم {entry.Id}."));

                if (startDate.Value > endDate.Value)
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Validation("PayrollEntry.InvalidDateRange",
                            $"تاريخ البداية للقيد رقم {entry.Id} ({startDate.Value}) يجب أن يكون قبل أو يساوي تاريخ النهاية ({endDate.Value})."));

                dateRanges[item.Id] = (startDate.Value, endDate.Value);
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
                var empId = entriesMap[item.Id].EmployeeId;
                if (existingOverlaps.Any(x => x.EmployeeId == empId && x.StartDate <= e && x.EndDate >= s))
                {
                    var emp = entriesMap[item.Id].Employee!;
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Conflict("PayrollEntry.PeriodOverlap",
                            $"تاريخ القيد للموظف {emp.Name} ({s:yyyy-MM-dd} إلى {e:yyyy-MM-dd}) يتداخل مع مسير رواتب مسجل مسبقًا."));
                }
            }

            await using var transaction = await dbContext.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            foreach (var item in request.Entries)
            {
                var entry = entriesMap[item.Id];
                var emp = entry.Employee!;
                var (startDate, endDate) = dateRanges[item.Id];

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

        // ─── MAPPING ────────────────────────────────────────────────────────────

        private static PayrollEntryResponse MapToResponse(PayrollEntry entry) =>
            new(
                Id:                             entry.Id,
                CompanyId:                      entry.CompanyId,
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
