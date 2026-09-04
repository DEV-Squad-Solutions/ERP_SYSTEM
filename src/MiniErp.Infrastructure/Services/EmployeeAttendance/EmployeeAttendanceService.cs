using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.EmployeeAttendance;
using MiniErp.Application.Features.Employees;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.EmployeeAttendance;

public  sealed partial class EmployeeAttendanceService(
    ApplicationDbContext dbContext,
    IPaginationService paginationService,
    ICurrentCompanyContext currentCompanyContext)
    : IEmployeeAttendanceService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<PagedResponse<EmployeeAttendanceResponse>>> GetAllAsync(
        PaginationRequest pagination,
        EmployeeAttendanceFilterRequest? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= new EmployeeAttendanceFilterRequest();
        var validationError = ValidateFilters(filters, cancellationToken);
        if (validationError is not null)
        {
            return Result<PagedResponse<EmployeeAttendanceResponse>>.Failure(validationError);
        }

        var query = dbContext.EmployeeAttendances
            .AsNoTracking()
            .Where(a => a.CompanyId == companyId);

        if (filters.EmployeeId.HasValue)
        {
            query = query.Where(a => a.EmployeeId == filters.EmployeeId);
        }

        if (filters.WorkDateFrom.HasValue)
        {
            query = query.Where(a => a.WorkDate >= filters.WorkDateFrom);
        }

        if (filters.WorkDateTo.HasValue)
        {
            query = query.Where(a => a.WorkDate <= filters.WorkDateTo);
        }

        if (filters.Status.HasValue)
        {
            query = query.Where(a => a.Status == filters.Status);
        }

        var search = filters.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(a =>
                a.Employee.Name.Contains(search) ||
                a.Employee.Code.Contains(search));
        }

        var orderedQuery = query
            .OrderByDescending(a => a.WorkDate)
            .ThenByDescending(a => a.Id);

        var pageResult = await paginationService.PaginateAsync<
            Domain.Entities.Employees.EmployeeAttendance,
            EmployeeAttendanceResponse>(
            orderedQuery,
            pagination,
            cancellationToken);

        if (pageResult.IsFailure)
        {
            return pageResult;
        }

        return pageResult;
    }

    public async Task<Result<EmployeeAttendanceResponse>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Result<EmployeeAttendanceResponse>.Failure(
                Error.Validation(
                    "EmployeeAttendance.InvalidId",
                    "معرف سجل الحضور غير صالح."));
        }

        var attendance = await dbContext.EmployeeAttendances
            .AsNoTracking()
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(
                employeeAttendance => employeeAttendance.Id == id && employeeAttendance.CompanyId == companyId,
                cancellationToken);

        if (attendance is null)
        {
            return Result<EmployeeAttendanceResponse>.Failure(
                Error.NotFound(
                    "EmployeeAttendance.NotFound",
                    "لم يتم العثور على سجل الحضور المطلوب."));
        }

        return Result<EmployeeAttendanceResponse>.Success(
            new EmployeeAttendanceResponse(
                Id: attendance.Id,
                CompanyId: attendance.CompanyId,
                EmployeeId: attendance.EmployeeId,
                EmployeeName: attendance.Employee.Name,
                Status: attendance.Status,
                WorkDate: attendance.WorkDate,
                CheckIn: attendance.CheckIn,
                CheckOut: attendance.CheckOut,
                WorkHours: attendance.WorkHours,
                WorkDayRatio: attendance.WorkDayRatio,
                WorkOverTimeRatio: attendance.WorkOverTimeRatio,
                WorkDaysDeductionRatio: attendance.WorkDaysDeductionRatio,
                WorkLocation: attendance.WorkLocation,
                Notes: attendance.Notes));
    }

    public async Task<Result<EmployeeAttendanceResponse>> AddAsync(
        EmployeeAttendanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateAddAsync(request, cancellationToken);
        if (validationError != null)
        {
            return Result<EmployeeAttendanceResponse>.Failure(validationError);
        }

        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.CompanyId == companyId, cancellationToken);

        if (employee is null)
        {
            return Result<EmployeeAttendanceResponse>.Failure(
                Error.NotFound(
                    "Employee.NotFound",
                    "الموظف المحدد غير موجود."));
        }

        var attendance = new Domain.Entities.Employees.EmployeeAttendance
        {
            CompanyId = companyId,
            EmployeeId = request.EmployeeId,
            Status = request.Status,
            WorkDate = request.WorkDate,
            CheckIn = request.Status == EmployeeAttendanceStatus.Present ? request.CheckIn : null,
            CheckOut = request.Status == EmployeeAttendanceStatus.Present ? request.CheckOut : null,
            WorkHours = request.Status == EmployeeAttendanceStatus.Present ? CalculateWorkHours(request.CheckIn, request.CheckOut) : null,
            WorkDayRatio = request.WorkDayRatio,
            WorkOverTimeRatio = request.WorkOverTimeRatio,
            WorkDaysDeductionRatio = request.WorkDaysDeductionRatio,
            WorkLocation = string.IsNullOrWhiteSpace(request.WorkLocation) ? null : request.WorkLocation.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        dbContext.EmployeeAttendances.Add(attendance);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<EmployeeAttendanceResponse>.Success(
            new EmployeeAttendanceResponse(
                Id: attendance.Id,
                CompanyId: attendance.CompanyId,
                EmployeeId: attendance.EmployeeId,
                EmployeeName: employee.Name,
                Status: attendance.Status,
                WorkDate: attendance.WorkDate,
                CheckIn: attendance.CheckIn,
                CheckOut: attendance.CheckOut,
                WorkHours: attendance.WorkHours,
                WorkDayRatio: attendance.WorkDayRatio,
                WorkOverTimeRatio: attendance.WorkOverTimeRatio,
                WorkDaysDeductionRatio: attendance.WorkDaysDeductionRatio,
                WorkLocation: attendance.WorkLocation,
                Notes: attendance.Notes));
    }

    public async Task<Result<EmployeeAttendanceResponse>> UpdateAsync(
        int id,
        EmployeeAttendanceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateUpdateAsync(request, cancellationToken);
        if (validationError != null)
        {
            return Result<EmployeeAttendanceResponse>.Failure(validationError);
        }

        var attendance = await dbContext.EmployeeAttendances
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId, cancellationToken);

        if (attendance is null)
        {
            return Result<EmployeeAttendanceResponse>.Failure(
                Error.NotFound(
                    "EmployeeAttendance.NotFound",
                    "لم يتم العثور على سجل الحضور المطلوب."));
        }

        var employeeName = attendance.Employee.Name;
        if (attendance.EmployeeId != request.EmployeeId)
        {
            var employee = await dbContext.Employees
                .FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.CompanyId == companyId, cancellationToken);

            if (employee is null)
            {
                return Result<EmployeeAttendanceResponse>.Failure(
                    Error.NotFound(
                        "Employee.NotFound",
                        "الموظف المحدد غير موجود."));
            }

            employeeName = employee.Name;
        }

        attendance.EmployeeId = request.EmployeeId;
        attendance.Status = request.Status ?? attendance.Status;
        attendance.WorkDate = request.WorkDate;

        if (attendance.Status == EmployeeAttendanceStatus.Present)
        {
            attendance.CheckIn = request.CheckIn ?? attendance.CheckIn;
            attendance.CheckOut = request.CheckOut ?? attendance.CheckOut;
            attendance.WorkHours = CalculateWorkHours(attendance.CheckIn, attendance.CheckOut);
        }
        else
        {
            attendance.CheckIn = null;
            attendance.CheckOut = null;
            attendance.WorkHours = null;
        }

        attendance.WorkDayRatio = request.WorkDayRatio ?? attendance.WorkDayRatio;
        attendance.WorkOverTimeRatio = request.WorkOverTimeRatio ?? attendance.WorkOverTimeRatio;
        attendance.WorkDaysDeductionRatio = request.WorkDaysDeductionRatio ?? attendance.WorkDaysDeductionRatio;
        attendance.WorkLocation = string.IsNullOrWhiteSpace(request.WorkLocation) ? null : request.WorkLocation.Trim();
        attendance.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        dbContext.EmployeeAttendances.Update(attendance);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<EmployeeAttendanceResponse>.Success(
            new EmployeeAttendanceResponse(
                Id: attendance.Id,
                CompanyId: attendance.CompanyId,
                EmployeeId: attendance.EmployeeId,
                EmployeeName: employeeName,
                Status: attendance.Status,
                WorkDate: attendance.WorkDate,
                CheckIn: attendance.CheckIn,
                CheckOut: attendance.CheckOut,
                WorkHours: attendance.WorkHours,
                WorkDayRatio: attendance.WorkDayRatio,
                WorkOverTimeRatio: attendance.WorkOverTimeRatio,
                WorkDaysDeductionRatio: attendance.WorkDaysDeductionRatio,
                WorkLocation: attendance.WorkLocation,
                Notes: attendance.Notes));
    }

    public async Task<Result> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var attendance = await dbContext.EmployeeAttendances
            .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId, cancellationToken);

        if (attendance is null)
        {
            return Result.Failure(
                Error.NotFound(
                    "EmployeeAttendance.NotFound",
                    "لم يتم العثور على سجل الحضور المطلوب."));
        }

        dbContext.EmployeeAttendances.Remove(attendance);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<List<EmployeeAttendanceResponse>>> AddBulkAsync(
        BulkEmployeeAttendanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var employeeIds = request.Attendances
            .Select(a => a.EmployeeId)
            .Distinct()
            .ToList();

        var employees = await dbContext.Employees
            .Where(e => e.CompanyId == companyId && employeeIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name })
            .ToListAsync(cancellationToken);

        if (employees.Count != employeeIds.Count)
        {
            var existingIds = employees.Select(e => e.Id).ToHashSet();
            var missingIds = employeeIds.Where(id => !existingIds.Contains(id)).ToList();
            return Result<List<EmployeeAttendanceResponse>>.Failure(
                Error.NotFound(
                    "Employee.NotFound",
                    $"بعض الموظفين المحددين غير موجودين: {string.Join(", ", missingIds)}"));
        }

        var employeeMap = employees.ToDictionary(e => e.Id, e => e.Name);

        var minDate = request.Attendances.Min(a => a.WorkDate);
        var maxDate = request.Attendances.Max(a => a.WorkDate);

        var existingAttendances = await dbContext.EmployeeAttendances
            .Where(a => a.CompanyId == companyId 
                        && employeeIds.Contains(a.EmployeeId) 
                        && a.WorkDate >= minDate 
                        && a.WorkDate <= maxDate)
            .ToListAsync(cancellationToken);

        var existingMap = existingAttendances.ToDictionary(
            a => (a.EmployeeId, a.WorkDate));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var processedEntities = new List<(Domain.Entities.Employees.EmployeeAttendance Entity, int EmployeeId)>();

        foreach (var item in request.Attendances)
        {
            var key = (item.EmployeeId, item.WorkDate);
            Domain.Entities.Employees.EmployeeAttendance attendance;

            if (existingMap.TryGetValue(key, out var existingRecord))
            {
                attendance = existingRecord;
                attendance.Status = item.Status;
                attendance.CheckIn = item.Status == EmployeeAttendanceStatus.Present ? item.CheckIn : null;
                attendance.CheckOut = item.Status == EmployeeAttendanceStatus.Present ? item.CheckOut : null;
                attendance.WorkHours = item.Status == EmployeeAttendanceStatus.Present ? CalculateWorkHours(item.CheckIn, item.CheckOut) : null;
                attendance.WorkDayRatio = item.WorkDayRatio;
                attendance.WorkOverTimeRatio = item.WorkOverTimeRatio;
                attendance.WorkDaysDeductionRatio = item.WorkDaysDeductionRatio;
                attendance.WorkLocation = string.IsNullOrWhiteSpace(item.WorkLocation) ? null : item.WorkLocation.Trim();
                attendance.Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim();

                dbContext.EmployeeAttendances.Update(attendance);
            }
            else
            {
                attendance = new Domain.Entities.Employees.EmployeeAttendance
                {
                    CompanyId = companyId,
                    EmployeeId = item.EmployeeId,
                    Status = item.Status,
                    WorkDate = item.WorkDate,
                    CheckIn = item.Status == EmployeeAttendanceStatus.Present ? item.CheckIn : null,
                    CheckOut = item.Status == EmployeeAttendanceStatus.Present ? item.CheckOut : null,
                    WorkHours = item.Status == EmployeeAttendanceStatus.Present ? CalculateWorkHours(item.CheckIn, item.CheckOut) : null,
                    WorkDayRatio = item.WorkDayRatio,
                    WorkOverTimeRatio = item.WorkOverTimeRatio,
                    WorkDaysDeductionRatio = item.WorkDaysDeductionRatio,
                    WorkLocation = string.IsNullOrWhiteSpace(item.WorkLocation) ? null : item.WorkLocation.Trim(),
                    Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim()
                };

                dbContext.EmployeeAttendances.Add(attendance);
            }

            processedEntities.Add((attendance, item.EmployeeId));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var responses = processedEntities.Select(x => new EmployeeAttendanceResponse(
            x.Entity.Id,
            x.Entity.CompanyId,
            x.Entity.EmployeeId,
            employeeMap.GetValueOrDefault(x.EmployeeId) ?? string.Empty,
            x.Entity.Status,
            x.Entity.WorkDate,
            x.Entity.CheckIn,
            x.Entity.CheckOut,
            x.Entity.WorkHours,
            x.Entity.WorkDayRatio,
            x.Entity.WorkOverTimeRatio,
            x.Entity.WorkDaysDeductionRatio,
            x.Entity.WorkLocation,
            x.Entity.Notes
        )).ToList();

        return Result<List<EmployeeAttendanceResponse>>.Success(responses);
    }

    public async Task<Result<List<EmployeeAttendanceResponse>>> UpdateBulkAsync(
        BulkEmployeeAttendanceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Attendances is null || request.Attendances.Count == 0)
        {
            return Result<List<EmployeeAttendanceResponse>>.Failure(
                Error.Validation("EmployeeAttendance.EmptyBulkRequest", "يجب إرسال سجل حضور واحد على الأقل للتعديل."));
        }

        var ids = request.Attendances.Select(a => a.Id).Distinct().ToList();
        var employeeIds = request.Attendances.Select(a => a.EmployeeId).Distinct().ToList();

        var employees = await dbContext.Employees
            .Where(e => e.CompanyId == companyId && employeeIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name })
            .ToDictionaryAsync(e => e.Id, e => e.Name, cancellationToken);

        if (employees.Count != employeeIds.Count)
        {
            var missing = employeeIds.Where(id => !employees.ContainsKey(id)).ToList();
            return Result<List<EmployeeAttendanceResponse>>.Failure(
                Error.NotFound("Employee.NotFound", $"بعض الموظفين المحددين غير موجودين: {string.Join(", ", missing)}"));
        }

        var existingAttendances = await dbContext.EmployeeAttendances
            .Include(a => a.Employee)
            .Where(a => a.CompanyId == companyId && ids.Contains(a.Id))
            .ToListAsync(cancellationToken);

        var existingMap = existingAttendances.ToDictionary(a => a.Id);

        if (existingAttendances.Count != ids.Count)
        {
            var missingIds = ids.Where(id => !existingMap.ContainsKey(id)).ToList();
            return Result<List<EmployeeAttendanceResponse>>.Failure(
                Error.NotFound("EmployeeAttendance.NotFound", $"بعض سجلات الحضور المحددة غير موجودة: {string.Join(", ", missingIds)}"));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var item in request.Attendances)
        {
            var attendance = existingMap[item.Id];
            attendance.EmployeeId = item.EmployeeId;
            attendance.Status = item.Status ?? attendance.Status;
            attendance.WorkDate = item.WorkDate;

            if (attendance.Status == EmployeeAttendanceStatus.Present)
            {
                attendance.CheckIn = item.CheckIn ?? attendance.CheckIn;
                attendance.CheckOut = item.CheckOut ?? attendance.CheckOut;
                attendance.WorkHours = CalculateWorkHours(attendance.CheckIn, attendance.CheckOut);
            }
            else
            {
                attendance.CheckIn = null;
                attendance.CheckOut = null;
                attendance.WorkHours = null;
            }

            attendance.WorkDayRatio = item.WorkDayRatio ?? attendance.WorkDayRatio;
            attendance.WorkOverTimeRatio = item.WorkOverTimeRatio ?? attendance.WorkOverTimeRatio;
            attendance.WorkDaysDeductionRatio = item.WorkDaysDeductionRatio ?? attendance.WorkDaysDeductionRatio;
            attendance.WorkLocation = string.IsNullOrWhiteSpace(item.WorkLocation) ? null : item.WorkLocation.Trim();
            attendance.Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim();

            dbContext.EmployeeAttendances.Update(attendance);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var responses = existingAttendances.Select(a => new EmployeeAttendanceResponse(
            a.Id,
            a.CompanyId,
            a.EmployeeId,
            employees.GetValueOrDefault(a.EmployeeId) ?? a.Employee?.Name ?? string.Empty,
            a.Status,
            a.WorkDate,
            a.CheckIn,
            a.CheckOut,
            a.WorkHours,
            a.WorkDayRatio,
            a.WorkOverTimeRatio,
            a.WorkDaysDeductionRatio,
            a.WorkLocation,
            a.Notes
        )).ToList();

        return Result<List<EmployeeAttendanceResponse>>.Success(responses);
    }

    public async Task<Result> DeleteBulkAsync(
        BulkEmployeeAttendanceDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AttendanceIds is null || request.AttendanceIds.Count == 0)
        {
            return Result.Failure(
                Error.Validation("EmployeeAttendance.EmptyBulkRequest", "يجب تحديد معرفات سجلات الحضور المراد حذفها."));
        }

        var distinctIds = request.AttendanceIds.Distinct().ToList();

        var attendances = await dbContext.EmployeeAttendances
            .Where(a => a.CompanyId == companyId && distinctIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        if (attendances.Count != distinctIds.Count)
        {
            var existingIds = attendances.Select(a => a.Id).ToHashSet();
            var missingIds = distinctIds.Where(id => !existingIds.Contains(id)).ToList();
            return Result.Failure(
                Error.NotFound("EmployeeAttendance.NotFound", $"بعض سجلات الحضور المحددة غير موجودة: {string.Join(", ", missingIds)}"));
        }

        dbContext.EmployeeAttendances.RemoveRange(attendances);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<EmployeeAttendanceReportResponse>> GetReportAsync(
        EmployeeAttendanceReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.StartDate > request.EndDate)
        {
            return Result<EmployeeAttendanceReportResponse>.Failure(
                Error.Validation(
                    "EmployeeAttendance.InvalidDateRange",
                    "تاريخ البداية يجب أن يكون قبل أو يساوي تاريخ النهاية."));
        }

        var query = dbContext.EmployeeAttendances
            .AsNoTracking()
            .Where(a => a.CompanyId == companyId &&
                        a.WorkDate >= request.StartDate &&
                        a.WorkDate <= request.EndDate);

        if (request.EmployeeId.HasValue)
        {
            query = query.Where(a => a.EmployeeId == request.EmployeeId.Value);
        }

        var attendances = await query
            .Select(a => new
            {
                a.EmployeeId,
                a.Employee.Code,
                a.Employee.Name,
                a.Status,
                a.WorkDayRatio,
                a.WorkOverTimeRatio,
                a.WorkDaysDeductionRatio
            })
            .ToListAsync(cancellationToken);

        var employeeGroups = attendances
            .GroupBy(a => (a.EmployeeId, a.Code, a.Name))
            .Select(g => new EmployeeAttendanceReportLine(
                EmployeeId: g.Key.EmployeeId,
                EmployeeCode: g.Key.Code,
                EmployeeName: g.Key.Name,
                PresentDays: g.Count(a => a.Status == EmployeeAttendanceStatus.Present),
                AbsentDays: g.Count(a => a.Status == EmployeeAttendanceStatus.Absent),
                WorkedUnits: g.Where(a => a.Status == EmployeeAttendanceStatus.Present).Sum(a => GetRatioValue(a.WorkDayRatio)),
                OvertimeUnits: g.Where(a => a.Status == EmployeeAttendanceStatus.Present).Sum(a => GetRatioValue(a.WorkOverTimeRatio)),
                DeductionUnits: g.Where(a => a.Status == EmployeeAttendanceStatus.Present).Sum(a => GetRatioValue(a.WorkDaysDeductionRatio))))
            .OrderBy(e => e.EmployeeName)
            .ToList();

        var response = new EmployeeAttendanceReportResponse(
            StartDate: request.StartDate,
            EndDate: request.EndDate,
            TotalEmployees: employeeGroups.Count,
            TotalPresentDays: employeeGroups.Sum(e => e.PresentDays),
            TotalAbsentDays: employeeGroups.Sum(e => e.AbsentDays),
            TotalWorkedUnits: employeeGroups.Sum(e => e.WorkedUnits),
            TotalOvertimeUnits: employeeGroups.Sum(e => e.OvertimeUnits),
            TotalDeductionUnits: employeeGroups.Sum(e => e.DeductionUnits),
            Employees: employeeGroups);

        return Result<EmployeeAttendanceReportResponse>.Success(response);
    }

    private static decimal GetRatioValue(WorkDayRatio? ratio) =>
        ratio switch
        {
            WorkDayRatio.FullDay         => 1m,
            WorkDayRatio.ThreeQuarterDay => 0.75m,
            WorkDayRatio.HalfDay         => 0.5m,
            WorkDayRatio.ThirdDay        => 1m / 3m,
            WorkDayRatio.QuarterDay      => 0.25m,
            _                            => 0m
        };

    private static TimeOnly? CalculateWorkHours(TimeOnly? checkIn, TimeOnly? checkOut)
    {
        // Convert to TimeSpan
        TimeSpan inSpan = checkIn?.ToTimeSpan() ?? TimeSpan.Zero;
        TimeSpan outSpan = checkOut?.ToTimeSpan() ?? TimeSpan.Zero;

        // Handle overnight shifts (checkout after midnight)
        if (outSpan < inSpan)
        {
            outSpan = outSpan.Add(TimeSpan.FromDays(1));
        }

        // Calculate duration
        TimeSpan duration = outSpan - inSpan;

        // Convert back to TimeOnly (hours/minutes of work)
        return TimeOnly.FromTimeSpan(duration);
    }
}
