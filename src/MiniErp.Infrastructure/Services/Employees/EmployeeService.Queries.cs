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
            var search=filters.Search?.Trim();
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
                    employee.Type.ToString().Contains(search)
                    );
            }
            var name = filters.Name?.Trim();
            if (!string.IsNullOrWhiteSpace(name)) {
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

            if (filters.MinSalary.HasValue)
            {
                query = query.Where(employee => 
                employee.MonthlySalary.HasValue && 
                employee.MonthlySalary.Value >= filters.MinSalary.Value);
            }

            if (filters.MaxSalary.HasValue)
            {
                query = query.Where(employee => 
                employee.MonthlySalary.HasValue && 
                employee.MonthlySalary.Value <= filters.MaxSalary.Value);
            }

            if (filters.Type.HasValue)
            {
                query = query.Where(employee => 
                employee.Type == filters.Type.Value);  
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
                })
                .SingleOrDefaultAsync(cancellationToken);
            
            return summary is null
                ? (0, new EmployeeSummaryResponse(0 , 0))
                : (summary.TotalCount, new EmployeeSummaryResponse(                
                    summary.TotalMonthlyEmployees,
                    summary.TotalDailyEmployees
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