namespace Bingo.Application.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void ApplicationReferencesDomain()
    {
        var references = typeof(AssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Contains("Bingo.Domain", references);
        Assert.DoesNotContain("Bingo.Infrastructure", references);
        Assert.DoesNotContain("Bingo.Web", references);
    }
}
