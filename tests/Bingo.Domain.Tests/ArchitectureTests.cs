using System.Reflection;

namespace Bingo.Domain.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void DomainDoesNotReferenceOuterLayers()
    {
        var references = typeof(AssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("Bingo.Application", references);
        Assert.DoesNotContain("Bingo.Infrastructure", references);
        Assert.DoesNotContain("Bingo.Web", references);
    }
}
