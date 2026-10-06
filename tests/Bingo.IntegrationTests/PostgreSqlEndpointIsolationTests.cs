using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class PostgreSqlEndpointIsolationTests
{
    [Fact]
    public async Task ConcurrentOwnedEndpointsKeepTheirAddressCredentialsAndServerIdentity()
    {
        string? reviewBinding = null, testBinding = null;
        await using var review = new PostgreSqlBuilder("postgres:17-alpine").WithLoopbackPort()
            .WithDatabase("endpoint_review").WithUsername("review_probe").WithPassword("synthetic-review-endpoint")
            .WithCreateParameterModifier(parameters => reviewBinding = parameters.HostConfig!.PortBindings["5432/tcp"][0].HostIP).Build();
        await using var test = new PostgreSqlBuilder("postgres:17-alpine").WithLoopbackPort()
            .WithDatabase("endpoint_test").WithUsername("bingo").WithPassword("synthetic-test-endpoint")
            .WithCreateParameterModifier(parameters => testBinding = parameters.HostConfig!.PortBindings["5432/tcp"][0].HostIP).Build();
        await Task.WhenAll(PostgreSqlReadiness.StartAsync(review), PostgreSqlReadiness.StartAsync(test));
        var reviewSettings = new NpgsqlConnectionStringBuilder(review.GetOwnedConnectionString());
        var testSettings = new NpgsqlConnectionStringBuilder(test.GetOwnedConnectionString());
        if (review.Hostname == "localhost")
        {
            Assert.Equal("127.0.0.1", reviewBinding); Assert.Equal("127.0.0.1", testBinding);
            Assert.Equal("127.0.0.1", reviewSettings.Host); Assert.Equal("127.0.0.1", testSettings.Host);
        }
        Assert.NotEqual(reviewSettings.Port, testSettings.Port);
        var identities = new List<string>();
        foreach (var database in new[] { review, test })
        {
            var native = await database.ExecScriptAsync("SELECT system_identifier FROM pg_control_system();");
            Assert.Equal(0, native.ExitCode);
            await using var connection = new NpgsqlConnection(database.GetOwnedConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT system_identifier::text FROM pg_control_system()", connection);
            var identity = Assert.IsType<string>(await command.ExecuteScalarAsync());
            Assert.Contains(identity, native.Stdout, StringComparison.Ordinal);
            identities.Add(identity);
        }
        Assert.NotEqual(identities[0], identities[1]);
        testSettings.Password = reviewSettings.Password; testSettings.Pooling = false;
        await using var wrong = new NpgsqlConnection(testSettings.ConnectionString);
        var rejected = await Assert.ThrowsAsync<PostgresException>(() => wrong.OpenAsync());
        Assert.Equal("28P01", rejected.SqlState); // Credentials stay strict; no readiness/class retry.
    }
}
