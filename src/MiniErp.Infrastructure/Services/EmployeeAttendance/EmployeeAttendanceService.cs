using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.EmployeeAttendance;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Services.EmployeeAttendance;

public sealed class EmployeeAttendanceService(
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

        var query = dbContext.EmployeeAttendances
            .AsNoTracking()
            .Where(a => a.CompanyId == companyId);
            //.Include(a => a.Employee);

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
            .ThenBy(a => a.Employee.Name)
            .ThenBy(a => a.Id);

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
        if(id <= 0)
        {
            return Result<EmployeeAttendanceResponse>.Failure(
                Error.Validation(
                    "EmployeeAttendance.InvalidId",
                    "معرف سجل الحضور غير صالح."));
        }

        var attendance = await dbContext.EmployeeAttendances
            .AsNoTracking().FirstOrDefaultAsync(employeeAttendance => employeeAttendance.Id == id && employeeAttendance.CompanyId == companyId, cancellationToken);

        if (attendance is null)
        {
            return Result<EmployeeAttendanceResponse>.Failure(
                Error.NotFound(
                    "EmployeeAttendance.NotFound",
                    "لم يتم العثور على سجل الحضور المطلوب."));
        }

        return Result<EmployeeAttendanceResponse>.Success(
            new EmployeeAttendanceResponse(
                attendance.Id,
                attendance.CompanyId,
                attendance.EmployeeId,
                attendance.Employee.Name,
                attendance.Status,
                attendance.WorkDate,
                attendance.CheckIn,
                attendance.CheckOut,
                attendance.WorkHours,
                attendance.WorkDayRatio,
                attendance.WorkOverTimeRatio,
                attendance.WorkDaysDeductionRatio,
                attendance.WorkLocation,
                attendance.Notes));
    }

    public async Task<Result<EmployeeAttendanceResponse>> AddAsync(
        EmployeeAttendanceRequest request,
        CancellationToken cancellationToken = default)
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

        var attendance = new Domain.Entities.Employees.EmployeeAttendance
        {
            CompanyId = companyId,
            EmployeeId = request.EmployeeId,
            Status = request.Status,
            WorkDate = request.WorkDate,
            CheckIn = request.CheckIn,
            CheckOut = request.CheckOut,
            WorkHours = CalculateWorkHours(request.CheckIn, request.CheckOut),
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
                attendance.Id,
                attendance.CompanyId,
                attendance.EmployeeId,
                employee.Name,
                attendance.Status,
                attendance.WorkDate,
                attendance.CheckIn,
                attendance.CheckOut,
                attendance.WorkHours,
                attendance.WorkDayRatio,
                attendance.WorkOverTimeRatio,
                attendance.WorkDaysDeductionRatio,
                attendance.WorkLocation,
                attendance.Notes));
    }

    public async Task<Result<EmployeeAttendanceResponse>> UpdateAsync(
        int id,
        EmployeeAttendanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var attendance = await dbContext.EmployeeAttendances
            .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId, cancellationToken);

        if (attendance is null)
        {
            return Result<EmployeeAttendanceResponse>.Failure(
                Error.NotFound(
                    "EmployeeAttendance.NotFound",
                    "لم يتم العثور على سجل الحضور المطلوب."));
        }

        if (attendance.EmployeeId != request.EmployeeId)
        {
            var employee = await dbContext.Employees.Select(employee => new { employee.Id, employee.CompanyId, employee.Name })
                .FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.CompanyId == companyId, cancellationToken);

            if (employee is null)
            {
                return Result<EmployeeAttendanceResponse>.Failure(
                    Error.NotFound(
                        "Employee.NotFound",
                        "الموظف المحدد غير موجود."));
            }
        }

        attendance.EmployeeId = request.EmployeeId;
        attendance.Status = request.Status;
        attendance.WorkDate = request.WorkDate;
        attendance.CheckIn = request.CheckIn;
        attendance.CheckOut = request.CheckOut;
        attendance.WorkHours = CalculateWorkHours(request.CheckIn, request.CheckOut);
        attendance.WorkDayRatio = request.WorkDayRatio;
        attendance.WorkOverTimeRatio = request.WorkOverTimeRatio;
        attendance.WorkDaysDeductionRatio = request.WorkDaysDeductionRatio;
        attendance.WorkLocation = string.IsNullOrWhiteSpace(request.WorkLocation) ? null : request.WorkLocation.Trim();
        attendance.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        dbContext.EmployeeAttendances.Update(attendance);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<EmployeeAttendanceResponse>.Success(
            new EmployeeAttendanceResponse(
                attendance.Id,
                attendance.CompanyId,
                attendance.EmployeeId,
                attendance.Employee.Name,
                attendance.Status,
                attendance.WorkDate,
                attendance.CheckIn,
                attendance.CheckOut,
                attendance.WorkHours,
                attendance.WorkDayRatio,
                attendance.WorkOverTimeRatio,
                attendance.WorkDaysDeductionRatio,
                attendance.WorkLocation,
                attendance.Notes));
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
