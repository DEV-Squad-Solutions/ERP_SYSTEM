using MiniErp.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Application.Features.Employees
{
    public record EmployeePageResponse(
        IReadOnlyCollection<EmployeeListResponse> Employees,
        int PageNumber,
        int PageSize,
        int TotalCount,
        int TotalPages,
        EmployeeSummaryResponse Summary
     );

    public record EmployeeSummaryResponse(
        int TotalMonthlyEmployees,
        int TotalDailyEmployees
    );

    public record EmployeeListResponse(
        string Code,
        string Name,
        string? JobTitle,
        string? PhoneNumber,
        string? Email,
        string? Address,
        EmployeeType EmployeeType,
        decimal Salary,
        int? RequiredWorkingDaysPerMonth,
        DateOnly LastDayOfReceivingSalary,
        bool IsActive
        );


    public record EmployeeResponse(
        int EmployeeId,
        string Code,
        string Name,
        string? JobTitle,
        string? PhoneNumber,
        string? Email,
        string? Address,
        EmployeeType EmployeeType,
        decimal Salary,
        int? RequiredWorkingDaysPerMonth,
        DateOnly? LastDayOfReceivingSalary, 
        bool IsActive
    );

}
