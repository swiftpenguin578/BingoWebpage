using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice5MigrationRejectionTests : IAsyncLifetime
{
    private const string PreviousMigration = "20260728145446_NormalizeRetainedLegacyDiscordSystemField";
    private const string Slice5FoundationMigration = "20260729132140_AddSlice5PersistenceFoundation";
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("bingo_slice5_migration_rejections").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;
    public async Task InitializeAsync() { await database.StartAsync(); options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options; }
    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Theory]
    [InlineData("membership", "cross-event team membership")]
    [InlineData("pick", "cross-event draft pick")]
    [InlineData("finalized", "ambiguous effective pick projection")]
    public async Task RetainedConversionRejectsInvalidProjectionWithoutPartialUpgrade(string kind, string expected)
    {
        await using var legacy = new ApplicationDbContext(options);
        await legacy.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        var ids = await SeedAsync(legacy, kind);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => legacy.GetService<IMigrator>().MigrateAsync());

        Assert.Contains(expected, exception.MessageText, StringComparison.OrdinalIgnoreCase);
        Assert.False(await ExistsAsync(legacy, "team_image_assets"));
        Assert.False(await ExistsAsync(legacy, "team_legacy_image_references"));
        Assert.Equal(2, await legacy.Database.SqlQuery<int>($"SELECT count(*) AS \"Value\" FROM teams WHERE id IN ({ids.TeamOne}, {ids.TeamTwo})").SingleAsync());
    }

    [Fact]
    public async Task RetainedPublicationBackfillUsesTheIdentityValidAtPublication()
    {
        await using var retained = new ApplicationDbContext(options);
        await retained.GetService<IMigrator>().MigrateAsync(Slice5FoundationMigration);
        var now = DateTimeOffset.UtcNow.AddDays(-1); var account = Account.CreateWebsite(Guid.NewGuid(), "publication-admin", "Publication admin", now); var ev = new BingoEvent(Guid.NewGuid(), "Retained publication", "retained-publication", "UTC", account.Id, now);
        var team = new Team(Guid.NewGuid(), ev.Id, "Retained team", "retained-team", TeamFormationType.Preformed, null, false, now);
        var draft = new DraftSession(Guid.NewGuid(), ev.Id, 1); draft.Start(now); draft.Finalize(now);
        var participant = new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated);
        var character = new OsrsCharacter(Guid.NewGuid(), "Publication name", "PUBLICATION NAME", now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), ev.Id, participant.Id, character.Id, 0, now, account.Id, null, EventCharacterRole.Playing, 1, EhbSource.AdminCorrection, null);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now, null, "retained");
        var cycle = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, now.AddMinutes(1), account.Id);
        retained.AddRange(account, ev, team, draft, participant, character, assignment, membership, cycle); await retained.SaveChangesAsync();
        await retained.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO draft_publication_rosters (id, draft_publication_cycle_id, team_id, event_participant_id, role, effective_pick_number) VALUES ({Guid.NewGuid()}, {cycle.Id}, {team.Id}, {participant.Id}, {"Participant"}, {null})");

        await retained.GetService<IMigrator>().MigrateAsync();
        Assert.Equal("Publication name", await retained.DraftPublicationRosters.Select(x => x.PublicCharacterName).SingleAsync());
    }

    [Fact]
    public async Task RetainedPublicationBackfillFailsClosedWhenIdentityIsMissing()
    {
        await using var retained = new ApplicationDbContext(options);
        await retained.GetService<IMigrator>().MigrateAsync(Slice5FoundationMigration);
        var now = DateTimeOffset.UtcNow.AddDays(-1); var account = Account.CreateWebsite(Guid.NewGuid(), "missing-admin", "Missing admin", now); var ev = new BingoEvent(Guid.NewGuid(), "Missing publication", "missing-publication", "UTC", account.Id, now);
        var team = new Team(Guid.NewGuid(), ev.Id, "Missing team", "missing-team", TeamFormationType.Preformed, null, false, now); var draft = new DraftSession(Guid.NewGuid(), ev.Id, 1); draft.Start(now); draft.Finalize(now); var participant = new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated); var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now, null, "missing"); var cycle = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, now, account.Id);
        retained.AddRange(account, ev, team, draft, participant, membership, cycle); await retained.SaveChangesAsync();
        await retained.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO draft_publication_rosters (id, draft_publication_cycle_id, team_id, event_participant_id, role, effective_pick_number) VALUES ({Guid.NewGuid()}, {cycle.Id}, {team.Id}, {participant.Id}, {"Participant"}, {null})");

        var exception = await Assert.ThrowsAsync<PostgresException>(() => retained.GetService<IMigrator>().MigrateAsync());
        Assert.Contains("Cannot backfill", exception.MessageText, StringComparison.Ordinal);
    }

    private static async Task<(Guid TeamOne, Guid TeamTwo)> SeedAsync(ApplicationDbContext db, string kind)
    {
        var account = Guid.NewGuid(); var eventOne = Guid.NewGuid(); var eventTwo = Guid.NewGuid(); var teamOne = Guid.NewGuid(); var teamTwo = Guid.NewGuid(); var participantOne = Guid.NewGuid(); var participantTwo = Guid.NewGuid(); var draft = Guid.NewGuid(); var now = DateTimeOffset.UtcNow.AddDays(-1);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO accounts (id, password_hash, must_change_password, account_type, active, authorization_version, login_name, normalized_login_name, global_role, public_username, normalized_public_username, password_version, version, created_at) VALUES ({account}, {"hash"}, FALSE, {"WebsiteAccount"}, TRUE, 1, {"admin"}, {"ADMIN"}, {"Admin"}, {"admin"}, {"ADMIN"}, 1, 1, {now});");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO events (id, name, slug, description, timezone, state, signup_opens_at, signup_closes_at, event_starts_at, event_ends_at, submission_cutoff_at, participant_cap, waiting_list_enabled, require_signup_code, participant_list_published, draft_results_published, team_rosters_published, board_published, results_published, draft_locked, created_by_account_id, created_at) VALUES ({eventOne}, {"One"}, {"one"}, {""}, {"UTC"}, {"Draft"}, {now}, {now}, {now}, {now.AddDays(1)}, {now.AddDays(1)}, 10, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, {account}, {now}), ({eventTwo}, {"Two"}, {"two"}, {""}, {"UTC"}, {"Draft"}, {now}, {now}, {now}, {now.AddDays(1)}, {now.AddDays(1)}, 10, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, {account}, {now});");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO event_participants (id, event_id, captain_volunteer, payment_received, signup_status, signup_sequence, signed_up_at, form_version, response_version, source) VALUES ({participantOne}, {eventOne}, FALSE, FALSE, {"Confirmed"}, 1, {now}, 1, 1, {"AdminCreated"}), ({participantTwo}, {eventTwo}, FALSE, FALSE, {"Confirmed"}, 1, {now}, 1, 1, {"AdminCreated"});");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO teams (id, event_id, name, slug, formation_type, included_in_draft, active) VALUES ({teamOne}, {eventOne}, {"One team"}, {"one-team"}, {"Drafted"}, TRUE, TRUE), ({teamTwo}, {eventTwo}, {"Two team"}, {"two-team"}, {"Drafted"}, TRUE, TRUE);");
        if (kind == "membership") await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO team_memberships (id, team_id, event_participant_id, role, joined_at) VALUES ({Guid.NewGuid()}, {teamOne}, {participantTwo}, {"Participant"}, {now});");
        else
        {
            var state = kind == "finalized" ? "Finalized" : "Running";
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO draft_sessions (id, event_id, target_team_size, state, locked_at, finalized_at, control_version) VALUES ({draft}, {eventOne}, 2, {state}, {now}, {(kind == "finalized" ? now : (DateTimeOffset?)null)}, 1);");
            var pickTeam = kind == "pick" ? teamTwo : teamOne;
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO draft_picks (id, draft_session_id, team_id, event_participant_id, pick_number, round_number, picked_at) VALUES ({Guid.NewGuid()}, {draft}, {pickTeam}, {participantOne}, 1, 1, {now});");
        }
        return (teamOne, teamTwo);
    }

    private static Task<bool> ExistsAsync(ApplicationDbContext db, string table) => db.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = {table}) AS \"Value\"").SingleAsync();
}
