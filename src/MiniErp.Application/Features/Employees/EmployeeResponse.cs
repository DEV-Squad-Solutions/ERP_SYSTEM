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
    public record SelectEmployeeResponse(
        int Id,
        string Name,
        DateOnly? LastDayOfReceivingSalary = null
    );
    public record EmployeeSummaryResponse(
        int TotalMonthlyEmployees,
        int TotalDailyEmployees,
        int TotalActiveEmployees = 0,
        int TotalInactiveEmployees = 0,
        int TotalInCompanyEmployees = 0,
        int TotalOutCompanyEmployees = 0
    );

    public record EmployeeListResponse(
        int Id,
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
        bool IsActive,
        WorkPlaceStatus WorkPlaceStatus,
        string? PlaceName = null
        );


    public record EmployeeResponse(
        int Id,
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
        bool IsActive,
        WorkPlaceStatus WorkPlaceStatus,
        string? PlaceName = null
    );

}
