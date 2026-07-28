using Mapster;
using MiniErp.Application.Common.Mappings;
using MiniErp.Infrastructure;

namespace MiniErp.Tests.Mappings;

public sealed class MappingConfigurationTests
{
    static MappingConfigurationTests()
    {
        MappingConfiguration.Register(
            typeof(InfrastructureAssemblyMarker).Assembly);
    }

    [Fact]
    public void AllRegisteredMappingsCompile()
    {
        TypeAdapterConfig.GlobalSettings.Compile();
    }
}
