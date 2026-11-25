using System.Reflection;
using MiniErp.Application;
using MiniErp.Domain.Common.Entities;
using MiniErp.Infrastructure;

namespace MiniErp.Tests.Architecture;

/// <summary>
/// Guards the Clean Architecture dependency direction:
/// Domain -> nothing, Application -> Domain, Infrastructure -> Application + Domain.
/// </summary>
public sealed class DependencyRuleTests
{
    private const string DomainName = "MiniErp.Domain";
    private const string ApplicationName = "MiniErp.Application";
    private const string InfrastructureName = "MiniErp.Infrastructure";
    private const string ApiName = "MiniErp.Api";

    private static readonly Assembly Domain = typeof(AuditableEntity).Assembly;
    private static readonly Assembly Application = typeof(ApplicationAssemblyMarker).Assembly;
    private static readonly Assembly Infrastructure = typeof(InfrastructureAssemblyMarker).Assembly;

    [Fact]
    public void Domain_DoesNotReferenceOtherLayersOrEfCore()
    {
        var references = ReferencedNames(Domain);

        Assert.DoesNotContain(ApplicationName, references);
        Assert.DoesNotContain(InfrastructureName, references);
        Assert.DoesNotContain(ApiName, references);
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_ReferencesOnlyDomainAmongProjectLayers()
    {
        var references = ReferencedNames(Application);

        Assert.Contains(DomainName, references);
        Assert.DoesNotContain(InfrastructureName, references);
        Assert.DoesNotContain(ApiName, references);
    }

    [Fact]
    public void Application_DoesNotReferenceEfCore()
    {
        var references = ReferencedNames(Application);

        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_DoesNotReferenceApi()
    {
        Assert.DoesNotContain(ApiName, ReferencedNames(Infrastructure));
    }

    private static HashSet<string> ReferencedNames(Assembly assembly) =>
        assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);
}
