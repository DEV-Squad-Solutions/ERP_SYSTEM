using MiniErp.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Application.Features.Employees
{
    public record EmployeeFilterRequest(
    string? Search = null,
    string? Name = null,
    string? Code = null,
    string? JobTitle = null,
    decimal? MinSalary = null,
    decimal? MaxSalary = null,
    EmployeeType? EmployeeType = null
    );
    public record EmployeeCreateRequest(
        string Name,
        string? JobTitle,
        string? PhoneNumber,
        string? Email,
        string? Address,
        EmployeeType Type,
        decimal? Salary,
        int? RequiredWorkingDaysPerMonth=26,
        bool IsActive = true
    );
    public record EmployeeUpdateRequest(
        int? CompanyId,
        string? Name,
        string? JobTitle,
        string? PhoneNumber,
        string? Email,
        string? Address,
        EmployeeType? Type,
        decimal? Salary,
        int? RequiredWorkingDaysPerMonth,
        bool IsActive = true
    );
}
