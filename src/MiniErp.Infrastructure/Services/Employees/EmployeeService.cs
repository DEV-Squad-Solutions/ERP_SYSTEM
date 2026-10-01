using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Employees;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Infrastructure.Services.Employees
{
    public sealed partial class EmployeeService(
        ApplicationDbContext dbContext,
        IPaginationService paginationService,
        ICurrentCompanyContext currentCompanyContext)
        : IEmployeeService, IScopedService
    {
        private readonly int campanyId = currentCompanyContext.CompanyId;
        public async Task<Result<EmployeePageResponse>> GetAllAsync(
            PaginationRequest pagination, 
            EmployeeFilterRequest? filters = null, 
            CancellationToken cancellationToken = default)
        {
            filters ??= new EmployeeFilterRequest();
            var FilterError = ValidateFilters(filters);
            if(FilterError is not null)
            {
                return Result<EmployeePageResponse>.Failure(FilterError);
            }

            var query = dbContext.Employees
                .AsNoTracking()
                .Where(e => e.CompanyId == campanyId);

            query = ApplyFilters(query, filters);

            // ── Balance filter (DB-level, fiscal-year scoped) ──────────────────────
            // Resolve the current fiscal year only when the balance filter is active,
            // avoiding an extra round-trip on ordinary list loads.
            int? fiscalYearId = null;
            if (filters.BalanceStatus.HasValue &&
                filters.BalanceStatus.Value != BalanceStatus.All)
            {
                fiscalYearId = await dbContext.FiscalYears
                    .AsNoTracking()
                    .Where(fy => fy.CompanyId == campanyId && fy.IsCurrent)
                    .Select(fy => (int?)fy.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (fiscalYearId.HasValue)
                {
                    query = ApplyBalanceFilter(
                        query,
                        filters.BalanceStatus.Value,
                        fiscalYearId.Value);
                }
            }

            var orderedQuery = query
                .OrderByDescending(e => e.CreatedOn)
                .ThenByDescending(e => e.Id);

            var aggregateSummary = await GetSummaryAsync(query, cancellationToken);

            var pagedResult = await paginationService.PaginateAsync<Employee, EmployeeListResponse>(
                orderedQuery, pagination, aggregateSummary.TotalCount, cancellationToken);
            if (pagedResult.IsFailure)
            {
                return Result<EmployeePageResponse>.Failure(pagedResult.Error);
            }

            var page = pagedResult.Value;

            // ── Batch-fetch balances for the current page (no N+1) ────────────────
            // Resolve fiscal year for balance fetch if not already resolved above.
            if (!fiscalYearId.HasValue && page.Items.Count > 0)
            {
                fiscalYearId = await dbContext.FiscalYears
                    .AsNoTracking()
                    .Where(fy => fy.CompanyId == campanyId && fy.IsCurrent)
                    .Select(fy => (int?)fy.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            IReadOnlyCollection<EmployeeListResponse> items = page.Items;
            if (fiscalYearId.HasValue && page.Items.Count > 0)
            {
                var employeeIds = page.Items.Select(e => e.Id).ToArray();
                var balances = await FetchEmployeeBalancesAsync(
                    employeeIds, fiscalYearId.Value, cancellationToken);

                items = page.Items
                    .Select(e => e with { Balance = balances.GetValueOrDefault(e.Id, 0m) })
                    .ToArray();
            }

            return Result<EmployeePageResponse>.Success(
                new EmployeePageResponse(
                    items,
                    page.PageNumber,
                    page.PageSize,
                    page.TotalCount,
                    page.TotalPages,
                    aggregateSummary.Summary));
        }


        public async Task<Result<IReadOnlyList<SelectEmployeeResponse>>> GetSelectAsync(
            EmployeeSelectedFilterRequest filters = null, 
            CancellationToken cancellationToken = default)
        {
            filters ??= new EmployeeSelectedFilterRequest();
            var query = dbContext.Employees.AsNoTracking()
                .Where(e => e.CompanyId == campanyId);

            query = ApplyFilters(query, filters);

            var employees = 
                await query.Where(e => e.CompanyId == campanyId)
                .OrderBy(e => e.Name)
                .Select(e => new SelectEmployeeResponse(e.Id, e.Name,e.LastDayOfReceivingSalary))
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<SelectEmployeeResponse>>.Success(employees.AsReadOnly());
        }

        public async Task<Result<EmployeeResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var employee = await dbContext.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == campanyId, cancellationToken);

            if (employee is null)
            {
                return Result<EmployeeResponse>.Failure(
                    Error.NotFound(
                        "Employee.NotFound",
                        "لم يتم العثور على الموظف المطلوب."));
            }

            return Result<EmployeeResponse>.Success(
                new EmployeeResponse(
                    Id: employee.Id,
                    Code: employee.Code,
                    Name: employee.Name,
                    JobTitle: employee.JobTitle,
                    PhoneNumber: employee.PhoneNumber,
                    Email: employee.Email,
                    Address: employee.Address,
                    EmployeeType: employee.Type,
                    Salary: employee.Type == EmployeeType.Monthly ? employee.MonthlySalary ?? 0 : employee.DailySalary ?? 0,
                    RequiredWorkingDaysPerMonth: employee.RequiredWorkingDaysPerMonth,
                    LastDayOfReceivingSalary: employee.LastDayOfReceivingSalary,
                    IsActive: employee.IsActive,
                    WorkPlaceStatus: employee.WorkPlaceStatus,
                    PlaceName: employee.PlaceName
                ));
        }

        public async Task<Result<EmployeeResponse>> AddAsync(EmployeeCreateRequest request, CancellationToken cancellationToken = default)
        {
            var validationError = await ValidateAddAsync(request, cancellationToken);
            if (validationError != null)
            {
                return Result<EmployeeResponse>.Failure(validationError);
            }
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                cancellationToken);
            var employee = new Employee
            { //EmployeeType): Daily = 0, Monthly = 1
                CompanyId = campanyId,
                Name = request.Name.Trim(),
                JobTitle = !string.IsNullOrWhiteSpace(request.JobTitle) ? request.JobTitle.Trim() : null,
                PhoneNumber = !string.IsNullOrWhiteSpace(request.PhoneNumber) ? request.PhoneNumber.Trim() : null,
                Email = !string.IsNullOrWhiteSpace(request.Email) ? request.Email.Trim() : null,
                Address = !string.IsNullOrWhiteSpace(request.Address) ? request.Address.Trim() : null,
                Type = request.Type,
                DailySalary = request.Type == EmployeeType.Daily ? request.Salary : null,
                MonthlySalary = request.Type == EmployeeType.Monthly ? request.Salary : null,
                RequiredWorkingDaysPerMonth = request.Type == EmployeeType.Monthly ? request.RequiredWorkingDaysPerMonth : null,
                IsActive = request.IsActive
            };
            employee.UpdateWorkPlace(request.WorkPlaceStatus, request.PlaceName);

            dbContext.Employees.Add(employee);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<EmployeeResponse>.Success(
                new EmployeeResponse(
                    Id: employee.Id,
                    Code: employee.Code,
                    Name: employee.Name,
                    JobTitle: employee.JobTitle,
                    PhoneNumber: employee.PhoneNumber,
                    Email: employee.Email,
                    Address: employee.Address,
                    EmployeeType: employee.Type,
                    Salary: employee.Type == EmployeeType.Monthly ? employee.MonthlySalary ?? 0 : employee.DailySalary ?? 0,
                    RequiredWorkingDaysPerMonth: employee.RequiredWorkingDaysPerMonth,
                    LastDayOfReceivingSalary: employee.LastDayOfReceivingSalary,
                    IsActive: employee.IsActive,
                    WorkPlaceStatus: employee.WorkPlaceStatus,
                    PlaceName: employee.PlaceName
                ));
        }

        public async Task<Result<EmployeeResponse>> UpdateAsync(int id, EmployeeUpdateRequest request, CancellationToken cancellationToken = default)
        {   
            var validationError = await ValidateUpdateAsync(id, request, cancellationToken);
            if (validationError is not null)
            {
                return Result<EmployeeResponse>.Failure(validationError);
            }

            var employee = await dbContext.Employees
                .FirstOrDefaultAsync(e => e.Id == id && e.CompanyId == campanyId, cancellationToken);

            if (employee is null)
            {
                return Result<EmployeeResponse>.Failure(
                    Error.NotFound(
                        "Employee.NotFound",
                        "لم يتم العثور على الموظف المطلوب."));
            }
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

            employee.Name = string.IsNullOrWhiteSpace(request.Name) ? employee.Name : request.Name.Trim();
            employee.JobTitle = string.IsNullOrWhiteSpace(request.JobTitle) ? employee.JobTitle : request.JobTitle.Trim();
            employee.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? employee.PhoneNumber : request.PhoneNumber.Trim();
            employee.Email = string.IsNullOrWhiteSpace(request.Email) ? employee.Email : request.Email.Trim();
            employee.Address = string.IsNullOrWhiteSpace(request.Address) ? employee.Address : request.Address.Trim();
            var targetType = request.Type ?? employee.Type;
            employee.Type = targetType;

            if (targetType == EmployeeType.Daily)
            {
                employee.DailySalary = request.Salary ?? employee.DailySalary ?? employee.MonthlySalary;
                employee.MonthlySalary = null;
                employee.RequiredWorkingDaysPerMonth = null;
            }
            else // Monthly
            {
                employee.MonthlySalary = request.Salary ?? employee.MonthlySalary ?? employee.DailySalary;
                employee.DailySalary = null;
                employee.RequiredWorkingDaysPerMonth = request.RequiredWorkingDaysPerMonth ?? employee.RequiredWorkingDaysPerMonth;
            }

            if (request.IsActive.HasValue)
            {
                employee.SetIsActive(request.IsActive.Value);
            }

            if (request.WorkPlaceStatus.HasValue || request.PlaceName != null)
            {
                var targetStatus = request.WorkPlaceStatus ?? employee.WorkPlaceStatus;
                var targetPlace = request.PlaceName ?? employee.PlaceName;
                employee.UpdateWorkPlace(targetStatus, targetPlace);
            }

            dbContext.Employees.Update(employee).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<EmployeeResponse>.Success(
                new EmployeeResponse(
                    Id: employee.Id,
                    Code: employee.Code,
                    Name: employee.Name,
                    JobTitle: employee.JobTitle,
                    PhoneNumber: employee.PhoneNumber,
                    Email: employee.Email,
                    Address: employee.Address,
                    EmployeeType: employee.Type,
                    Salary: employee.Type == EmployeeType.Monthly ? employee.MonthlySalary ?? 0 : employee.DailySalary ?? 0,
                    RequiredWorkingDaysPerMonth: employee.RequiredWorkingDaysPerMonth,
                    LastDayOfReceivingSalary: employee.LastDayOfReceivingSalary,
                    IsActive: employee.IsActive,
                    WorkPlaceStatus: employee.WorkPlaceStatus,
                    PlaceName: employee.PlaceName
                ));
        }

        public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            if (id <= 0)
            {
                return Result.Failure(InvalidId());
            }
            
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

            var employeedata = await LoadForWriteAsync(id, cancellationToken);
            if (employeedata.Item1 is null)
            {
                return Result.Failure(
                    Error.NotFound(
                        "Employee.NotFound",
                        "لم يتم العثور على الموظف المطلوب."));
            }
            // Only fail if the employee has attendance records (cannot delete then)
            if (employeedata.Item2 is not null && employeedata.Item2.Any())
            {
                return Result.Failure(
                    Error.Conflict(
                        "Employee.HasAttendanceRecords",
                        "لا يمكن حذف الموظف لوجود سجلات حضور مرتبطة به."));
            }

            dbContext.Employees.Remove(employeedata.Item1);
            if (employeedata.Item2 is not null)
                dbContext.EmployeeAttendances.RemoveRange(employeedata.Item2);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }

    }
}
