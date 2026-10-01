using Mapster;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;

namespace MiniErp.Application.Features.Employees;

public sealed class EmployeeMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<Employee, EmployeeListResponse>()
            .Map(dest => dest.EmployeeType, src => src.Type)
            .Map(dest => dest.Salary, src => src.Type == EmployeeType.Monthly ? (src.MonthlySalary ?? 0) : (src.DailySalary ?? 0))
            .Map(dest => dest.LastDayOfReceivingSalary, src => src.LastDayOfReceivingSalary)
            .Map(dest => dest.WorkPlaceStatus, src => src.WorkPlaceStatus)
            .Map(dest => dest.PlaceName, src => src.PlaceName)
            // Balance is not persisted on the entity; the real value is injected
            // post-pagination via a batch query. Map to 0m so ProjectToType succeeds.
            .Map(dest => dest.Balance, _ => 0m);
    }
}
