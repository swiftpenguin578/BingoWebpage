using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Bingo.BrowserTests;

[CollectionDefinition(Name)]
public sealed class BrowserTestGroup : ICollectionFixture<BrowserTestApplicationFactory>
{
    public const string Name = "Browser test host";
}

public sealed class BrowserTestApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_browser_tests")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();

    public async Task InitializeAsync()
    {
        await PostgreSqlReadiness.StartAsync(database);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .UseSetting("WiseOldMan:BaseUrl", "http://127.0.0.1/")
            .UseSetting("WiseOldMan:DevelopmentFake:Enabled", "false")
            .ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(BrowserTestTimeProvider.Instance);
            });
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeDatabaseAsync();

    private async Task DisposeDatabaseAsync()
    {
        await base.DisposeAsync();
        await database.DisposeAsync();
    }

    private sealed class BrowserTestTimeProvider : TimeProvider
    {
        public static BrowserTestTimeProvider Instance { get; } = new();

        private static readonly DateTimeOffset CurrentTime = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => CurrentTime;
    }
}
