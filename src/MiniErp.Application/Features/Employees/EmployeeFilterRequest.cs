using MiniErp.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Application.Features.Employees
{
    public record EmployeeFilterRequest(
        string? Search = null,
        string? Name=null,
        string? Code=null,
        string? JobTitle=null,
        decimal? MinSalary=null,
        decimal? MaxSalary=null,
        EmployeeType? EmployeeType = null
    );

}
