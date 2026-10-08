using DotNet.Testcontainers.Configurations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.Testing;

// Local Docker's wildcard and loopback port pools can allocate the same port.
// Publish and connect on the same address as the owned UI review environment.
// Remote Docker/DinD endpoints retain their existing connection behavior.
public static class PostgreSqlTestEndpoint
{
    public static PostgreSqlBuilder WithLoopbackPort(this PostgreSqlBuilder builder)
    {
        var endpoint = TestcontainersSettings.OS.DockerEndpointAuthConfig.Endpoint;
        var hostOverride = TestcontainersSettings.DockerHostOverride;
        var local = endpoint.Scheme is "unix" or "npipe" || endpoint.IsLoopback;
        if (!local || File.Exists("/.dockerenv") ||
            (hostOverride is not null && hostOverride is not ("localhost" or "127.0.0.1")))
            return builder;
        return builder.WithCreateParameterModifier(parameters =>
        {
            var bindings = parameters.HostConfig?.PortBindings
                ?? throw new InvalidOperationException("Test-owned PostgreSQL needs a published port.");
            foreach (var binding in bindings["5432/tcp"])
                binding.HostIP = "127.0.0.1";
        });
    }

    public static string GetOwnedConnectionString(this PostgreSqlContainer database)
    {
        var settings = new NpgsqlConnectionStringBuilder(database.GetConnectionString());
        if (settings.Host == "localhost") settings.Host = "127.0.0.1";
        return settings.ConnectionString;
    }
}
