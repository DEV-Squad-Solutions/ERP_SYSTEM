using System.Reflection;
using Mapster;

namespace MiniErp.Application.Common.Mappings;

public static class MappingConfiguration
{
    public static void Register(params Assembly[] additionalAssemblies)
    {
        var assemblies = additionalAssemblies
            .Prepend(typeof(ApplicationAssemblyMarker).Assembly)
            .Distinct()
            .ToArray();

        TypeAdapterConfig.GlobalSettings.Scan(assemblies);
    }
}
