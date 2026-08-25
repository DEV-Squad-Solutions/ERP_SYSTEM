using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.EmployeeTransactions;
using MiniErp.Application.Features.PayrollEntries;
using MiniErp.Domain.Entities.Payroll;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.PayrollEntries
{
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

        // ─── ADD ────────────────────────────────────────────────────────────────

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

            var startDate = request.StartDate
                ?? employee.LastDayOfReceivingSalary?.AddDays(1)
                ?? DateOnly.FromDateTime(employee.CreatedOn);
            var endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.Now);

            if (startDate > endDate)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation(
                        "PayrollEntry.InvalidDateRange",
                        "تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية."));

            var attendanceSummary = await GetAttendanceSummaryAsync(
                employee.Id, companyId, startDate, endDate, cancellationToken);

            var (grossSalary, calculatedSalary) = CalculateSalary(employee, attendanceSummary);
            if (grossSalary < 0)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("Employee.SalaryRequired",
                        "يجب تحديد الراتب أو اليومية للموظف."));

            decimal netSalary = calculatedSalary + (request.Bonus ?? 0) - (request.Deduction ?? 0);
            bool shouldAutoMove = request.IsSalaryMoveToEmployeeAccount ?? false;

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
                RequiredWorkingDays             = employee.RequiredWorkingDaysPerMonth,
                SalaryPerDay                    = employee.Type == EmployeeType.Monthly
                    ? ((employee.RequiredWorkingDaysPerMonth is > 0)
                        ? employee.MonthlySalary!.Value / employee.RequiredWorkingDaysPerMonth.Value
                        : employee.MonthlySalary!.Value)
                    : employee.DailySalary,
                Bonus                           = request.Bonus,
                Deduction                       = request.Deduction,
                GrossSalary                     = grossSalary,
                CalculatedSalary                = calculatedSalary,
                NetSalary                       = netSalary,
                IsSalaryMoveToEmployeeAccount   = shouldAutoMove
            };

            dbContext.PayrollEntries.Add(entry);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (shouldAutoMove)
            {
                var cashInfo = await ResolveCashboxAndMovementTypeAsync(
                    request.CashboxId,
                    request.CashMovementTypeId,
                    cancellationToken);

                if (cashInfo is not null)
                {
                    var creditResult = await employeeTransactionService.PostSalaryCreditAsync(
                        employeeId: entry.EmployeeId,
                        amount: entry.NetSalary,
                        payrollEntryId: entry.Id,
                        transactionDate: entry.EndDate,
                        cashboxId: cashInfo.Value.CashboxId,
                        cashMovementTypeId: cashInfo.Value.CashMovementTypeId,
                        cancellationToken: cancellationToken);

                    if (creditResult.IsSuccess)
                    {
                        employee.LastDayOfReceivingSalary = entry.EndDate;
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
            }

            return Result<PayrollEntryResponse>.Success(MapToResponse(entry));
        }

        // ─── ADD BULK ───────────────────────────────────────────────────────────

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

            var today = DateOnly.FromDateTime(DateTime.Now);
            var dateRanges = new Dictionary<int, (DateOnly StartDate, DateOnly EndDate)>();

            foreach (var item in request.Entries)
            {
                var emp = employees[item.EmployeeId];
                var startDate = item.StartDate
                    ?? (emp.LastDayOfReceivingSalary?.AddDays(1) 
                        ?? request.DefaultStartDate 
                        ?? DateOnly.FromDateTime(emp.CreatedOn));
                var endDate = item.EndDate ?? request.DefaultEndDate ?? today;

                if (startDate > endDate)
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Validation("PayrollEntry.InvalidDateRange",
                            $"تاريخ البداية للموظف {emp.Name} ({startDate}) يجب أن يكون قبل أو يساوي تاريخ النهاية ({endDate})."));

                dateRanges[item.EmployeeId] = (startDate, endDate);
            }

            var minStartDate = dateRanges.Values.Min(r => r.StartDate);
            var maxEndDate   = dateRanges.Values.Max(r => r.EndDate);

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
                    PresentDays:        empAttendances.Count(a => a.Status == AttendanceStatus.Present),
                    AbsentDays:         empAttendances.Count(a => a.Status == AttendanceStatus.Absent),
                    TotalPresentDays:   empAttendances.Where(a => a.Status == AttendanceStatus.Present).Sum(a => GetRatioValue(a.WorkDayRatio)),
                    TotalOvertimeDays:  empAttendances.Where(a => a.Status == AttendanceStatus.Present).Sum(a => GetRatioValue(a.WorkOverTimeRatio)),
                    TotalDeductionDays: empAttendances.Where(a => a.Status == AttendanceStatus.Present).Sum(a => GetRatioValue(a.WorkDaysDeductionRatio)));

                var (grossSalary, calculatedSalary) = CalculateSalary(emp, summary);
                if (grossSalary < 0)
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Validation("Employee.SalaryRequired",
                            $"يجب تحديد الراتب أو اليومية للموظف {emp.Name}."));

                var netSalary = calculatedSalary + (item.Bonus ?? 0) - (item.Deduction ?? 0);
                bool moveSalary = item.IsSalaryMoveToEmployeeAccount ?? request.DefaultIsSalaryMoveToEmployeeAccount ?? false;

                entries.Add(new PayrollEntry
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
                    RequiredWorkingDays             = emp.RequiredWorkingDaysPerMonth,
                    SalaryPerDay                    = emp.Type == EmployeeType.Monthly && emp.RequiredWorkingDaysPerMonth is > 0
                        ? emp.MonthlySalary!.Value / emp.RequiredWorkingDaysPerMonth.Value
                        : emp.DailySalary,
                    Bonus                           = item.Bonus,
                    Deduction                       = item.Deduction,
                    GrossSalary                     = grossSalary,
                    CalculatedSalary                = calculatedSalary,
                    NetSalary                       = netSalary,
                    IsSalaryMoveToEmployeeAccount   = moveSalary
                });
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            dbContext.PayrollEntries.AddRange(entries);
            await dbContext.SaveChangesAsync(cancellationToken);

            var autoMoveEntries = entries.Where(e => e.IsSalaryMoveToEmployeeAccount).ToList();
            if (autoMoveEntries.Count > 0)
            {
                var creditItems = new List<EmployeeSalaryCreditItem>();
                for (int i = 0; i < autoMoveEntries.Count; i++)
                {
                    var e = autoMoveEntries[i];
                    var reqItem = request.Entries.FirstOrDefault(r => r.EmployeeId == e.EmployeeId);
                    var cashInfo = await ResolveCashboxAndMovementTypeAsync(
                        reqItem?.CashboxId ?? request.DefaultCashboxId,
                        reqItem?.CashMovementTypeId ?? request.DefaultCashMovementTypeId,
                        cancellationToken);

                    if (cashInfo is not null)
                    {
                        creditItems.Add(new EmployeeSalaryCreditItem(
                            EmployeeId:         e.EmployeeId,
                            Amount:             e.NetSalary,
                            PayrollEntryId:     e.Id,
                            TransactionDate:    e.EndDate,
                            CashboxId:          cashInfo.Value.CashboxId,
                            CashMovementTypeId: cashInfo.Value.CashMovementTypeId,
                            Notes:              $"راتب مقيد تلقائيًا من مسير الرواتب رقم {e.Id}"));
                    }
                }

                if (creditItems.Count > 0)
                {
                    var creditResult = await employeeTransactionService.PostSalaryCreditBulkAsync(
                        items: creditItems,
                        cancellationToken: cancellationToken);

                    if (creditResult.IsSuccess)
                    {
                        foreach (var entry in autoMoveEntries)
                        {
                            if (employees.TryGetValue(entry.EmployeeId, out var emp))
                            {
                                emp.LastDayOfReceivingSalary = entry.EndDate;
                            }
                        }
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
            }

            await transaction.CommitAsync(cancellationToken);

            return Result<List<PayrollEntryResponse>>.Success(entries.Select(MapToResponse).ToList());
        }

        // ─── MOVE SALARY TO EMPLOYEE ACCOUNT ────────────────────────────────────

        public async Task<Result<PayrollEntryResponse>> MoveSalaryForEmployeeAccountAsync(
            int id,
            PayrollEntrySalaryPaymentRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

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

            var cashInfo = await ResolveCashboxAndMovementTypeAsync(
                request.CashboxId,
                request.CashMovementTypeId,
                cancellationToken);

            if (cashInfo is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("PayrollEntry.CashboxRequired", "يجب توفر صندوق نقدي نشط ونوع حركة لإتمام قيد الراتب."));
            }

            var creditResult = await employeeTransactionService.PostSalaryCreditAsync(
                employeeId:         entry.EmployeeId,
                amount:             entry.NetSalary,
                payrollEntryId:     entry.Id,
                transactionDate:    request.PostingDate,
                cashboxId:          cashInfo.Value.CashboxId,
                cashMovementTypeId: cashInfo.Value.CashMovementTypeId,
                cancellationToken:  cancellationToken);

            if (creditResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PayrollEntryResponse>.Failure(creditResult.Error);
            }

            entry.IsSalaryMoveToEmployeeAccount = true;
            if (entry.Employee is not null)
                entry.Employee.LastDayOfReceivingSalary = entry.EndDate;

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

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

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

            var creditItems = new List<EmployeeSalaryCreditItem>();
            foreach (var item in requestedItems)
            {
                var entry = entriesMap[item.PayrollEntryId];
                var reqEntry = request.Entries?.FirstOrDefault(r => r.PayrollEntryId == item.PayrollEntryId);
                var cashInfo = await ResolveCashboxAndMovementTypeAsync(
                    reqEntry?.CashboxId ?? request.DefaultCashboxId,
                    reqEntry?.CashMovementTypeId ?? request.DefaultCashMovementTypeId,
                    cancellationToken);

                if (cashInfo is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<List<PayrollEntryResponse>>.Failure(
                        Error.Validation("PayrollEntry.CashboxRequired", "يجب توفر صندوق نقدي نشط ونوع حركة لإتمام قيد الراتب."));
                }

                creditItems.Add(new EmployeeSalaryCreditItem(
                    EmployeeId:         entry.EmployeeId,
                    Amount:             entry.NetSalary,
                    PayrollEntryId:     entry.Id,
                    TransactionDate:    item.PostingDate,
                    CashboxId:          cashInfo.Value.CashboxId,
                    CashMovementTypeId: cashInfo.Value.CashMovementTypeId,
                    Notes:              item.Notes));
            }

            var creditResult = await employeeTransactionService.PostSalaryCreditBulkAsync(
                items: creditItems,
                cancellationToken: cancellationToken);

            if (creditResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<List<PayrollEntryResponse>>.Failure(creditResult.Error);
            }

            foreach (var entry in entries)
            {
                entry.IsSalaryMoveToEmployeeAccount = true;
                if (entry.Employee is not null)
                    entry.Employee.LastDayOfReceivingSalary = entry.EndDate;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<List<PayrollEntryResponse>>.Success(entries.Select(MapToResponse).ToList());
        }

        // ─── UPDATE ─────────────────────────────────────────────────────────────

        public async Task<Result<PayrollEntryResponse>> UpdateAsync(
            int id,
            PayrollEntryUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            var reqError = ValidateAddAsync(
                new PayrollEntryCreateRequest(
                    StartDate:                      request.StartDate,
                    EndDate:                        request.EndDate,
                    EmployeeId:                     request.EmployeeId,
                    Bonus:                          request.Bonus,
                    Deduction:                      request.Deduction,
                    IsSalaryMoveToEmployeeAccount:  request.IsSalaryMoveToEmployeeAccount),
                cancellationToken);

            if (reqError is not null)
                return Result<PayrollEntryResponse>.Failure(reqError);

            var entry = await dbContext.PayrollEntries
                .Include(e => e.Employee)
                .FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == companyId, cancellationToken);

            if (entry is null)
                return Result<PayrollEntryResponse>.Failure(
                    Error.NotFound("PayrollEntry.NotFound", "لم يتم العثور على قيد الراتب المطلوب."));

            var guardError = ValidateForUpdate(entry);
            if (guardError is not null)
                return Result<PayrollEntryResponse>.Failure(guardError);

            var employee = await dbContext.Employees
                .FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.CompanyId == companyId, cancellationToken);

            if (employee is null)
                return Result<PayrollEntryResponse>.Failure(
                    Error.NotFound("Employee.NotFound", "الموظف المحدد غير موجود."));

            var startDate = request.StartDate
                ?? employee.LastDayOfReceivingSalary?.AddDays(1)
                ?? DateOnly.FromDateTime(employee.CreatedOn);
            var endDate = request.EndDate ?? DateOnly.FromDateTime(DateTime.Now);

            if (startDate > endDate)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("PayrollEntry.InvalidDateRange",
                        "تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية."));

            var attendanceSummary = await GetAttendanceSummaryAsync(
                employee.Id, companyId, startDate, endDate, cancellationToken);

            var (grossSalary, calculatedSalary) = CalculateSalary(employee, attendanceSummary);
            if (grossSalary < 0)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("Employee.SalaryRequired",
                        "يجب تحديد الراتب أو اليومية للموظف."));

            var netSalary = calculatedSalary + (request.Bonus ?? 0) - (request.Deduction ?? 0);
            bool shouldAutoMove = request.IsSalaryMoveToEmployeeAccount ?? entry.IsSalaryMoveToEmployeeAccount;

            entry.EmployeeId                    = employee.Id;
            entry.EmployeeCode                  = employee.Code;
            entry.EmployeeName                  = employee.Name;
            entry.EmployeeType                  = employee.Type;
            entry.StartDate                     = startDate;
            entry.EndDate                       = endDate;
            entry.PresentDays                   = attendanceSummary.PresentDays;
            entry.AbsentDays                    = attendanceSummary.AbsentDays;
            entry.WorkedDaysbydayunit           = attendanceSummary.TotalPresentDays;
            entry.Overtimebydayunit             = attendanceSummary.TotalOvertimeDays;
            entry.Deductionbydayunit            = attendanceSummary.TotalDeductionDays;
            entry.RequiredWorkingDays           = employee.RequiredWorkingDaysPerMonth;
            entry.SalaryPerDay                  = employee.Type == EmployeeType.Monthly && employee.RequiredWorkingDaysPerMonth is > 0
                ? employee.MonthlySalary!.Value / employee.RequiredWorkingDaysPerMonth.Value
                : employee.DailySalary;
            entry.Bonus                         = request.Bonus;
            entry.Deduction                     = request.Deduction;
            entry.GrossSalary                   = grossSalary;
            entry.CalculatedSalary              = calculatedSalary;
            entry.NetSalary                     = netSalary;

            // Capture the flag BEFORE overwriting it — the guard below needs the old value.
            bool wasAlreadyMoved = entry.IsSalaryMoveToEmployeeAccount;
            entry.IsSalaryMoveToEmployeeAccount = shouldAutoMove;

            await dbContext.SaveChangesAsync(cancellationToken);

            // Only post an auto-credit when the flag is being enabled for the first time.
            if (shouldAutoMove && !wasAlreadyMoved)
            {
                var cashInfo = await ResolveCashboxAndMovementTypeAsync(
                    request.CashboxId,
                    request.CashMovementTypeId,
                    cancellationToken);

                if (cashInfo is not null)
                {
                    var creditResult = await employeeTransactionService.PostSalaryCreditAsync(
                        employeeId:         entry.EmployeeId,
                        amount:             entry.NetSalary,
                        payrollEntryId:     entry.Id,
                        transactionDate:    entry.EndDate,
                        cashboxId:          cashInfo.Value.CashboxId,
                        cashMovementTypeId: cashInfo.Value.CashMovementTypeId,
                        cancellationToken:  cancellationToken);

                    if (creditResult.IsSuccess)
                    {
                        employee.LastDayOfReceivingSalary = entry.EndDate;
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
            }

            return Result<PayrollEntryResponse>.Success(MapToResponse(entry));
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

            var guardError = ValidateForUpdate(entry);
            if (guardError is not null)
                return Result<PayrollEntryResponse>.Failure(guardError);

            if (entry.Employee is null)
                return Result<PayrollEntryResponse>.Failure(
                    Error.NotFound("Employee.NotFound", "لم يتم العثور على بيانات الموظف المرتبطة بقيد الراتب."));

            var employee = entry.Employee;

            var attendanceSummary = await GetAttendanceSummaryAsync(
                employee.Id, companyId, entry.StartDate, entry.EndDate, cancellationToken);

            var (grossSalary, calculatedSalary) = CalculateSalary(employee, attendanceSummary);
            if (grossSalary < 0)
                return Result<PayrollEntryResponse>.Failure(
                    Error.Validation("Employee.SalaryRequired",
                        "يجب تحديد الراتب أو اليومية للموظف."));

            var netSalary = calculatedSalary + (entry.Bonus ?? 0) - (entry.Deduction ?? 0);

            entry.PresentDays                   = attendanceSummary.PresentDays;
            entry.AbsentDays                    = attendanceSummary.AbsentDays;
            entry.WorkedDaysbydayunit           = attendanceSummary.TotalPresentDays;
            entry.Overtimebydayunit             = attendanceSummary.TotalOvertimeDays;
            entry.Deductionbydayunit            = attendanceSummary.TotalDeductionDays;
            entry.RequiredWorkingDays           = employee.RequiredWorkingDaysPerMonth;
            entry.SalaryPerDay                  = employee.Type == EmployeeType.Monthly && employee.RequiredWorkingDaysPerMonth is > 0
                ? employee.MonthlySalary!.Value / employee.RequiredWorkingDaysPerMonth.Value
                : employee.DailySalary;
            entry.GrossSalary                   = grossSalary;
            entry.CalculatedSalary              = calculatedSalary;
            entry.NetSalary                     = netSalary;

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

            if (entry.IsSalaryMoveToEmployeeAccount)
                return Result.Failure(
                    Error.Conflict("PayrollEntry.AlreadyPaid",
                        "لا يمكن حذف قيد راتب تم صرفه. يجب إلغاء سند الصرف أولًا."));

            dbContext.PayrollEntries.Remove(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
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
                NetSalary:                      entry.NetSalary,
                IsSalaryMoveToEmployeeAccount:  entry.IsSalaryMoveToEmployeeAccount,
                AttendanceSummary: new AttendanceSummary(
                    PresentDays:        entry.PresentDays,
                    AbsentDays:         entry.AbsentDays,
                    TotalPresentDays:   entry.WorkedDaysbydayunit,
                    TotalOvertimeDays:  entry.Overtimebydayunit,
                    TotalDeductionDays: entry.Deductionbydayunit));
    }
}