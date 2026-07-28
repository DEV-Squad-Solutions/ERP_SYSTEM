using System.Reflection;
using Mapster;

namespace MiniErp.Application.Common.Mappings;

public static class MappingConfiguration
{
    private static readonly object Sync = new();
    private static bool _isRegistered;

    public static void Register(params Assembly[] additionalAssemblies)
    {
        if (_isRegistered)
        {
            return;
        }

        lock (Sync)
        {
            if (_isRegistered)
            {
                return;
            }

            var assemblies = additionalAssemblies
                .Prepend(typeof(ApplicationAssemblyMarker).Assembly)
                .Distinct()
                .ToArray();

            TypeAdapterConfig.GlobalSettings.Scan(assemblies);
            _isRegistered = true;
        }
    }
}
