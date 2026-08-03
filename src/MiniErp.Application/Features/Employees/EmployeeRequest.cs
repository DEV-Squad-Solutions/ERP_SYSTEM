using MiniErp.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Application.Features.Employees
{
    public record EmployeeRequest(
        string Code,
        string Name,
        string? JobTitle,
        string? PhoneNumber,
        string? Email,
        string? Address,
        EmployeeType Type,
        decimal? Salary,
        int? RequiredWorkingDaysPerMonth = 26,
        bool IsTakeSalary = true,
        bool IsActive = true
    );

    public record EmployeeUpdateRequest(
        string Code,
        string Name,
        string? JobTitle,
        string? PhoneNumber,
        string? Email,
        string? Address,
        EmployeeType Type,
        decimal? Salary,
        int? RequiredWorkingDaysPerMonth = 26,
        bool IsTakeSalary = true,
        bool IsActive = true
    );
}
