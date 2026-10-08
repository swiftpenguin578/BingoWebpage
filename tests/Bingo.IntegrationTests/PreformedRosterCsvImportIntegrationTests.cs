using System.Text;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Bingo.Web.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class PreformedRosterCsvImportIntegrationTests(PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("bingo_preformed_csv").WithUsername("bingo").WithPassword("bingo_test_password"));
    private DbContextOptions<ApplicationDbContext> options = null!;
    public async Task InitializeAsync() { await database.StartAsync(); options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options; await using var db = new ApplicationDbContext(options); }
    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task PreviewIsNonMutatingAndApplyCreatesTheAuthoritativePreformedRosterExactlyOnce()
    {
        var (actor, ev, team) = await SeedAsync();
        await using var db = new ApplicationDbContext(options); using var cache = new MemoryCache(new MemoryCacheOptions()); var service = new PreformedRosterCsvImportService(db, new EventParticipantCharacterService(db, TimeProvider.System), cache, TimeProvider.System);
        await using var previewStream = new MemoryStream(Encoding.UTF8.GetBytes("Account,EHB,Account,Account\r\nMain One,12.5,,Shared Alt\r\nMain Two,0,,\r\n"));
        var preview = await service.PreviewAsync(actor.Id, ev.Id, team.Id, previewStream, CancellationToken.None);
        Assert.True(preview.IsValid); Assert.Equal(0, await db.EventParticipants.CountAsync()); Assert.Equal(0, await db.OsrsCharacters.CountAsync());
        var applied = await service.ApplyAsync(actor.Id, actor.LoginName, ev.Id, team.Id, preview.Nonce!, CancellationToken.None);
        Assert.True(applied.Succeeded); Assert.Equal(2, await db.EventParticipants.CountAsync(x => x.AccountId == null)); Assert.Equal(3, await db.EventParticipantCharacters.CountAsync()); Assert.Equal(2, await db.TeamMemberships.CountAsync(x => x.Source == TeamMembershipSource.PreformedCsv && x.LeftAt == null)); Assert.Single(await db.AuditEntries.Where(x => x.Action == "team.preformed_roster_csv_imported").ToListAsync());
        var assignments = await (from assignment in db.EventParticipantCharacters
                                 join character in db.OsrsCharacters on assignment.OsrsCharacterId equals character.Id
                                 select new { character.DisplayName, assignment.EventRole, assignment.EhbSnapshot }).ToListAsync();
        Assert.Contains(assignments, x => x.DisplayName == "Main One" && x.EventRole == EventCharacterRole.Playing && x.EhbSnapshot == 12.5m);
        Assert.Contains(assignments, x => x.DisplayName == "Main Two" && x.EventRole == EventCharacterRole.Playing && x.EhbSnapshot == 0m);
        Assert.Contains(assignments, x => x.DisplayName == "Shared Alt" && x.EventRole == EventCharacterRole.Informational && x.EhbSnapshot == null);
        Assert.False((await service.ApplyAsync(actor.Id, actor.LoginName, ev.Id, team.Id, preview.Nonce!, CancellationToken.None)).Succeeded);
    }

    [Theory]
    [InlineData("Account,EHB,Account\r\n,12.5,Secondary\r\n")]
    [InlineData("Account;EHB;Account;Account\r\n  ;12,5;;Secondary\r\n")]
    public async Task BlankPrimaryCannotPromoteAnOptionalAccount(string csv)
    {
        var (actor, ev, team) = await SeedAsync();
        await using var db = new ApplicationDbContext(options);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new PreformedRosterCsvImportService(db, new EventParticipantCharacterService(db, TimeProvider.System), cache, TimeProvider.System);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var preview = await service.PreviewAsync(actor.Id, ev.Id, team.Id, stream, CancellationToken.None);
        Assert.False(preview.IsValid);
        Assert.Null(preview.Nonce);
        Assert.Contains(preview.Errors, error => error.Number == 2 && error.Message == "Primary Account is required.");
        Assert.False((await service.ApplyAsync(actor.Id, actor.LoginName, ev.Id, team.Id, preview.Nonce ?? "", CancellationToken.None)).Succeeded);
        Assert.Equal(0, await db.EventParticipants.CountAsync());
        Assert.Equal(0, await db.OsrsCharacters.CountAsync());
        Assert.Equal(0, await db.TeamMemberships.CountAsync());
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task NumbersSemicolonRosterWithIntegerAndDecimalEhbPreviewsAndApplies()
    {
        var (actor, ev, team) = await SeedAsync();
        await using var db = new ApplicationDbContext(options);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new PreformedRosterCsvImportService(db, new EventParticipantCharacterService(db, TimeProvider.System), cache, TimeProvider.System);
        const string csv = "Account;EHB\r\nAccount1;5\r\nAccount2;5,5\r\nAccount3;5.25\r\nAccount4;5\r\nAccount5;5\r\nAccount6;5\r\nAccount7;5\r\n";

        await using var previewStream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var preview = await service.PreviewAsync(actor.Id, ev.Id, team.Id, previewStream, CancellationToken.None);
        Assert.True(preview.IsValid);
        Assert.Equal(7, preview.Rows.Count);
        Assert.Equal(5m, preview.Rows[0].Ehb);
        Assert.Equal(5.5m, preview.Rows[1].Ehb);
        Assert.Equal(5.25m, preview.Rows[2].Ehb);

        Assert.True((await service.ApplyAsync(actor.Id, actor.LoginName, ev.Id, team.Id, preview.Nonce!, CancellationToken.None)).Succeeded);
        Assert.Equal(7, await db.EventParticipants.CountAsync());
        Assert.Equal(7, await db.TeamMemberships.CountAsync());
    }

    [Theory]
    [InlineData("Account,EHB\r\nMain,not-a-number\r\n")]
    [InlineData("Account,EHB\r\nMain,1\r\nmain,2\r\n")]
    [InlineData("Account,Account\r\nMain,1\r\n")]
    [InlineData("Account;EHB\r\nMain,1\r\n")]
    public async Task InvalidPreviewWritesNothing(string csv)
    {
        var (actor, ev, team) = await SeedAsync(); await using var db = new ApplicationDbContext(options); using var cache = new MemoryCache(new MemoryCacheOptions()); var service = new PreformedRosterCsvImportService(db, new EventParticipantCharacterService(db, TimeProvider.System), cache, TimeProvider.System); await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        Assert.False((await service.PreviewAsync(actor.Id, ev.Id, team.Id, stream, CancellationToken.None)).IsValid); Assert.Equal(0, await db.EventParticipants.CountAsync()); Assert.Equal(0, await db.EventParticipantCharacters.CountAsync()); Assert.Equal(0, await db.TeamMemberships.CountAsync()); Assert.Equal(0, await db.AuditEntries.CountAsync());
    }
    [Fact]
    public async Task NewInvalidRsnIsRefusedPerRowAndNothingIsWrittenWhileStoredNamesStayUntouched()
    {
        var (actor, ev, team) = await SeedAsync();
        await using var db = new ApplicationDbContext(options); using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new PreformedRosterCsvImportService(db, new EventParticipantCharacterService(db, TimeProvider.System), cache, TimeProvider.System);
        await using var bad = new MemoryStream(Encoding.UTF8.GetBytes("Account,EHB,Account\r\nGood Name,1,\r\nBad.Name!!,2,\r\nFine One,3,Way Too Long Name 1\r\n"));
        var refused = await service.PreviewAsync(actor.Id, ev.Id, team.Id, bad, CancellationToken.None);
        Assert.False(refused.IsValid); Assert.Null(refused.Nonce);
        Assert.Equal([3, 4], refused.Errors.Select(error => error.Number).Order().ToArray());
        Assert.All(refused.Errors, error => Assert.Equal(RsnRule.Message, error.Message));
        Assert.False((await service.ApplyAsync(actor.Id, actor.LoginName, ev.Id, team.Id, refused.Nonce ?? "", CancellationToken.None)).Succeeded);
        Assert.Equal(0, await db.EventParticipants.CountAsync()); Assert.Equal(0, await db.OsrsCharacters.CountAsync()); Assert.Equal(0, await db.EventParticipantCharacters.CountAsync()); Assert.Equal(0, await db.TeamMemberships.CountAsync()); Assert.Equal(0, await db.AuditEntries.CountAsync());

        // A stored name that predates the rule is not re-validated: it imports and its stored display name is unchanged.
        db.OsrsCharacters.Add(new OsrsCharacter(Guid.NewGuid(), "Legacy.Name!!", SignupService.NormalizeAccountName("Legacy.Name!!"), DateTimeOffset.UtcNow)); await db.SaveChangesAsync();
        await using var legacy = new MemoryStream(Encoding.UTF8.GetBytes("Account,EHB\r\nLegacy.Name!!,1\r\n"));
        var preview = await service.PreviewAsync(actor.Id, ev.Id, team.Id, legacy, CancellationToken.None);
        Assert.True(preview.IsValid);
        Assert.True((await service.ApplyAsync(actor.Id, actor.LoginName, ev.Id, team.Id, preview.Nonce!, CancellationToken.None)).Succeeded);
        Assert.Equal("Legacy.Name!!", await db.OsrsCharacters.Select(x => x.DisplayName).SingleAsync());
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("team")]
    [InlineData("expired")]
    public async Task PreviewTokenBoundaryRejectsWithoutResidue(string kind)
    {
        var (actor, ev, team) = await SeedAsync(); var clock = new TestClock(DateTimeOffset.UtcNow); await using var db = new ApplicationDbContext(options); using var cache = new MemoryCache(new MemoryCacheOptions()); var service = new PreformedRosterCsvImportService(db, new EventParticipantCharacterService(db, clock), cache, clock); const string csv = "Account,EHB\r\nBound Main,1\r\n"; await using var previewStream = new MemoryStream(Encoding.UTF8.GetBytes(csv)); var preview = await service.PreviewAsync(actor.Id, ev.Id, team.Id, previewStream, CancellationToken.None); var other = kind == "actor" ? Guid.NewGuid() : actor.Id; var target = kind == "team" ? Guid.NewGuid() : team.Id; if (kind == "expired") clock.Advance(TimeSpan.FromMinutes(11));
        Assert.False((await service.ApplyAsync(other, actor.LoginName, ev.Id, target, preview.Nonce!, CancellationToken.None)).Succeeded); Assert.Equal(0, await db.EventParticipants.CountAsync()); Assert.Equal(0, await db.OsrsCharacters.CountAsync()); Assert.Equal(0, await db.TeamMemberships.CountAsync()); Assert.Equal(0, await db.AuditEntries.CountAsync());
    }
    [Fact]
    public async Task CurrentReservationAndConcurrentApplyFailClosed()
    {
        var (actor, ev, team) = await SeedAsync(); await using var first = new ApplicationDbContext(options); await using var second = new ApplicationDbContext(options); using var cache = new MemoryCache(new MemoryCacheOptions()); var one = new PreformedRosterCsvImportService(first, new EventParticipantCharacterService(first, TimeProvider.System), cache, TimeProvider.System); var two = new PreformedRosterCsvImportService(second, new EventParticipantCharacterService(second, TimeProvider.System), cache, TimeProvider.System); const string csv = "Account,EHB\r\nRace Main,1\r\n"; await using var previewStream = new MemoryStream(Encoding.UTF8.GetBytes(csv)); var preview = await one.PreviewAsync(actor.Id, ev.Id, team.Id, previewStream, CancellationToken.None); var results = await Task.WhenAll(one.ApplyAsync(actor.Id, actor.LoginName, ev.Id, team.Id, preview.Nonce!, CancellationToken.None), two.ApplyAsync(actor.Id, actor.LoginName, ev.Id, team.Id, preview.Nonce!, CancellationToken.None)); Assert.Equal(1, results.Count(result => result.Succeeded)); await using var verify = new ApplicationDbContext(options); Assert.Equal(1, await verify.EventParticipants.CountAsync()); Assert.Equal(1, await verify.TeamMemberships.CountAsync()); Assert.Single(await verify.AuditEntries.Where(x => x.Action == "team.preformed_roster_csv_imported").ToListAsync());
    }
    [Fact]
    public async Task PreviewThenCurrentReservationConflictRollsBackTheEntireApply()
    {
        var (actor, ev, team) = await SeedAsync(); await using var db = new ApplicationDbContext(options); using var cache = new MemoryCache(new MemoryCacheOptions()); var service = new PreformedRosterCsvImportService(db, new EventParticipantCharacterService(db, TimeProvider.System), cache, TimeProvider.System); const string csv = "Account,EHB\r\nReserved Main,1\r\nSecond Main,2\r\n"; await using var previewStream = new MemoryStream(Encoding.UTF8.GetBytes(csv)); var preview = await service.PreviewAsync(actor.Id, ev.Id, team.Id, previewStream, CancellationToken.None); var participant = new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, 1, DateTimeOffset.UtcNow, SignupSource.AdminCreated); var character = new OsrsCharacter(Guid.NewGuid(), "Reserved Main", "RESERVED MAIN", DateTimeOffset.UtcNow); db.AddRange(participant, character, new EventParticipantCharacter(Guid.NewGuid(), ev.Id, participant.Id, character.Id, 0, DateTimeOffset.UtcNow, actor.Id, null, EventCharacterRole.Playing, 5m, EhbSource.AdminCorrection, null)); await db.SaveChangesAsync(); Assert.False((await service.ApplyAsync(actor.Id, actor.LoginName, ev.Id, team.Id, preview.Nonce!, CancellationToken.None)).Succeeded); Assert.Equal(1, await db.EventParticipants.CountAsync()); Assert.Equal(1, await db.OsrsCharacters.CountAsync()); Assert.Equal(1, await db.EventParticipantCharacters.CountAsync()); Assert.Equal(0, await db.TeamMemberships.CountAsync()); Assert.Equal(0, await db.AuditEntries.CountAsync());
    }
    [Fact]
    public async Task OperatorCsvTemplateRemainsAvailableWhileDraftRetiresTheCsvSurface()
    {
        var markup = await File.ReadAllTextAsync(Path.Combine(FindRepositoryRoot(), "src", "Bingo.Web", "Pages", "Admin", "Events", "Draft.cshtml"));
        Assert.Equal("Account,EHB\r\n", Encoding.UTF8.GetString(PreformedRosterCsvImportService.Template()));
        Assert.Contains("includedInDraft", markup);
        Assert.DoesNotContain("Import external roster CSV", markup);
        Assert.DoesNotContain("RosterCsv", markup);
        Assert.DoesNotContain("TeamFormationType.Preformed", markup);
    }
    private async Task<(Account Actor, BingoEvent Event, Team Team)> SeedAsync()
    { var now = DateTimeOffset.UtcNow; await using var db = new ApplicationDbContext(options); var actor = Account.CreateWebsite(Guid.NewGuid(), "csv-admin-" + Guid.NewGuid(), "CSVADMIN" + Guid.NewGuid().ToString("N"), now); actor.SetGlobalRole(GlobalRole.Admin); var ev = new BingoEvent(Guid.NewGuid(), "CSV event " + Guid.NewGuid(), "csv-" + Guid.NewGuid().ToString("N"), "UTC", actor.Id, now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb); var team = new Team(Guid.NewGuid(), ev.Id, "Preformed", "preformed-" + Guid.NewGuid().ToString("N"), TeamFormationType.Preformed, null, false); db.AddRange(actor, ev, team); await db.SaveChangesAsync(); return (actor, ev, team); }
    private sealed class TestClock(DateTimeOffset now) : TimeProvider { private DateTimeOffset value = now; public override DateTimeOffset GetUtcNow() => value; public void Advance(TimeSpan span) => value += span; }
    private static string FindRepositoryRoot() { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) return directory.FullName; throw new DirectoryNotFoundException("Repository root was not found."); }
}
