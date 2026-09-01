using Mapster;
using EmployeeAttendanceEntity = MiniErp.Domain.Entities.Employees.EmployeeAttendance;

namespace MiniErp.Application.Features.EmployeeAttendance;

public sealed class EmployeeAttendanceMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<EmployeeAttendanceEntity, EmployeeAttendanceResponse>()
            .Map(dest => dest.EmployeeName, src => src.Employee != null ? src.Employee.Name : string.Empty);
    }
}
