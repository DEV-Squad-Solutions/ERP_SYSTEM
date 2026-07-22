using Mapster;
using MiniErp.Domain.Entities.Logistics;

namespace MiniErp.Application.Features.Drivers;

public sealed class DriverMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<DriverRequest, Driver>()
            .Map(driver => driver.Code, request => request.Code.Trim())
            .Map(driver => driver.Name, request => request.Name.Trim())
            .Map(
                driver => driver.PhoneNumber,
                request => string.IsNullOrWhiteSpace(request.PhoneNumber)
                    ? null
                    : request.PhoneNumber.Trim())
            .Map(
                driver => driver.NationalId,
                request => string.IsNullOrWhiteSpace(request.NationalId)
                    ? null
                    : request.NationalId.Trim())
            .Map(
                driver => driver.LicenseNumber,
                request => request.LicenseNumber.Trim());
    }
}
