namespace Bingo.Application;

/// <summary>
/// Identifies the application assembly for registration and architecture tests.
/// </summary>
public sealed class AssemblyMarker;

public static class ApplicationDependencies
{
    public static Type DomainAssemblyMarker => typeof(Bingo.Domain.AssemblyMarker);
}
