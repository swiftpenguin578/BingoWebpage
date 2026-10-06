using Bingo.Domain.Catalogue;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Fact]
    public async Task Cat1ActivityTeamSizeUsesExplicitContractAndMissingInputPreservesStoredValue()
    {
        var (actor, boss, _, _) = await PriceFixtureAsync();
        await using (var role = new ApplicationDbContext(options))
        {
            role.Accounts.Single(account => account.Id == actor.Id).SetGlobalRole(GlobalRole.Admin);
            await role.SaveChangesAsync();
        }
        decimal? originalEhb;
        await using (var configured = new ApplicationDbContext(options))
        {
            var current = await configured.BossActivities.SingleAsync(value => value.Id == boss.Id);
            current.SetTeamSize(3);
            originalEhb = await configured.SourceDrops.Where(value => value.BossActivityId == boss.Id).Select(value => value.DefaultEhbEstimate).SingleAsync();
            await configured.SaveChangesAsync();
        }

        await using (var explicitUpdate = new ApplicationDbContext(options))
        {
            var current = await explicitUpdate.BossActivities.SingleAsync(value => value.Id == boss.Id);
            var page = CataloguePage(explicitUpdate, actor.Id, false);
            Assert.IsType<RedirectToPageResult>(await page.OnPostUpdateBossAsync(
                current.Id, current.Version, current.Name, current.Category, current.EfficientCompletionsPerHour,
                null, null, CancellationToken.None, 5));
        }

        await using (var verifyExplicit = new ApplicationDbContext(options))
            Assert.Equal(originalEhb, await verifyExplicit.SourceDrops.Where(value => value.BossActivityId == boss.Id).Select(value => value.DefaultEhbEstimate).SingleAsync());

        await using (var preserving = new ApplicationDbContext(options))
        {
            var current = await preserving.BossActivities.SingleAsync(value => value.Id == boss.Id);
            var page = CataloguePage(preserving, actor.Id, false);
            Assert.IsType<RedirectToPageResult>(await page.OnPostUpdateBossAsync(
                current.Id, current.Version, current.Name, current.Category, current.EfficientCompletionsPerHour,
                null, null, CancellationToken.None));
        }

        await using (var rejected = new ApplicationDbContext(options))
        {
            var current = await rejected.BossActivities.SingleAsync(value => value.Id == boss.Id);
            var page = CataloguePage(rejected, actor.Id, false);
            Assert.IsType<RedirectToPageResult>(await page.OnPostUpdateBossAsync(
                current.Id, current.Version, current.Name, current.Category, current.EfficientCompletionsPerHour,
                null, null, CancellationToken.None, 0));
            Assert.Contains("at least 1", page.TempData["StatusMessage"]?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(5, await verify.BossActivities.Where(value => value.Id == boss.Id).Select(value => value.TeamSize).SingleAsync());
    }

    [Fact]
    public async Task Cat1ActivityTeamSizeAddUsesExplicitValueAndDefaultsToOne()
    {
        var (actor, _, _, _) = await PriceFixtureAsync();
        await using (var role = new ApplicationDbContext(options))
        {
            role.Accounts.Single(account => account.Id == actor.Id).SetGlobalRole(GlobalRole.Admin);
            await role.SaveChangesAsync();
        }
        var explicitName = $"Team size activity {Guid.NewGuid():N}";
        await using (var adding = new ApplicationDbContext(options))
        {
            var page = CataloguePage(adding, actor.Id, false);
            page.PageContext.HttpContext.RequestServices = new ServiceCollection().AddMvcCore().AddDataAnnotations().Services.BuildServiceProvider();
            page.Boss = new IndexModel.BossInput
            {
                Name = explicitName,
                Category = "Boss",
                EfficientRate = 10,
                TeamSize = 4
            };
            Assert.IsType<RedirectToPageResult>(await page.OnPostBossAsync(CancellationToken.None));
        }

        await using (var verify = new ApplicationDbContext(options))
        {
            var explicitValue = await verify.BossActivities.SingleAsync(value => value.Name == explicitName);
            Assert.Equal(4, explicitValue.TeamSize);
        }

        var defaultName = $"Default team size activity {Guid.NewGuid():N}";
        await using (var addingDefault = new ApplicationDbContext(options))
        {
            var page = CataloguePage(addingDefault, actor.Id, false);
            page.PageContext.HttpContext.RequestServices = new ServiceCollection().AddMvcCore().AddDataAnnotations().Services.BuildServiceProvider();
            page.Boss = new IndexModel.BossInput
            {
                Name = defaultName,
                Category = "Boss",
                EfficientRate = 10
            };
            Assert.IsType<RedirectToPageResult>(await page.OnPostBossAsync(CancellationToken.None));
        }

        await using var verifyDefault = new ApplicationDbContext(options);
        Assert.Equal(1, await verifyDefault.BossActivities.Where(value => value.Name == defaultName).Select(value => value.TeamSize).SingleAsync());
    }
}

public sealed class Cat1TeamSizeMigrationIntegrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261004112050_AddCompetitionEndUpdateState";
    private const string TeamSizeMigration = "20261004133947_AddBossActivityTeamSize";
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_cat1_team_size_migration")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await PostgreSqlReadiness.StartAsync(database);
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task Cat1MigrationFailsClosedForNonDefaultContextThenBackfillsAndRollsBack()
    {
        var bossId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        await using (var legacy = new ApplicationDbContext(options))
        {
            await legacy.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            var itemId = Guid.NewGuid();
            var dropId = Guid.NewGuid();
            await legacy.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO boss_activities (id, name, slug, category, efficient_completions_per_hour, data_updated_at, active, version)
                VALUES ({bossId}, {"Legacy team activity"}, {"legacy-team-activity"}, {"Boss"}, {10m}, {now}, {true}, {1L});
                INSERT INTO catalogue_items (id, name, normalized_name, active, version)
                VALUES ({itemId}, {"Legacy team item"}, {"LEGACY TEAM ITEM"}, {true}, {1L});
                INSERT INTO source_drops (id, boss_activity_id, item_id, display_rate, numeric_probability, default_ehb_estimate,
                    probability_scope, conditional_on_parent, assumed_participants, rolls_per_completion, roll_group,
                    data_updated_at, active, version)
                VALUES ({dropId}, {bossId}, {itemId}, {"1/100"}, {.01m}, {10m}, {"Team"}, {false}, {2}, {1}, {"default"}, {now}, {true}, {1L});
                """);
        }

        await using (var blocked = new ApplicationDbContext(options))
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() => blocked.GetService<IMigrator>().MigrateAsync());
            Assert.Contains("non-default participant context", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(await blocked.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'boss_activities' AND column_name = 'team_size') AS \"Value\"").SingleAsync());
        }

        await using (var repaired = new ApplicationDbContext(options))
        {
            await repaired.Database.ExecuteSqlRawAsync("UPDATE source_drops SET assumed_participants = 1, probability_scope = 'Participant'");
            await repaired.GetService<IMigrator>().MigrateAsync();
        }

        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(1, await verify.BossActivities.Where(value => value.Id == bossId).Select(value => value.TeamSize).SingleAsync());
            Assert.Contains(TeamSizeMigration, await verify.Database.GetAppliedMigrationsAsync());
        }

        await using (var down = new ApplicationDbContext(options))
        {
            await down.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            Assert.False(await down.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'boss_activities' AND column_name = 'team_size') AS \"Value\"").SingleAsync());
        }
    }
}
