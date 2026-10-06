using System.Security.Claims;
using Bingo.Application.Dashboard;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Dashboard;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Navigation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Testcontainers.PostgreSql;
using DirectoryPage = Bingo.Web.Pages.Admin.Events.IndexModel;

namespace Bingo.IntegrationTests;

public sealed class EventsDirectoryIntegrationTests(PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_directory").WithUsername("bingo").WithPassword("bingo_test_password"));
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task DirectoryOrdersAcrossPhasesWithNullLastAndStableTiesAndFiltersItsPopulation()
    {
        var admin = Admin();
        var live = Event(admin, EventState.Live, "Live", Now.AddDays(-5));
        var early = Event(admin, EventState.SignupClosed, "Early", Now.AddDays(1));
        var tieHigh = Event(admin, EventState.SignupOpen, "Alpha", Now.AddDays(2), Guid.Parse("ffffffff-0000-0000-0000-000000000001"));
        var tieLow = Event(admin, EventState.Draft, "Zulu", Now.AddDays(2).AddTicks(7), Guid.Parse("00000001-0000-0000-0000-000000000001"));
        var unscheduled = new BingoEvent(Guid.NewGuid(), "Unscheduled", "unscheduled", "UTC", admin.Id, Now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        var archived = Event(admin, EventState.Archived, "Newest archived", Now.AddDays(-4));
        var final = Event(admin, EventState.Finalized, "Finished", Now.AddDays(-6));
        var review = Event(admin, EventState.AwaitingFinalReview, "Review", Now.AddDays(-8));
        var cancelled = Event(admin, EventState.Cancelled, "Cancelled", Now.AddDays(-10));
        var hidden = Event(admin, EventState.Archived, "Hidden", Now.AddDays(-3));
        hidden.Hide(admin.Id, Now, hidden.Name, "Controlled fixture");
        var discarded = new BingoEvent(Guid.NewGuid(), "Discarded", "discarded", "UTC", admin.Id, Now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        discarded.Discard(admin.Id, Now, false);
        await using var db = new ApplicationDbContext(options);
        db.AddRange(admin, live, early, tieHigh, tieLow, unscheduled, archived, final, review, cancelled, hidden, discarded);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        // PostgreSQL truncates sub-microsecond precision: both ties are truly equal after roundtrip.
        Assert.Equal(tieHigh.EventStartsAt, (await db.Events.SingleAsync(x => x.Id == tieLow.Id)).EventStartsAt);
        var page = Page(db, admin);
        await page.OnGetAsync(default);
        Assert.Equal(new[] { live.Id, early.Id, tieLow.Id, tieHigh.Id, unscheduled.Id, archived.Id, final.Id, review.Id, cancelled.Id }, page.Events.Select(x => x.Id));
        Assert.Equal(9, page.PopulationCount);
        Assert.Null(page.Events.Single(x => x.Id == unscheduled.Id).ParticipantCap);
        Assert.All(page.Events, row => Assert.Equal(0, row.AttentionCategoryCount));
        Assert.All(page.Events, row => Assert.Equal("—", page.AttentionSummary(row)));
        page.View = "current";
        await page.OnGetAsync(default);
        Assert.Equal(5, page.PopulationCount);
        page.View = "past";
        page.Filter = "archived";
        await page.OnGetAsync(default);
        Assert.Equal(4, page.PopulationCount);
        Assert.Equal(archived.Id, Assert.Single(page.Events).Id);
        page.Search = "nothing matches";
        await page.OnGetAsync(default);
        Assert.Empty(page.Events);
        Assert.Equal(4, page.PopulationCount);
        page.View = "all"; page.Filter = null; page.Search = null; page.Sort = "identity";
        await page.OnGetAsync(default);
        Assert.Equal(page.Events.Select(x => x.Name).Order(StringComparer.Ordinal), page.Events.Select(x => x.Name));
        page.Sort = "dates"; page.Direction = "desc";
        await page.OnGetAsync(default);
        Assert.Equal(unscheduled.Id, page.Events[^1].Id);
        page.Sort = "signups";
        await page.OnGetAsync(default);
        Assert.Equal(0L, page.Events.Single(row => row.Id == cancelled.Id).ParticipantCount);
    }

    [Fact]
    public async Task RetainedParticipationReusesDashboardRulesAndHonestImportAndMissingCoverage()
    {
        var admin = Admin();
        var member = Account.CreateWebsite(Guid.NewGuid(), "Former player", "FORMERPLAYER", Now.AddDays(-30));
        member.Disable(Now, admin.Id, "Controlled fixture");
        var live = Event(admin, EventState.Live, "Retained", Now.AddDays(-5));
        var imported = Event(admin, EventState.Archived, "Imported", Now.AddDays(-12));
        var broken = Event(admin, EventState.AwaitingFinalReview, "Missing interval", Now.AddDays(-9));
        await using var db = new ApplicationDbContext(options);
        db.AddRange(admin, member, live, imported, broken);
        AddPerson(db, live, member, SignupStatus.Withdrawn, live.ActualStartedAt!.Value, Now.AddDays(-1), move: true);
        AddPerson(db, live, null, SignupStatus.Withdrawn, live.ActualStartedAt.Value.AddDays(-1), live.ActualStartedAt.Value);
        AddPerson(db, live, null, SignupStatus.Confirmed, Now.AddDays(1), null);
        AddPerson(db, imported, null, SignupStatus.Withdrawn, imported.ActualStartedAt!.Value, imported.ActualEndedAt);
        db.Add(new AuditEntry(Guid.NewGuid(), Now, admin.Id, admin.LoginName, "historical_import.applied", "Event", imported.Id.ToString(), "{}", imported.Id));
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET actual_started_at = NULL WHERE id = {broken.Id}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET participant_cap = NULL WHERE id = {imported.Id}");
        db.ChangeTracker.Clear();
        var service = new AdminDashboardService(db, new FixedClock());
        var dashboard = await service.GetAsync(admin.Id);
        var counts = await service.GetEventParticipationAsync(admin.Id, [live.Id, imported.Id, broken.Id]);
        foreach (var row in dashboard.History)
            Assert.Equal(row.Participants.Availability, counts[row.EventId].Participants.Availability);
        Assert.Equal(1L, counts[live.Id].Participants.Value);
        Assert.Equal(dashboard.History.Single(x => x.EventId == live.Id).Participants.Value, counts[live.Id].Participants.Value);
        Assert.Equal(1L, counts[imported.Id].Participants.Value);
        Assert.True(counts[imported.Id].IsHistoricalImport);
        Assert.False(counts[broken.Id].Participants.IsAvailable);
        var page = Page(db, admin);
        await page.OnGetAsync(default);
        var retained = page.Events.Single(x => x.Id == live.Id);
        Assert.Equal(1L, retained.ParticipantCount);
        Assert.Equal(1, retained.Confirmed); // Future member: deliberately different from retained people.
        var importRow = page.Events.Single(x => x.Id == imported.Id);
        Assert.Equal(0, importRow.Confirmed);
        Assert.Equal(1L, importRow.ParticipantCount);
        Assert.Null(importRow.ParticipantCap);
        Assert.True(importRow.Participation!.IsHistoricalImport);
        Assert.Null(page.Events.Single(x => x.Id == broken.Id).ParticipantCount);
        page.Sort = "signups";
        foreach (var direction in new[] { "asc", "desc" })
        {
            page.Direction = direction;
            await page.OnGetAsync(default);
            Assert.Equal(broken.Id, page.Events[^1].Id); // Unavailable is null-last, not zero.
            Assert.Null(page.Events[^1].ParticipantCount);
        }
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task HiddenPopulationRequiresCurrentSuperAdminAndLostAuthorityFailsClosed()
    {
        var admin = Admin();
        var hidden = Event(admin, EventState.Archived, "Quarantined", Now.AddDays(-10));
        hidden.Hide(admin.Id, Now, hidden.Name, "Controlled fixture");
        var visible = Event(admin, EventState.Live, "Visible", Now.AddDays(-3));
        await using var db = new ApplicationDbContext(options);
        db.AddRange(admin, hidden, visible);
        await db.SaveChangesAsync();
        var page = Page(db, admin, claimedRole: "SuperAdmin");
        page.View = "hidden";
        await page.OnGetAsync(default);
        Assert.Equal("all", page.ActiveView);
        Assert.Equal(visible.Id, Assert.Single(page.Events).Id);
        var service = new AdminDashboardService(db, new FixedClock());
        Assert.Empty(await service.GetEventParticipationAsync(admin.Id, [hidden.Id]));
        admin.SetGlobalRole(GlobalRole.SuperAdmin);
        await db.SaveChangesAsync();
        await page.OnGetAsync(default);
        Assert.Equal(hidden.Id, Assert.Single(page.Events).Id);
        Assert.Equal(1, page.PopulationCount);
        Assert.Single(await service.GetEventParticipationAsync(admin.Id, [hidden.Id]));
        page.View = "all";
        await page.OnGetAsync(default);
        Assert.Equal(visible.Id, Assert.Single(page.Events).Id);
        admin.SetGlobalRole(GlobalRole.User);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => page.OnGetAsync(default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetEventParticipationAsync(admin.Id, [visible.Id]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetEventParticipationAsync(Guid.Empty, []));
        admin.SetGlobalRole(GlobalRole.Admin);
        admin.Disable(Now, admin.Id, "Controlled fixture");
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetEventParticipationAsync(admin.Id, [visible.Id]));
    }

    [Fact]
    public async Task AttentionPrioritizesFailuresAndCountsReviewOnceWithoutChangingInboxUnits()
    {
        var admin = Admin();
        var item = Event(admin, EventState.Draft, "Failure plus review", Now.AddHours(-1));
        var quiet = new BingoEvent(Guid.NewGuid(), "Incomplete setup", "incomplete-setup", "UTC", admin.Id, Now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        var hidden = Event(admin, EventState.Archived, "Hidden attention fixture", Now.AddDays(-10));
        hidden.Hide(admin.Id, Now, hidden.Name, "Controlled fixture");
        await using var db = new ApplicationDbContext(options);
        db.Add(hidden);
        db.AddRange(admin, item, quiet,
            new ScheduledSignupOpeningAttempt(Guid.NewGuid(), item.Id, item.SignupOpensAt!.Value, Now.AddMinutes(-4), false, ["DESCRIPTION_REQUIRED"], ["Description missing"]),
            new ScheduledEventStartAttempt(Guid.NewGuid(), item.Id, item.EventStartsAt!.Value, Now.AddMinutes(-3), false, ["BOARD_NOT_PUBLISHED"]));
        var team = new Team(Guid.NewGuid(), item.Id, "Team", "team", TeamFormationType.Preformed, null, false, Now);
        var board = new Board(Guid.NewGuid(), item.Id, "Board", 1, 1);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Tile", "", "", 1m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 1, false, false, "Requirement", true);
        var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, Now, SignupSource.AdminCreated);
        participant.AssignOwner(admin);
        var character = new OsrsCharacter(Guid.NewGuid(), "Synthetic player", "SYNTHETIC PLAYER", Now);
        db.AddRange(team, board, tile, requirement, participant, character);
        for (var n = 0; n < 3; n++)
            db.Add(new Submission(Guid.NewGuid(), item.Id, team.Id, tile.Id, requirement.Id, null, participant.Id, character.Id, character.DisplayName, admin.Id, 1, Now.AddMinutes(-n), null, null));
        await db.SaveChangesAsync();
        var page = Page(db, admin);
        page.Sort = "attention"; page.Direction = "desc";
        await page.OnGetAsync(default);
        var row = page.Events[0];
        Assert.Equal(item.Id, row.Id);
        Assert.Equal(5, row.ActionCount);
        Assert.Equal(3, row.AttentionCategoryCount);
        Assert.Equal("Start postponed +2", page.AttentionSummary(row));
        Assert.Equal("—", page.AttentionSummary(page.Events[1]));
        Assert.Equal(5, (await Shell(db).GetAdminActionsAsync(default)).Count);
        item.ConfigureSchedule(Now.AddDays(1), Now.AddDays(2), null, Now.AddDays(3), Now.AddDays(4), 10);
        await db.SaveChangesAsync();
        await page.OnGetAsync(default);
        row = page.Events.Single(x => x.Id == item.Id);
        Assert.Equal(1, row.AttentionCategoryCount);
        Assert.Equal("3 to review", page.AttentionSummary(row));
        Assert.Equal(3, (await Shell(db).GetAdminActionsAsync(default)).Count);

        page.Attention = "1";
        page.View = "current";
        page.Filter = "draft";
        page.Search = "Failure";
        await page.OnGetAsync(default);
        Assert.True(page.ActiveAttention);
        Assert.Equal(item.Id, Assert.Single(page.Events).Id);
        Assert.Equal(2, page.PopulationCount); // Attention/search/phase do not replace population size.
        page.Search = "Incomplete";
        await page.OnGetAsync(default);
        Assert.Empty(page.Events); // Ordinary setup is never an attention category.
        Assert.Equal(2, page.PopulationCount);
        page.Search = null;
        page.Filter = "live";
        await page.OnGetAsync(default);
        Assert.Empty(page.Events);
        page.Filter = null;
        page.View = "past";
        await page.OnGetAsync(default);
        Assert.Empty(page.Events);
        Assert.Equal(0, page.PopulationCount); // Hidden past event remains excluded.
        page.View = "hidden";
        await page.OnGetAsync(default);
        Assert.Equal("all", page.ActiveView); // Attention cannot grant hidden access.
        Assert.Equal(item.Id, Assert.Single(page.Events).Id);
        Assert.Equal(2, page.PopulationCount);
        admin.SetGlobalRole(GlobalRole.SuperAdmin);
        await db.SaveChangesAsync();
        await page.OnGetAsync(default);
        Assert.Equal("hidden", page.ActiveView);
        Assert.Empty(page.Events); // Shared action projection excludes quarantined issues.
        Assert.Equal(1, page.PopulationCount);
        page.Attention = "0";
        await page.OnGetAsync(default);
        Assert.False(page.ActiveAttention);
        Assert.Equal(hidden.Id, Assert.Single(page.Events).Id);
        page.View = "all";
        page.Attention = null;
        await page.OnGetAsync(default);
        Assert.False(page.ActiveAttention);
        Assert.Equal(2, page.Events.Count);
    }

    private static Account Admin()
    {
        var account = Account.CreateWebsite(Guid.NewGuid(), "Directory admin", "DIRECTORYADMIN", Now.AddDays(-30));
        account.SetGlobalRole(GlobalRole.Admin);
        return account;
    }

    private static BingoEvent Event(Account admin, EventState state, string name, DateTimeOffset start, Guid? id = null)
    {
        var item = new BingoEvent(id ?? Guid.NewGuid(), name, $"directory-{Guid.NewGuid():N}", "UTC", admin.Id, Now.AddDays(-30), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        item.ConfigureSchedule(start.AddDays(-3), start.AddDays(-2), null, start, start.AddDays(1), 10);
        item.ConfigureSignup(true, false, null);
        if (state == EventState.Cancelled) { item.Cancel(admin.Id, start.AddDays(1), "Controlled fixture", true); return item; }
        if (state == EventState.Draft) return item;
        item.OpenSignups(start.AddDays(-3));
        if (state == EventState.SignupOpen) return item;
        item.CloseSignups(start.AddDays(-2));
        if (state == EventState.SignupClosed) return item;
        item.StartEvent(start);
        if (state == EventState.Live) return item;
        item.EndEvent(start.AddDays(1));
        if (state == EventState.AwaitingFinalReview) return item;
        item.FinalizeResults(start.AddDays(2));
        if (state == EventState.Archived) item.Archive(start.AddDays(3));
        return item;
    }

    private static void AddPerson(ApplicationDbContext db, BingoEvent item, Account? owner, SignupStatus status, DateTimeOffset join, DateTimeOffset? leave, bool move = false)
    {
        var participant = new EventParticipant(Guid.NewGuid(), item.Id, status, Math.Abs((long)Guid.NewGuid().GetHashCode()) + 1, Now.AddDays(-25), SignupSource.AdminCreated);
        if (owner is not null) participant.AssignOwner(owner);
        var team = new Team(Guid.NewGuid(), item.Id, $"Retained team {participant.Id:N}", $"team-{Guid.NewGuid():N}", TeamFormationType.Preformed, null, false, join);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, join, null, "Controlled fixture");
        if (leave is { } end) membership.Leave(end, "Controlled fixture");
        db.AddRange(participant, team, membership);
        if (move)
            db.Add(new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, leave!.Value, null, "Controlled move"));
    }

    private static SharedShellService Shell(ApplicationDbContext db) => new(db, new Text(), null!, null!, null!, new FixedClock());
    private static DirectoryPage Page(ApplicationDbContext db, Account account, string claimedRole = "Admin") => new(db, null!, new FixedClock(), new Text(), Shell(db), new AdminDashboardService(db, new FixedClock()))
    {
        PageContext = new PageContext(new ActionContext(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()), new Claim(ClaimTypes.Role, claimedRole)], "test"))
        }, new RouteData(), new PageActionDescriptor()))
    };
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Text : IStringLocalizer<Bingo.Web.SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
