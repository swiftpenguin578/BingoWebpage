// Standalone infrastructure probe; not a discovered xUnit test.
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Bingo.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

static PostgreSqlBuilder Builder() => new PostgreSqlBuilder("postgres:17-alpine")
    .WithDatabase("ts_provisioning_template").WithUsername("bingo").WithPassword("synthetic-ts-probe");
static ApplicationDbContext Db(string connectionString) => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options);

var fixture = new PostgreSqlTestFixture();
var first = fixture.CreateDatabase(Builder());
var second = fixture.CreateDatabase(Builder());
PostgreSqlTestDatabase? third = null;
try
{
    await fixture.InitializeAsync();
    await Task.WhenAll(first.StartAsync(), second.StartAsync());
    var firstSettings = new NpgsqlConnectionStringBuilder(first.GetConnectionString());
    var secondSettings = new NpgsqlConnectionStringBuilder(second.GetConnectionString());
    if (firstSettings.Database == secondSettings.Database || firstSettings.Host != secondSettings.Host || firstSettings.Port != secondSettings.Port)
        throw new InvalidOperationException("Clones must have distinct names on one container endpoint.");
    if (firstSettings.Username != "bingo" || firstSettings.Password != "synthetic-ts-probe" || secondSettings.Username != firstSettings.Username || secondSettings.Password != firstSettings.Password)
        throw new InvalidOperationException("Configured credentials were not preserved.");
    string[] migrations;
    await using (var firstDb = Db(first.GetConnectionString()))
    await using (var secondDb = Db(second.GetConnectionString()))
    {
        migrations = (await firstDb.Database.GetAppliedMigrationsAsync()).ToArray();
        if (migrations.Length == 0 || !migrations.SequenceEqual(await secondDb.Database.GetAppliedMigrationsAsync()))
            throw new InvalidOperationException("Both clones must contain the same migrated schema.");
        if (await firstDb.Accounts.CountAsync() != 0 || await secondDb.Accounts.CountAsync() != 0)
            throw new InvalidOperationException("Both clones must start with no accounts.");
        var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        firstDb.Accounts.Add(Account.CreateWebsite(Guid.NewGuid(), "ts-probe", "TS-PROBE", now));
        await firstDb.SaveChangesAsync();
        if (await firstDb.Accounts.CountAsync() != 1 || await secondDb.Accounts.CountAsync() != 0)
            throw new InvalidOperationException("A write leaked between test databases.");
        if (await secondDb.Database.SqlQuery<bool>($"SELECT datallowconn AS \"Value\" FROM pg_database WHERE datname = {"ts_provisioning_template"}").SingleAsync())
            throw new InvalidOperationException("The migrated template must reject ordinary connections.");
    }
    await first.DisposeAsync();
    await using (var secondDb = Db(second.GetConnectionString()))
    {
        if (await secondDb.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_database WHERE datname = {firstSettings.Database}").SingleAsync() != 0)
            throw new InvalidOperationException("The first test database was not dropped.");
        if (await secondDb.Accounts.CountAsync() != 0)
            throw new InvalidOperationException("Dropping one clone affected another clone.");
    }
    third = fixture.CreateDatabase(Builder());
    await third.StartAsync();
    await using (var thirdDb = Db(third.GetConnectionString()))
    {
        if (await thirdDb.Accounts.CountAsync() != 0 || !migrations.SequenceEqual(await thirdDb.Database.GetAppliedMigrationsAsync()))
            throw new InvalidOperationException("A later clone was not clean and fully migrated.");
    }
    await third.DisposeAsync();
    await using (var secondDb = Db(second.GetConnectionString()))
    {
        var remaining = await secondDb.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_database WHERE datname LIKE {"ts_%"}").SingleAsync();
        if (remaining != 2) throw new InvalidOperationException("Only the template and the second clone should remain.");
    }
    Console.WriteLine($"PASS: concurrent unique clones, one endpoint, preserved credentials, {migrations.Length} migrations, isolated writes, immutable template, clean later clone, and database cleanup.");
}
finally
{
    try { await first.DisposeAsync(); await second.DisposeAsync(); if (third is not null) await third.DisposeAsync(); }
    finally { await fixture.DisposeAsync(); }
}
