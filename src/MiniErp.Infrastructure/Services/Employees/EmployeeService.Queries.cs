using Azure.Core;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Features.Employees;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace MiniErp.Infrastructure.Services.Employees
{
    public sealed partial class EmployeeService
    {
        private static IQueryable<Employee> ApplyFilters(
            IQueryable<Employee> query,
            EmployeeFilterRequest filters)
        {
            var search = filters.Search?.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(employee =>
                    employee.Code.Contains(search) ||
                    employee.Name.Contains(search) ||
                    employee.Email != null &&
                    employee.Email.Contains(search) ||
                    employee.PhoneNumber != null &&
                    employee.PhoneNumber.Contains(search) ||
                    employee.Address != null &&
                    employee.Address.Contains(search) ||
                    employee.Type.ToString().Contains(search) ||
                    employee.JobTitle != null &&
                    employee.JobTitle.Contains(search) ||
                    employee.PlaceName != null &&
                    employee.PlaceName.Contains(search)
                    );
            }
            var name = filters.Name?.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(employee =>
                    employee.Name.Contains(name));
            }

            var code = filters.Code?.Trim();
            if (!string.IsNullOrWhiteSpace(code))
            {
                query = query.Where(employee =>
                    employee.Code.Contains(code));
            }
            var jobTitle = filters.JobTitle?.Trim();
            if (!string.IsNullOrWhiteSpace(jobTitle))
            {
                query = query.Where(employee =>
                    employee.JobTitle != null &&
                    employee.JobTitle.Contains(jobTitle));
            }

            var placeName = filters.PlaceName?.Trim();
            if (!string.IsNullOrWhiteSpace(placeName))
            {
                query = query.Where(employee =>
                    employee.PlaceName != null &&
                    employee.PlaceName.Contains(placeName));
            }

            if (filters.MinSalary.HasValue)
            {
                query = query.Where(employee =>
                employee.MonthlySalary.HasValue && employee.MonthlySalary.Value >= filters.MinSalary.Value
                || employee.DailySalary.HasValue && employee.DailySalary.Value >= filters.MinSalary.Value);
            }

            if (filters.MaxSalary.HasValue)
            {
                query = query.Where(employee =>
                employee.MonthlySalary.HasValue && employee.MonthlySalary.Value <= filters.MaxSalary.Value
                || employee.DailySalary.HasValue && employee.DailySalary.Value <= filters.MaxSalary.Value);
            }
            if (filters.EmployeeType.HasValue)
            {
                query = query.Where(employee =>
                employee.Type == filters.EmployeeType.Value);
            }
            if (filters.WorkPlaceStatus.HasValue)
            {
                query = query.Where(employee =>
                employee.WorkPlaceStatus == filters.WorkPlaceStatus.Value);
            }
            if (filters.IsActive.HasValue)
            {
                query = query.Where(employee =>
                employee.IsActive == filters.IsActive.Value);
            }
            return query;
        }
        private static IQueryable<Employee> ApplyFilters(
            IQueryable<Employee> query,
            EmployeeSelectedFilterRequest filters)
        {
            if (filters.EmployeeType.HasValue)
            {
                query = query.Where(employee =>
                employee.Type == filters.EmployeeType.Value);
            }
            if (filters.WorkPlaceStatus.HasValue)
            {
                query = query.Where(employee =>
                employee.WorkPlaceStatus == filters.WorkPlaceStatus.Value);
            }
            if (filters.IsActive.HasValue)
            {
                query = query.Where(employee =>
                employee.IsActive == filters.IsActive.Value);
            }
            return query;
        }


        private static async Task<(int TotalCount, EmployeeSummaryResponse Summary)>
            GetSummaryAsync(IQueryable<Employee> query,
                CancellationToken cancellationToken)
        {
            var summary = await query
                .GroupBy(_ => 1)
                .Select(group => new
                {                    
                    TotalCount = group.Count(),
                    TotalMonthlyEmployees = group.Count(e => e.Type == EmployeeType.Monthly),
                    TotalDailyEmployees = group.Count(e => e.Type == EmployeeType.Daily),
                    TotalActiveEmployees = group.Count(e => e.IsActive),
                    TotalInactiveEmployees = group.Count(e => !e.IsActive),
                    TotalInCompanyEmployees = group.Count(e => e.WorkPlaceStatus == WorkPlaceStatus.InCompany),
                    TotalOutCompanyEmployees = group.Count(e => e.WorkPlaceStatus == WorkPlaceStatus.OutCompany)
                })
                .SingleOrDefaultAsync(cancellationToken);
            
            return summary is null
                ? (0, new EmployeeSummaryResponse(
                    TotalMonthlyEmployees: 0,
                    TotalDailyEmployees: 0,
                    TotalActiveEmployees: 0,
                    TotalInactiveEmployees: 0,
                    TotalInCompanyEmployees: 0,
                    TotalOutCompanyEmployees: 0))
                : (summary.TotalCount, new EmployeeSummaryResponse(                
                    TotalMonthlyEmployees: summary.TotalMonthlyEmployees,
                    TotalDailyEmployees: summary.TotalDailyEmployees,
                    TotalActiveEmployees: summary.TotalActiveEmployees,
                    TotalInactiveEmployees: summary.TotalInactiveEmployees,
                    TotalInCompanyEmployees: summary.TotalInCompanyEmployees,
                    TotalOutCompanyEmployees: summary.TotalOutCompanyEmployees
                ));
        }
        private async Task<(Employee?, IEnumerable<MiniErp.Domain.Entities.Employees.EmployeeAttendance>)> LoadForWriteAsync(
            int id,
            CancellationToken cancellationToken)
        {
            var employee = await dbContext.Employees
                .Where(employee => employee.CompanyId == campanyId)
                .FirstOrDefaultAsync(employee => employee.Id == id, cancellationToken);
            var attendances = await dbContext.EmployeeAttendances
                .Where(attendance => attendance.Employee.CompanyId == campanyId)
                .Where(attendance => attendance.EmployeeId == id).ToListAsync(cancellationToken);
            return (employee, attendances);
        }
    }
}   
