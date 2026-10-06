// Standalone deterministic Docker Desktop reproduction, linked by a scratch net10 console.
using Bingo.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

static PostgreSqlBuilder Builder(string user, string password) => new PostgreSqlBuilder("postgres:17-alpine")
    .WithDatabase("u2_endpoint_probe").WithUsername(user).WithPassword(password);
static async Task<string> Identity(string connectionString)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand("SELECT current_user || ':' || system_identifier::text FROM pg_control_system()", connection);
    return (string)(await command.ExecuteScalarAsync())!;
}
await using var review = Builder("review_probe", "synthetic-review-endpoint").WithLoopbackPort().Build();
await PostgreSqlReadiness.StartAsync(review);
var port = new NpgsqlConnectionStringBuilder(review.GetOwnedConnectionString()).Port;
Console.WriteLine("Loopback peer port=" + port + " identity=" + await Identity(review.GetOwnedConnectionString()));
// Original wildcard builder: same numeric port, different IP allocation pool.
await using (var wildcard = Builder("bingo", "synthetic-test-endpoint").WithPortBinding(port, 5432).Build())
{
    await wildcard.StartAsync();
    var native = await wildcard.ExecScriptAsync("SELECT current_user, system_identifier FROM pg_control_system();");
    if (native.ExitCode != 0 || !native.Stdout.Contains("bingo", StringComparison.Ordinal)) throw new InvalidOperationException("Wildcard server credentials were not initialized.");
    Console.WriteLine("Wildcard server is initialized: " + native.Stdout.Trim());
    var ipv4 = new NpgsqlConnectionStringBuilder(wildcard.GetConnectionString()) { Host = "127.0.0.1", Pooling = false };
    try { await Identity(ipv4.ConnectionString); throw new InvalidOperationException("Expected shadowed-endpoint rejection."); }
    catch (PostgresException error) when (error.SqlState == "28P01") { Console.WriteLine("REPRODUCED: intended wildcard credentials reach loopback peer: 28P01."); }
}
// Production tests request random ports, not a forced occupied numeric port.
// Validate the actual fix: same-address automatic allocation plus explicit host.
await using var isolated = Builder("bingo", "synthetic-test-endpoint").WithLoopbackPort().Build();
await PostgreSqlReadiness.StartAsync(isolated);
var fixedSettings = new NpgsqlConnectionStringBuilder(isolated.GetOwnedConnectionString());
if (fixedSettings.Host != "127.0.0.1" || fixedSettings.Port == port) throw new InvalidOperationException("Endpoint isolation failed.");
Console.WriteLine("FIX: separate loopback port=" + fixedSettings.Port + " identity=" + await Identity(fixedSettings.ConnectionString));
Console.WriteLine("PASS: deterministic endpoint reproduction and fixed allocation; all probe containers disposed.");
