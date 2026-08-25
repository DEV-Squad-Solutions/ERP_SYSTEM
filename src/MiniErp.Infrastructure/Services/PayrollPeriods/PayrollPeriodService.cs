using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.PayrollPeriods;
using MiniErp.Domain.Entities.Payroll;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.PayrollPeriods;

public sealed class PayrollPeriodService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : IPayrollPeriodService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    // ─── GET ALL ────────────────────────────────────────────────────────────

    public async Task<Result<PagedResponse<PayrollPeriodListResponse>>> GetAllAsync(
        PaginationRequest pagination,
        PayrollPeriodFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new PayrollPeriodFilterRequest();

        var query = dbContext.PayrollPeriods
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId);

        if (filters.StartDate.HasValue)
            query = query.Where(p => p.StartDate >= filters.StartDate.Value);

        if (filters.EndDate.HasValue)
            query = query.Where(p => p.EndDate <= filters.EndDate.Value);

        if (filters.Status.HasValue)
            query = query.Where(p => p.Status == filters.Status.Value);

        var search = filters.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                p.Code.Contains(search) ||
                (p.Name != null && p.Name.Contains(search)));

        var ordered = query
            .OrderByDescending(p => p.StartDate)
            .ThenByDescending(p => p.Id);

        return await paginationService.PaginateAsync<PayrollPeriod, PayrollPeriodListResponse>(
            ordered,
            pagination,
            cancellationToken);
    }

    // ─── GET BY ID ──────────────────────────────────────────────────────────

    public async Task<Result<PayrollPeriodResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var period = await dbContext.PayrollPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId, cancellationToken);

        if (period is null)
            return Result<PayrollPeriodResponse>.Failure(
                Error.NotFound("PayrollPeriod.NotFound", "لم يتم العثور على فترة الرواتب المطلوبة."));

        return Result<PayrollPeriodResponse>.Success(MapToResponse(period));
    }

    // ─── CREATE ─────────────────────────────────────────────────────────────

    public async Task<Result<PayrollPeriodResponse>> CreateAsync(
        PayrollPeriodCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.StartDate > request.EndDate)
            return Result<PayrollPeriodResponse>.Failure(
                Error.Validation("PayrollPeriod.InvalidDateRange", "تاريخ البدء يجب أن يكون قبل أو يساوي تاريخ الانتهاء."));

        var overlapping = await dbContext.PayrollPeriods
            .AnyAsync(p => p.CompanyId == companyId &&
                           p.StartDate <= request.EndDate &&
                           p.EndDate >= request.StartDate,
                      cancellationToken);

        if (overlapping)
            return Result<PayrollPeriodResponse>.Failure(
                Error.Conflict("PayrollPeriod.OverlappingDates", "توجد فترة رواتب أخرى تتداخل مع هذه التواريخ."));

        var periodName = !string.IsNullOrWhiteSpace(request.Name)
            ? request.Name.Trim()
            : $"فترة رواتب من {request.StartDate:yyyy-MM-dd} إلى {request.EndDate:yyyy-MM-dd}";

        var period = new PayrollPeriod
        {
            CompanyId = companyId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            WorkingDaysInPeriod = request.WorkingDaysInPeriod,
            Name = periodName,
            Status = PayrollPeriodStatus.Draft
        };

        dbContext.PayrollPeriods.Add(period);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<PayrollPeriodResponse>.Success(MapToResponse(period));
    }

    // ─── UPDATE ─────────────────────────────────────────────────────────────

    public async Task<Result<PayrollPeriodResponse>> UpdateAsync(
        int id,
        PayrollPeriodUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.StartDate > request.EndDate)
            return Result<PayrollPeriodResponse>.Failure(
                Error.Validation("PayrollPeriod.InvalidDateRange", "تاريخ البدء يجب أن يكون قبل أو يساوي تاريخ الانتهاء."));

        var period = await dbContext.PayrollPeriods
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId, cancellationToken);

        if (period is null)
            return Result<PayrollPeriodResponse>.Failure(
                Error.NotFound("PayrollPeriod.NotFound", "لم يتم العثور على فترة الرواتب المطلوبة."));

        if (period.Status == PayrollPeriodStatus.Paid)
            return Result<PayrollPeriodResponse>.Failure(
                Error.Conflict("PayrollPeriod.AlreadyPaid", "لا يمكن تعديل فترة رواتب مكتملة مدفوعة."));

        var overlapping = await dbContext.PayrollPeriods
            .AnyAsync(p => p.CompanyId == companyId &&
                           p.Id != id &&
                           p.StartDate <= request.EndDate &&
                           p.EndDate >= request.StartDate,
                      cancellationToken);

        if (overlapping)
            return Result<PayrollPeriodResponse>.Failure(
                Error.Conflict("PayrollPeriod.OverlappingDates", "توجد فترة رواتب أخرى تتداخل مع هذه التواريخ."));

        period.StartDate = request.StartDate;
        period.EndDate = request.EndDate;
        period.WorkingDaysInPeriod = request.WorkingDaysInPeriod;

        if (!string.IsNullOrWhiteSpace(request.Name))
            period.Name = request.Name.Trim();

        if (request.Status.HasValue)
            period.Status = request.Status.Value;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<PayrollPeriodResponse>.Success(MapToResponse(period));
    }

    // ─── DELETE ─────────────────────────────────────────────────────────────

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var period = await dbContext.PayrollPeriods
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId, cancellationToken);

        if (period is null)
            return Result.Failure(
                Error.NotFound("PayrollPeriod.NotFound", "لم يتم العثور على فترة الرواتب المطلوبة."));

        if (period.Status == PayrollPeriodStatus.Paid)
            return Result.Failure(
                Error.Conflict("PayrollPeriod.AlreadyPaid", "لا يمكن حذف فترة رواتب تم صرف رواتبها."));

        dbContext.PayrollPeriods.Remove(period);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    // ─── CALCULATE PERIOD ───────────────────────────────────────────────────

    public async Task<Result<PayrollPeriodResponse>> CalculatePeriodAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var period = await dbContext.PayrollPeriods
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId, cancellationToken);

        if (period is null)
            return Result<PayrollPeriodResponse>.Failure(
                Error.NotFound("PayrollPeriod.NotFound", "لم يتم العثور على فترة الرواتب المطلوبة."));

        var entries = await dbContext.PayrollEntries
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId &&
                        e.StartDate >= period.StartDate &&
                        e.EndDate <= period.EndDate)
            .ToListAsync(cancellationToken);

        var employeeIds = entries.Select(e => e.EmployeeId).Distinct().ToList();

        period.TotalEmployees = employeeIds.Count;
        period.TotalMonthlyEmployees = entries.Count(e => e.EmployeeType == EmployeeType.Monthly);
        period.TotalDailyEmployees = entries.Count(e => e.EmployeeType == EmployeeType.Daily);
        period.TotalGrossSalary = entries.Sum(e => e.GrossSalary);
        period.TotalNetSalary = entries.Sum(e => e.NetSalary);
        period.TotalWorkedDays = entries.Sum(e => e.WorkedDaysbydayunit);
        period.TotalOvertimeDays = entries.Sum(e => e.Overtimebydayunit ?? 0m);
        period.TotalAbsentDays = entries.Sum(e => e.AbsentDays);
        period.CalculatedAt = DateTime.UtcNow;
        period.Status = PayrollPeriodStatus.Calculated;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<PayrollPeriodResponse>.Success(MapToResponse(period));
    }

    // ─── REPORT BY PERIOD ───────────────────────────────────────────────────

    public async Task<Result<PayrollPeriodReportResponse>> GetReportByPeriodAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var period = await dbContext.PayrollPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId, cancellationToken);

        if (period is null)
            return Result<PayrollPeriodReportResponse>.Failure(
                Error.NotFound("PayrollPeriod.NotFound", "لم يتم العثور على فترة الرواتب المطلوبة."));

        return await BuildReportAsync(
            periodId: period.Id,
            periodCode: period.Code,
            periodName: period.Name,
            periodStatus: period.Status,
            startDate: period.StartDate,
            endDate: period.EndDate,
            cancellationToken: cancellationToken);
    }

    // ─── REPORT BY DATE RANGE ───────────────────────────────────────────────

    public async Task<Result<PayrollPeriodReportResponse>> GetReportByDateRangeAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            return Result<PayrollPeriodReportResponse>.Failure(
                Error.Validation("PayrollPeriod.InvalidDateRange", "تاريخ البدء يجب أن يكون قبل أو يساوي تاريخ الانتهاء."));

        return await BuildReportAsync(
            periodId: null,
            periodCode: null,
            periodName: $"تقرير رواتب من {startDate:yyyy-MM-dd} إلى {endDate:yyyy-MM-dd}",
            periodStatus: null,
            startDate: startDate,
            endDate: endDate,
            cancellationToken: cancellationToken);
    }

    // ─── HELPER: BUILD REPORT ───────────────────────────────────────────────

    private async Task<Result<PayrollPeriodReportResponse>> BuildReportAsync(
        int? periodId,
        string? periodCode,
        string? periodName,
        PayrollPeriodStatus? periodStatus,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var entries = await dbContext.PayrollEntries
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId &&
                        e.StartDate >= startDate &&
                        e.EndDate <= endDate)
            .OrderBy(e => e.EmployeeName)
            .ToListAsync(cancellationToken);

        var employeeLines = entries.Select(e => new PayrollPeriodEmployeeReportLine(
            PayrollEntryId: e.Id,
            EmployeeId: e.EmployeeId,
            EmployeeCode: e.EmployeeCode,
            EmployeeName: e.EmployeeName,
            EmployeeType: e.EmployeeType,
            StartDate: e.StartDate,
            EndDate: e.EndDate,
            PresentDays: e.PresentDays,
            AbsentDays: e.AbsentDays,
            WorkedUnits: e.WorkedDaysbydayunit,
            OvertimeUnits: e.Overtimebydayunit,
            DeductionUnits: e.Deductionbydayunit,
            GrossSalary: e.GrossSalary,
            CalculatedSalary: e.CalculatedSalary,
            Bonus: e.Bonus,
            Deduction: e.Deduction,
            NetSalary: e.NetSalary,
            IsPaid: e.IsSalaryMoveToEmployeeAccount
        )).ToList();

        var paidEntries = entries.Where(e => e.IsSalaryMoveToEmployeeAccount).ToList();
        var pendingEntries = entries.Where(e => !e.IsSalaryMoveToEmployeeAccount).ToList();

        var summary = new PayrollPeriodReportSummary(
            TotalEntries: entries.Count,
            TotalEmployees: entries.Select(e => e.EmployeeId).Distinct().Count(),
            MonthlyEmployeeCount: entries.Count(e => e.EmployeeType == EmployeeType.Monthly),
            DailyEmployeeCount: entries.Count(e => e.EmployeeType == EmployeeType.Daily),
            TotalGrossSalary: entries.Sum(e => e.GrossSalary),
            TotalCalculatedSalary: entries.Sum(e => e.CalculatedSalary),
            TotalBonus: entries.Sum(e => e.Bonus ?? 0m),
            TotalDeduction: entries.Sum(e => e.Deduction ?? 0m),
            TotalNetSalary: entries.Sum(e => e.NetSalary),
            TotalPresentDays: entries.Sum(e => (decimal)e.PresentDays),
            TotalAbsentDays: entries.Sum(e => (decimal)e.AbsentDays),
            TotalWorkedUnits: entries.Sum(e => e.WorkedDaysbydayunit),
            TotalOvertimeUnits: entries.Sum(e => e.Overtimebydayunit ?? 0m),
            TotalDeductionUnits: entries.Sum(e => e.Deductionbydayunit ?? 0m),
            PaidCount: paidEntries.Count,
            PendingCount: pendingEntries.Count,
            PaidAmount: paidEntries.Sum(e => e.NetSalary),
            PendingAmount: pendingEntries.Sum(e => e.NetSalary));

        var response = new PayrollPeriodReportResponse(
            PeriodId: periodId,
            PeriodCode: periodCode,
            PeriodName: periodName,
            PeriodStatus: periodStatus,
            StartDate: startDate,
            EndDate: endDate,
            Summary: summary,
            Employees: employeeLines);

        return Result<PayrollPeriodReportResponse>.Success(response);
    }

    // ─── MAPPING HELPER ─────────────────────────────────────────────────────

    private static PayrollPeriodResponse MapToResponse(PayrollPeriod p) =>
        new(
            Id: p.Id,
            CompanyId: p.CompanyId,
            Code: p.Code,
            Name: p.Name,
            StartDate: p.StartDate,
            EndDate: p.EndDate,
            Status: p.Status,
            WorkingDaysInPeriod: p.WorkingDaysInPeriod,
            TotalEmployees: p.TotalEmployees,
            TotalMonthlyEmployees: p.TotalMonthlyEmployees,
            TotalDailyEmployees: p.TotalDailyEmployees,
            TotalGrossSalary: p.TotalGrossSalary,
            TotalCredits: p.TotalCredits,
            TotalDebits: p.TotalDebits,
            TotalNetSalary: p.TotalNetSalary,
            TotalWorkedDays: p.TotalWorkedDays,
            TotalOvertimeDays: p.TotalOvertimeDays,
            TotalAbsentDays: p.TotalAbsentDays,
            CalculatedAt: p.CalculatedAt,
            PaidAt: p.PaidAt);
}
