using Mapster;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Employees;

public sealed class EmployeeMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<Employee, EmployeeListResponse>()
            .Map(dest => dest.Salary, src => src.Type == EmployeeType.Monthly ? (src.MonthlySalary ?? 0) : (src.DailySalary ?? 0));
    }
}
