global using Bingo.Testing;

using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Bingo.Testing;

// xUnit's existing default collections contain one test class each. A class
// fixture supplies that collection's lifetime without changing parallelism.
public sealed class PostgreSqlTestFixture : IAsyncLifetime
{
    private readonly object gate = new();
    private PostgreSqlContainer? container;
    private Func<DbContextOptions<ApplicationDbContext>, Task>? prepareTemplate;
    private Task? initialization;

    // The first test supplies its original builder, preserving all settings.
    public Task InitializeAsync() => Task.CompletedTask;

    // The template is migrated once; each test receives a copy of it.
    public PostgreSqlTestDatabase CreateDatabase(PostgreSqlBuilder builder) => CreateDatabase(builder, PostgreSqlTemplate.Migrate);

    // A class whose tests previously prepared each fresh database differently
    // (for example EnsureCreated) prepares the template the same way once.
    public PostgreSqlTestDatabase CreateDatabase(PostgreSqlBuilder builder, Func<DbContextOptions<ApplicationDbContext>, Task> prepare)
    {
        lock (gate)
        {
            container ??= builder.WithLoopbackPort().Build();
            prepareTemplate ??= prepare;
        }
        return new PostgreSqlTestDatabase(this);
    }

    internal async Task<string> CreateDatabaseAsync(string name)
    {
        PostgreSqlContainer database;
        Task ready;
        lock (gate)
        {
            database = container ?? throw new InvalidOperationException("The fixture needs a test-owned container builder.");
            ready = initialization ??= InitializeTemplateAsync(database, prepareTemplate ?? PostgreSqlTemplate.Migrate);
        }
        await ready;

        var settings = new NpgsqlConnectionStringBuilder(database.GetOwnedConnectionString());
        var template = settings.Database ?? throw new InvalidOperationException("The template database must be named.");
        await using (var connection = new NpgsqlConnection(AdminConnectionString(database)))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {Quote(name)} TEMPLATE {Quote(template)}", connection);
            await command.ExecuteNonQueryAsync();
        }
        settings.Database = name;
        var connectionString = settings.ConnectionString;
        await PostgreSqlReadiness.WaitAsync(connectionString);
        return connectionString;
    }

    private static async Task InitializeTemplateAsync(PostgreSqlContainer database, Func<DbContextOptions<ApplicationDbContext>, Task> prepare)
    {
        await PostgreSqlReadiness.StartAsync(database);
        // No pooled session may keep the template open while it is cloned.
        var settings = new NpgsqlConnectionStringBuilder(database.GetOwnedConnectionString()) { Pooling = false };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(settings.ConnectionString).Options;
        await prepare(options);
        await using var connection = new NpgsqlConnection(AdminConnectionString(database));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"ALTER DATABASE {Quote(settings.Database!)} WITH ALLOW_CONNECTIONS false", connection);
        await command.ExecuteNonQueryAsync();
    }

    internal async Task DropDatabaseAsync(string name, string connectionString)
    {
        // Clear this test's pool only; other collections keep their connections.
        using (var pooledConnection = new NpgsqlConnection(connectionString))
        {
            NpgsqlConnection.ClearPool(pooledConnection);
        }
        var database = container ?? throw new InvalidOperationException("The test-owned container is unavailable.");
        await using var connection = new NpgsqlConnection(AdminConnectionString(database));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE {Quote(name)} WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string AdminConnectionString(PostgreSqlContainer database)
    {
        var settings = new NpgsqlConnectionStringBuilder(database.GetOwnedConnectionString()) { Pooling = false };
        // The original builder may name postgres as its migrated template. The
        // administrative connection must remain outside that closed database.
        settings.Database = settings.Database == "postgres" ? "template1" : "postgres";
        return settings.ConnectionString;
    }

    private static string Quote(string name) => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    public Task DisposeAsync() => container?.DisposeAsync().AsTask() ?? Task.CompletedTask;
}

public static class PostgreSqlTemplate
{
    public static async Task Migrate(DbContextOptions<ApplicationDbContext> options)
    {
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public static async Task EnsureCreated(DbContextOptions<ApplicationDbContext> options)
    {
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
    }
}

public sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    private readonly PostgreSqlTestFixture fixture;
    private readonly string name = "ts_" + Guid.NewGuid().ToString("N");
    private string? connectionString;

    internal PostgreSqlTestDatabase(PostgreSqlTestFixture fixture) => this.fixture = fixture;

    public async Task StartAsync() => connectionString = await fixture.CreateDatabaseAsync(name);

    public string GetConnectionString() => connectionString ?? throw new InvalidOperationException("Start this test's database before using it.");

    public async ValueTask DisposeAsync()
    {
        if (connectionString is not null)
        {
            await fixture.DropDatabaseAsync(name, connectionString);
            connectionString = null;
        }
    }
}

public static class PostgreSqlReadiness
{
    public static async Task StartAsync(PostgreSqlContainer database)
    {
        await database.StartAsync();
        await WaitAsync(database.GetOwnedConnectionString());
    }

    public static async Task WaitAsync(string connectionString)
    {
        using var readiness = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var settings = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };
        Exception? lastFailure = null;
        while (!readiness.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(settings.ConnectionString);
                await connection.OpenAsync(readiness.Token);
                await using var command = new NpgsqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync(readiness.Token);
                return;
            }
            catch (Exception error) when (error is NpgsqlException or TimeoutException or OperationCanceledException)
            {
                lastFailure = error;
            }
            // A bounded readiness poll, only after a failed credentialed query.
            try { await Task.Delay(250, readiness.Token); }
            catch (OperationCanceledException) { break; }
        }
        throw new InvalidOperationException("Test-owned PostgreSQL did not accept its configured credentials within 60 seconds.", lastFailure);
    }
}
