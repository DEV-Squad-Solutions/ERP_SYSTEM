using Mapster;

namespace MiniErp.Application.Common.Mappings;

public static class MappingConfiguration
{
    public static void Register() =>
        TypeAdapterConfig.GlobalSettings.Scan(typeof(ApplicationAssemblyMarker).Assembly);
}
