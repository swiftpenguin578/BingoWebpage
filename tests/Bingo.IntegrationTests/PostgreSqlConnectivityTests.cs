using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class PostgreSqlConnectivityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_tests")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();

    public Task InitializeAsync() => _database.StartAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [Fact]
    public async Task ContextCanCreateAndQueryTheFoundationSchema()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_database.GetConnectionString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.SystemMetadata.Add(new SystemMetadata
        {
            Key = "foundation",
            Value = "ready",
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var value = await context.SystemMetadata
            .Where(item => item.Key == "foundation")
            .Select(item => item.Value)
            .SingleAsync();

        Assert.Equal("ready", value);
    }
}
