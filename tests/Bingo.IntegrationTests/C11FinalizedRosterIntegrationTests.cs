using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Application.Events;
using Bingo.Application.Evidence;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class C11FinalizedRosterIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_c11").WithUsername("bingo").WithPassword("c11_fixture_only").Build();
    private readonly MutableClock clock = new(new DateTimeOffset(2026, 9, 14, 12, 34, 27, TimeSpan.Zero));
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = Db();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();
    private ApplicationDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(interceptors).Options);
    private SignupService Service(ApplicationDbContext db, IEventCompetitionManagementService? competitionManagement = null) => new(db, new SecretHasher(), clock, accountValidation: new SuccessfulWiseOldManAccountValidation(), competitionManagement: competitionManagement);

    [Fact]
    public async Task FinalizedRosterWithdrawalPreservesPublicationPicksNotesReservationsAndNotificationAccess()
    {
        var seed = await SeedAsync();
        await using var factory = Factory();
        using var admin = await LoginAsync(factory, "c11-admin");
        using var owner = await LoginAsync(factory, "c11-departed");
        using var publicClient = factory.CreateClient();
        var rosterBefore = await RowsAsync("draft_publication_rosters");
        var picksBefore = await RowsAsync("draft_picks");
        var assignmentsBefore = await RowsAsync("event_participant_characters");
        var answersBefore = await RowsAsync("signup_answers", $"event_participant_id = '{seed.DepartedId}'");
        var route = ParticipantPath(seed, seed.DepartedId);
        Assert.Contains(route, await admin.GetStringAsync($"/Admin/Events/Participants/{seed.EventId}"));
        Assert.Contains(route, await admin.GetStringAsync($"/Admin/Events/Draft/{seed.EventId}"));
        var page = await admin.GetStringAsync(route);
        Assert.Contains("A note is appended", page);
        using (var response = await PostAsync(admin, route, "Withdraw", page, new()
        {
            ["ConfirmLifecycleAction"] = "true",
            ["ExpectedMembershipVersion"] = Input(page, "ExpectedMembershipVersion"),
            ["PrivateWithdrawalNote"] = "  C11 private departure detail  "
        })) Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var vacancy = await admin.GetStringAsync(route);
        Assert.Contains("Departed C", vacancy);
        Assert.DoesNotContain("Fill open vacancy", vacancy);
        Assert.Contains("Existing private note", vacancy);
        Assert.DoesNotContain("C11 private departure detail", vacancy);
        var search = await admin.GetStringAsync($"/Admin/Events/Participants/{seed.EventId}?ParticipantStatus=Confirmed&ParticipantSearch=Departed%20C");
        Assert.Contains($"participant-row-{seed.DepartedId}", search);
        Assert.Contains(route, search);
        Assert.Contains("Departed C", search);
        var nonmatching = await admin.GetStringAsync($"/Admin/Events/Participants/{seed.EventId}?ParticipantStatus=Confirmed&ParticipantSearch=No%20such%20participant");
        Assert.DoesNotContain($"participant-row-{seed.DepartedId}", nonmatching);
        Assert.Contains("Departed C", search);
        Assert.Contains("Departed C", await admin.GetStringAsync($"/Admin/Events/Draft/{seed.EventId}"));
        Assert.Equal(assignmentsBefore, await RowsAsync("event_participant_characters"));
        Assert.Equal(picksBefore, await RowsAsync("draft_picks"));
        Assert.Equal(answersBefore, await RowsAsync("signup_answers", $"event_participant_id = '{seed.DepartedId}'"));
        await AssertOldRosterAsync(seed, rosterBefore);
        var published = await publicClient.GetStringAsync(TeamsPath(seed));
        Assert.DoesNotContain("Departed C", RosterSection(published));
        Assert.Contains("Departed C", PickSection(published));
        Assert.DoesNotContain("private departure detail", published);
        Assert.DoesNotContain("Existing private note", published);

        await using (var db = Db())
        {
            Assert.Null((await db.EventParticipants.SingleAsync(x => x.Id == seed.DepartedId)).WithdrawnAt);
            Assert.Equal(SignupStatus.Confirmed, (await db.EventParticipants.SingleAsync(x => x.Id == seed.DepartedId)).SignupStatus);
            Assert.Equal(clock.Now, (await db.TeamMemberships.SingleAsync(x => x.Id == seed.DepartedMembershipId)).LeftAt);
            Assert.Equal(SignupStatus.WaitingList, (await db.EventParticipants.SingleAsync(x => x.Id == seed.WaitingId)).SignupStatus);
            Assert.Empty(await db.EventParticipantCharacterSwaps.ToListAsync());
            Assert.Empty(await db.PersonalNotifications.ToListAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => new EvidenceAuthority(db).ResolveActorAsync(seed.DepartedOwnerId, seed.EventId, seed.TeamId, clock.Now));
        }
        using (var denied = await owner.GetAsync(route)) Assert.NotEqual(HttpStatusCode.OK, denied.StatusCode);
        using (var denied = await owner.GetAsync(TeamsPath(seed) + $"?participantId={seed.DepartedId}"))
        {
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.Contains("/Account/AccessDenied", denied.Headers.Location!.OriginalString);
        }
        var draftPath = $"/Admin/Events/Draft/{seed.EventId}";
        var draftPage = await admin.GetStringAsync(draftPath);
        long teamVersion;
        await using (var db = Db()) teamVersion = await db.Teams.Where(x => x.Id == seed.TeamId).Select(x => x.Version).SingleAsync();
        using (var response = await PostAsync(admin, draftPath, "AddMember", draftPage, new()
        {
            ["teamId"] = seed.TeamId.ToString(),
            ["participantId"] = seed.WaitingId.ToString(),
            ["confirmed"] = "true",
            ["expectedTeamVersion"] = teamVersion.ToString(CultureInfo.InvariantCulture),
            ["rosterTeamId"] = seed.TeamId.ToString()
        })) Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        published = await publicClient.GetStringAsync(TeamsPath(seed));
        Assert.Contains("Waiting C", RosterSection(published));
        Assert.DoesNotContain("Waiting C", PickSection(published));
        Assert.Contains("Departed C", PickSection(published));
        await AssertOldRosterAsync(seed, rosterBefore);
        Assert.Equal(picksBefore, await RowsAsync("draft_picks"));
        Assert.Equal(assignmentsBefore, await RowsAsync("event_participant_characters"));
        await using (var db = Db())
        {
            var member = await db.TeamMemberships.SingleAsync(x => x.EventParticipantId == seed.WaitingId && x.LeftAt == null);
            Assert.Null(member.ReplacesMembershipId);
            Assert.Null(member.AssignedByDraftPickId);
            Assert.Equal(TeamMembershipRole.Participant, member.Role);
            Assert.Equal(clock.Now, member.JoinedAt);
            Assert.Empty(await db.EventParticipantCharacterSwaps.ToListAsync());
            Assert.Equal(SignupStatus.Confirmed, (await db.EventParticipants.SingleAsync(x => x.Id == seed.WaitingId)).SignupStatus);
            Assert.Empty(await db.WaitingListPromotionFollowUps.ToListAsync());
            Assert.Empty(await db.PersonalNotifications.ToListAsync());
        }
        await using var final = Db();
        Assert.Single(await final.DraftPublicationCycles.Where(x => x.SupersededAt == null).ToListAsync());
        Assert.Equal(3, await final.DraftPublicationCycles.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpCaptainRecoveryRepublishesRolesAndStartsWithActualPrimaryEligibility(bool fillVacancy)
    {
        var seed = await SeedAsync();
        await PublishBoardAsync(seed);
        await using var factory = Factory();
        using var admin = await LoginAsync(factory, "c11-admin");
        await WithdrawAsync(seed);
        if (!fillVacancy)
        {
            var rosterBeforeStart = await RowsAsync("draft_publication_rosters");
            var picksBeforeStart = await RowsAsync("draft_picks");
            var notificationsBeforeStart = await RowsAsync("personal_notifications");
            var startManagePath = $"/Admin/Events/Manage/{seed.EventId}";
            var startManage = await admin.GetStringAsync(startManagePath);
            using (var started = await PostAsync(admin, startManagePath, "StartEvent", startManage, new()
            {
                ["EventVersion"] = Input(startManage, "EventVersion"),
                ["ConfirmStartEvent"] = "true",
                ["StartReason"] = "Controlled early start"
            })) Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);

            await using var verifyStart = Db();
            var startedEvent = await verifyStart.Events.SingleAsync(x => x.Id == seed.EventId);
            Assert.Equal(EventState.Live, startedEvent.State);
            Assert.Equal(clock.Now, startedEvent.ActualStartedAt);
            Assert.Equal(rosterBeforeStart, await RowsAsync("draft_publication_rosters"));
            Assert.Equal(picksBeforeStart, await RowsAsync("draft_picks"));
            Assert.Equal(notificationsBeforeStart, await RowsAsync("personal_notifications"));
            Assert.Empty(await verifyStart.EventParticipantCharacterSwaps.Where(x => x.EventParticipantId == seed.DepartedId).ToListAsync());
            var expectedLeader = await verifyStart.PrimaryCharacters().SingleAsync(x => x.ParticipantId == seed.LeaderId);
            var leaderActivation = await verifyStart.EventParticipantCharacterSwaps
                .SingleAsync(x => x.EventParticipantId == seed.LeaderId && x.PreviousOsrsCharacterId == null);
            Assert.Equal(expectedLeader.OsrsCharacterId, leaderActivation.NextOsrsCharacterId);
            Assert.Equal(clock.Now, leaderActivation.EffectiveAtUtc);
            Assert.Equal(clock.Now, leaderActivation.RecordedAtUtc);
            Assert.Single(await verifyStart.AuditEntries.Where(x => x.EventId == seed.EventId && x.Action == "event.started").ToListAsync());
            return;
        }
        if (fillVacancy)
        {
            var added = await AddFinalizedAsync(seed, seed.TeamId, participantId: seed.WaitingId);
            Assert.True(added.Succeeded, added.Error);
        }
        var beforeRole = await RowsAsync("draft_publication_rosters");
        var currentBefore = await CurrentCycleAsync();
        var managePath = $"/Admin/Events/Manage/{seed.EventId}";
        var manage = await admin.GetStringAsync(managePath);
        using (var blocked = await PostAsync(admin, managePath, "StartEvent", manage, new()
        {
            ["EventVersion"] = Input(manage, "EventVersion"),
            ["ConfirmStartEvent"] = "true",
            ["StartReason"] = "Controlled early start"
        })) Assert.True(blocked.IsSuccessStatusCode || blocked.StatusCode == HttpStatusCode.Redirect);
        if (fillVacancy)
        {
            await using var validStart = Db();
            var started = await validStart.Events.SingleAsync(x => x.Id == seed.EventId);
            Assert.Equal(EventState.Live, started.State);
            Assert.Equal(clock.Now, started.ActualStartedAt);
            Assert.Equal(beforeRole, await RowsAsync("draft_publication_rosters"));
            var waitingActivation = await validStart.EventParticipantCharacterSwaps
                .SingleAsync(x => x.EventParticipantId == seed.WaitingId && x.PreviousOsrsCharacterId == null);
            var waitingPrimary = await validStart.PrimaryCharacters().SingleAsync(x => x.ParticipantId == seed.WaitingId);
            Assert.Equal(waitingPrimary.OsrsCharacterId, waitingActivation.NextOsrsCharacterId);
            Assert.Empty(await validStart.EventParticipantCharacterSwaps.Where(x => x.EventParticipantId == seed.DepartedId).ToListAsync());
            return;
        }
        await using (var db = Db()) Assert.Null((await db.Events.SingleAsync(x => x.Id == seed.EventId)).ActualStartedAt);
        var targetId = fillVacancy ? seed.WaitingId : seed.LeaderId;
        var targetOwner = fillVacancy ? seed.WaitingOwnerId : seed.LeaderOwnerId;
        await AssertPreStartSubmissionDeniedAsync(seed, targetId, targetOwner);
        var draftPath = $"/Admin/Events/Draft/{seed.EventId}";
        var draftPage = await admin.GetStringAsync(draftPath);
        Guid membershipId; long version;
        await using (var db = Db())
        {
            var member = await db.TeamMemberships.SingleAsync(x => x.EventParticipantId == targetId && x.LeftAt == null);
            membershipId = member.Id; version = member.Version;
        }
        using (var changed = await PostAsync(admin, draftPath, "ChangeRole", draftPage, new()
        {
            ["membershipId"] = membershipId.ToString(),
            ["membershipVersion"] = version.ToString(CultureInfo.InvariantCulture),
            ["role"] = "Captain",
            ["rosterTeamId"] = seed.TeamId.ToString()
        })) Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        await using (var db = Db())
        {
            var newCycle = await db.DraftPublicationCycles.SingleAsync(x => x.SupersededAt == null);
            Assert.NotEqual(currentBefore, newCycle.Id);
            Assert.Equal(TeamMembershipRole.Captain, (await db.DraftPublicationRosters.SingleAsync(x => x.DraftPublicationCycleId == newCycle.Id && x.EventParticipantId == targetId)).Role);
            Assert.NotEqual(TeamMembershipRole.Captain, (await db.DraftPublicationRosters.SingleAsync(x => x.DraftPublicationCycleId == currentBefore && x.EventParticipantId == targetId)).Role);
            var notice = Assert.Single(await db.PersonalNotifications.Where(x => x.RecipientAccountId == targetOwner && x.Title == "Team role updated").ToListAsync());
            using var captain = await LoginAsync(factory, fillVacancy ? "c11-waiting" : "c11-leader");
            var rendered = await FollowNoticeAsync(captain, notice.Id, TeamsPath(seed));
            Assert.Matches("(?s)<li class=\"is-captain\">\\s*<span>" + (fillVacancy ? "Waiting C" : "Leader") + "</span>", rendered);
        }
        await AssertPreStartSubmissionDeniedAsync(seed, targetId, targetOwner);
        manage = await admin.GetStringAsync(managePath);
        using (var started = await PostAsync(admin, managePath, "StartEvent", manage, new()
        {
            ["EventVersion"] = Input(manage, "EventVersion"),
            ["ConfirmStartEvent"] = "true",
            ["StartReason"] = "Controlled early start"
        })) Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);
        await using var verify = Db();
        Assert.Equal(clock.Now, (await verify.Events.SingleAsync(x => x.Id == seed.EventId)).ActualStartedAt);
        Assert.Empty(await verify.EventParticipantCharacterSwaps.Where(x => x.EventParticipantId == seed.DepartedId).ToListAsync());
        if (fillVacancy)
        {
            var active = await verify.EventParticipantCharacterSwaps.SingleAsync(x => x.EventParticipantId == seed.WaitingId);
            Assert.Equal(seed.WaitingPrimaryId, active.NextOsrsCharacterId);
            Assert.Equal(clock.Now, active.EffectiveAtUtc);
            Assert.Equal(seed.WaitingPrimaryId, (await new EvidenceAuthority(verify).ResolveCreditedCharacterAsync(seed.EventId, seed.WaitingId, clock.Now)).OsrsCharacterId);
        }
        var retained = await RowsAsync("draft_publication_rosters", $"draft_publication_cycle_id IN (SELECT id FROM draft_publication_cycles WHERE cycle_number <= {(fillVacancy ? 3 : 2)})");
        Assert.Equal(beforeRole, retained);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpRoleChangeRepublishesCurrentPublicRoleDuringLiveAndFinalReview(bool finalReview)
    {
        var seed = await SeedAsync();
        await StartLiveAsync(seed.EventId);
        if (finalReview) await EndLiveForReviewAsync(seed.EventId);

        Guid membershipId;
        long membershipVersion;
        Guid teamId;
        Guid participantId;
        TeamMembershipSource source;
        Guid? pickId;
        DateTimeOffset joinedAt;
        int publicationCountBefore;
        int competitionManagementBefore;
        int competitionSynchronizationBefore;
        int competitionOperationBefore;
        int competitionUpdateBefore;
        await using (var before = Db())
        {
            var membership = await before.TeamMemberships.SingleAsync(x => x.EventParticipantId == seed.LeaderId && x.LeftAt == null);
            membershipId = membership.Id;
            membershipVersion = membership.Version;
            teamId = membership.TeamId;
            participantId = membership.EventParticipantId;
            source = membership.Source;
            pickId = membership.AssignedByDraftPickId;
            joinedAt = membership.JoinedAt;
            publicationCountBefore = await before.DraftPublicationCycles.CountAsync(x => x.DraftSessionId == before.DraftSessions.Where(d => d.EventId == seed.EventId).Select(d => d.Id).Single());
            competitionManagementBefore = await before.EventCompetitionManagements.CountAsync(x => x.EventId == seed.EventId);
            competitionSynchronizationBefore = await before.EventCompetitionSynchronizations.CountAsync(x => x.EventId == seed.EventId);
            competitionOperationBefore = await before.EventCompetitionManagementOperations.CountAsync(x => x.EventId == seed.EventId);
            competitionUpdateBefore = await before.EventCompetitionUpdateAllSlots.CountAsync(x => x.EventId == seed.EventId);
        }

        await using var factory = Factory();
        using var admin = await LoginAsync(factory, "c11-admin");
        using var publicClient = factory.CreateClient();
        var path = $"/Admin/Events/Draft/{seed.EventId}";
        var page = await admin.GetStringAsync(path);
        using (var changed = await PostAsync(admin, path, "ChangeRole", page, new()
        {
            ["membershipId"] = membershipId.ToString(),
            ["membershipVersion"] = membershipVersion.ToString(CultureInfo.InvariantCulture),
            ["role"] = "Captain",
            ["rosterTeamId"] = teamId.ToString()
        })) Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);

        var publicPage = await publicClient.GetStringAsync(TeamsPath(seed));
        Assert.Matches("(?s)<li class=\"is-captain\">\\s*<span>Leader</span>", RosterSection(publicPage));

        await using var verify = Db();
        Assert.Equal(finalReview ? EventState.AwaitingFinalReview : EventState.Live,
            await verify.Events.Where(x => x.Id == seed.EventId).Select(x => x.State).SingleAsync());
        var memberAfter = await verify.TeamMemberships.SingleAsync(x => x.Id == membershipId);
        Assert.Equal(TeamMembershipRole.Captain, memberAfter.Role);
        Assert.Equal(teamId, memberAfter.TeamId);
        Assert.Equal(participantId, memberAfter.EventParticipantId);
        Assert.Equal(source, memberAfter.Source);
        Assert.Equal(pickId, memberAfter.AssignedByDraftPickId);
        Assert.Equal(joinedAt, memberAfter.JoinedAt);
        Assert.Null(memberAfter.LeftAt);
        // ChangeRole advances the domain version, then the persistence hook
        // advances the concurrency version for the single modified row.
        Assert.Equal(membershipVersion + 2, memberAfter.Version);
        Assert.Equal(1, await verify.TeamMembershipRoleTransitions.CountAsync(x => x.TeamMembershipId == membershipId));

        var draftId = await verify.DraftSessions.Where(x => x.EventId == seed.EventId).Select(x => x.Id).SingleAsync();
        var cycles = await verify.DraftPublicationCycles.Where(x => x.DraftSessionId == draftId).OrderBy(x => x.CycleNumber).ToListAsync();
        Assert.Equal(publicationCountBefore + 1, cycles.Count);
        var oldEntry = await verify.DraftPublicationRosters.SingleAsync(x => x.DraftPublicationCycleId == seed.InitialCycleId && x.EventParticipantId == seed.LeaderId);
        Assert.Equal(TeamMembershipRole.CoCaptain, oldEntry.Role);
        var activeCycle = Assert.Single(cycles, x => x.SupersededAt == null);
        var currentEntry = await verify.DraftPublicationRosters.SingleAsync(x => x.DraftPublicationCycleId == activeCycle.Id && x.EventParticipantId == seed.LeaderId);
        Assert.Equal(TeamMembershipRole.Captain, currentEntry.Role);
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == seed.EventId && x.Action == "team.role_roster_published").ToListAsync());

        Assert.Equal(competitionManagementBefore, await verify.EventCompetitionManagements.CountAsync(x => x.EventId == seed.EventId));
        Assert.Equal(competitionSynchronizationBefore, await verify.EventCompetitionSynchronizations.CountAsync(x => x.EventId == seed.EventId));
        Assert.Equal(competitionOperationBefore, await verify.EventCompetitionManagementOperations.CountAsync(x => x.EventId == seed.EventId));
        Assert.Equal(competitionUpdateBefore, await verify.EventCompetitionUpdateAllSlots.CountAsync(x => x.EventId == seed.EventId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpInternalReplacementValidatesReservationsAndCreatesNoPickOrPreStartActivation(bool linked)
    {
        var seed = await SeedAsync();
        await WithdrawAsync(seed);
        await using var factory = Factory();
        using var admin = await LoginAsync(factory, "c11-admin");
        var path = ParticipantPath(seed, seed.DepartedId);
        Assert.DoesNotContain("Fill open vacancy", await admin.GetStringAsync(path));
        var before = await StateHashAsync();
        await using (var missingVersion = Db())
        {
            var rejected = await Service(missingVersion).AddFinalizedRosterParticipantAsync(new(seed.EventId, seed.TeamId, seed.AdminId, "c11-admin", linked ? seed.InternalOwnerId : null, linked ? null : seed.WaitingId));
            Assert.False(rejected.Succeeded);
            Assert.Contains("version is required", rejected.Error, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(before, await StateHashAsync());
        Assert.False((await FillAsync(seed, seed.WaitingId)).Succeeded);
        Assert.Equal(before, await StateHashAsync());

        if (linked)
        {
            await using var db = Db();
            var owner = await db.Accounts.SingleAsync(x => x.Id == seed.InternalOwnerId);
            var character = new OsrsCharacter(Guid.NewGuid(), "Internal replacement", "INTERNAL REPLACEMENT", clock.Now);
            db.AddRange(character, new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, character.Id, owner.Id, true, 0, null, 42m, clock.Now));
            await db.SaveChangesAsync();
        }

        var added = linked
            ? await AddFinalizedAsync(seed, seed.TeamId, websiteAccountId: seed.InternalOwnerId)
            : await AddFinalizedAsync(seed, seed.TeamId, participantId: seed.WaitingId);
        Assert.True(added.Succeeded, added.Error);
        await using var verify = Db();
        var member = await verify.TeamMemberships.SingleAsync(x => x.Id == added.MembershipId);
        var participant = await verify.EventParticipants.SingleAsync(x => x.Id == added.ParticipantId);
        Assert.Equal(linked ? SignupSource.AdminCreated : SignupSource.Website, participant.Source);
        Assert.Equal(linked ? seed.InternalOwnerId : seed.WaitingOwnerId, participant.AccountId);
        Assert.Null(member.AssignedByDraftPickId);
        Assert.Equal(TeamMembershipRole.Participant, member.Role);
        Assert.Empty(await verify.EventParticipantCharacterSwaps.ToListAsync());
        Assert.Equal(3, await verify.DraftPublicationCycles.CountAsync());
        var publicPage = await admin.GetStringAsync(TeamsPath(seed));
        Assert.Contains(linked ? "Internal replacement" : "Waiting C", RosterSection(publicPage));
        Assert.DoesNotContain("internal private answer", publicPage);
        Assert.Equal(2, await verify.DraftPicks.CountAsync());
    }

    [Fact]
    public async Task InternalReplacementOutageConfirmationRetainsTheOriginalForm()
    {
        var seed = await SeedAsync();
        await WithdrawAsync(seed);
        await using var factory = Factory(unavailablePlayerLookup: true);
        using var admin = await LoginAsync(factory, "c11-admin");
        var path = ParticipantPath(seed, seed.DepartedId);
        var page = await admin.GetStringAsync(path);
        var unchanged = await StateHashAsync();
        Assert.DoesNotContain("Fill open vacancy", page);
        using (var response = await PostAsync(admin, path, "FillVacancy", page, new()
        {
            ["VacancyMembershipId"] = seed.DepartedMembershipId.ToString(),
            ["VacancyMembershipVersion"] = "1",
            ["ReplacementWaitingParticipantId"] = seed.WaitingId.ToString()
        }))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.NotNull(response.Headers.Location);
        }
        await using (var db = Db())
        {
            var result = await Service(db).ReplaceVacancyAsync(new(seed.EventId, seed.DepartedMembershipId, seed.AdminId, "c11-admin", seed.WaitingId));
            Assert.False(result.Succeeded);
            Assert.Contains("replacements and vacancies are retired", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(unchanged, await StateHashAsync());
    }

    [Fact]
    public async Task ConcurrentFinalizedRosterAddsHaveOnlyOneWinner()
    {
        var seed = await SeedAsync();
        await RemoveFinalizedAsync(seed, seed.DepartedId);
        long firstTeamVersion;
        long secondTeamVersion;
        await using (var db = Db())
        {
            firstTeamVersion = await db.Teams.Where(x => x.Id == seed.TeamId).Select(x => x.Version).SingleAsync();
            secondTeamVersion = await db.Teams.Where(x => x.Id == seed.SecondTeamId).Select(x => x.Version).SingleAsync();
        }
        async Task<FinalizedRosterMutationResult> Add(Guid teamId, long version)
        {
            await using var db = Db();
            return await Service(db).AddFinalizedRosterParticipantAsync(new(seed.EventId, teamId, seed.AdminId, "c11-admin", ParticipantId: seed.WaitingId, ExpectedTeamVersion: version));
        }
        var results = await Task.WhenAll(Add(seed.TeamId, firstTeamVersion), Add(seed.SecondTeamId, secondTeamVersion));
        Assert.Single(results, x => x.Succeeded);
        Assert.Single(results, x => !x.Succeeded);
        await using (var db = Db())
        {
            Assert.Single(await db.TeamMemberships.Where(x => x.EventParticipantId == seed.WaitingId && x.LeftAt == null).ToListAsync());
            Assert.Single(await db.DraftPublicationCycles.Where(x => x.SupersededAt == null).ToListAsync());
            Assert.Equal(3, await db.DraftPublicationCycles.CountAsync());
            Assert.Empty(await db.WaitingListPromotionFollowUps.ToListAsync());
            Assert.Empty(await db.PersonalNotifications.ToListAsync());
        }
        var snapshot = await StateHashAsync();
        Assert.False((await AddFinalizedAsync(seed, seed.TeamId, participantId: seed.WaitingId)).Succeeded);
        Assert.Equal(snapshot, await StateHashAsync());
    }

    [Theory]
    [InlineData("withdraw", "publication")]
    [InlineData("withdraw", "notification")]
    [InlineData("withdraw", "audit")]
    [InlineData("waiting", "publication")]
    [InlineData("internal", "publication")]
    public async Task FailureAfterFlushRollsBackTheWholeCorrectionAndRetrySucceeds(string action, string fault)
    {
        var seed = await SeedAsync();
        if (action != "withdraw") await RemoveFinalizedAsync(seed, seed.DepartedId);
        if (action == "internal")
        {
            await using var setup = Db();
            var owner = await setup.Accounts.SingleAsync(x => x.Id == seed.InternalOwnerId);
            var character = new OsrsCharacter(Guid.NewGuid(), "C11 fault replacement", "C11 FAULT REPLACEMENT", clock.Now);
            setup.AddRange(character, new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, character.Id, owner.Id, true, 0, null, 42m, clock.Now));
            await setup.SaveChangesAsync();
        }
        var before = await StateHashAsync();
        var failure = new SaveFailure(fault);
        await using (var db = Db(failure))
        {
            FinalizedRosterMutationResult result;
            if (action == "withdraw")
            {
                var version = await db.TeamMemberships.Where(x => x.Id == seed.DepartedMembershipId).Select(x => x.Version).SingleAsync();
                result = await Service(db).RemoveFinalizedRosterParticipantAsync(new(seed.EventId, seed.DepartedId, seed.AdminId, "c11-admin", true, version));
            }
            else
            {
                var teamVersion = await db.Teams.Where(x => x.Id == seed.TeamId).Select(x => x.Version).SingleAsync();
                result = await Service(db).AddFinalizedRosterParticipantAsync(new(seed.EventId, seed.TeamId, seed.AdminId, "c11-admin",
                    WebsiteAccountId: action == "internal" ? seed.InternalOwnerId : null,
                    ParticipantId: action == "waiting" ? seed.WaitingId : null,
                    ExpectedTeamVersion: teamVersion));
            }
            Assert.False(result.Succeeded, result.Error);
        }
        Assert.True(failure.Triggered);
        Assert.Equal(before, await StateHashAsync());
        if (action == "withdraw")
            Assert.True((await RemoveFinalizedAsync(seed, seed.DepartedId)).Succeeded);
        else if (action == "waiting")
            Assert.True((await AddFinalizedAsync(seed, seed.TeamId, participantId: seed.WaitingId)).Succeeded);
        else
            Assert.True((await AddFinalizedAsync(seed, seed.TeamId, websiteAccountId: seed.InternalOwnerId)).Succeeded);
    }

    [Fact]
    public async Task HttpNoteLengthFailurePreservesInputAndNotesThenBlankAndReplayPreserveStoredValue()
    {
        var seed = await SeedAsync();
        await using var factory = Factory();
        using var admin = await LoginAsync(factory, "c11-admin");
        var path = ParticipantPath(seed, seed.DepartedId);
        var page = await admin.GetStringAsync(path);
        var attempt = new string('x', 2001);
        var before = await StateHashAsync();
        using (var rejected = await PostAsync(admin, path, "AdminNote", page, new()
        {
            ["AdminNote"] = attempt,
            ["ExpectedAdminNote"] = Input(page, "ExpectedAdminNote")
        }))
        {
            Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        }
        Assert.Equal(before, await StateHashAsync());
        page = await admin.GetStringAsync(path);
        using (var saved = await PostAsync(admin, path, "AdminNote", page, new()
        {
            ["AdminNote"] = "Stored replacement note",
            ["ExpectedAdminNote"] = Input(page, "ExpectedAdminNote")
        })) Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        await using (var db = Db()) Assert.Equal("Stored replacement note", (await db.EventParticipants.SingleAsync(x => x.Id == seed.DepartedId)).AdminNotes);
        page = await admin.GetStringAsync(path);
        using (var blank = await PostAsync(admin, path, "AdminNote", page, new()
        {
            ["AdminNote"] = " \n ",
            ["ExpectedAdminNote"] = Input(page, "ExpectedAdminNote")
        })) Assert.Equal(HttpStatusCode.Redirect, blank.StatusCode);
        var after = await StateHashAsync();
        await using (var db = Db())
        {
            var participant = await db.EventParticipants.SingleAsync(x => x.Id == seed.DepartedId);
            Assert.Null(participant.AdminNotes);
            var replay = await Service(db).SetAdminNotesAsync(seed.EventId, seed.DepartedId, seed.AdminId, "c11-admin", " \n ", null);
            Assert.True(replay.Succeeded); Assert.False(replay.Changed);
        }
        Assert.Equal(after, await StateHashAsync());
    }

    [Fact]
    public async Task NoteEditOrderingUsesLatestLockedValueAndRejectsStaleOverwrite()
    {
        var seed = await SeedAsync();
        await using (var editor = Db())
            Assert.True((await Service(editor).SetAdminNotesAsync(seed.EventId, seed.DepartedId, seed.AdminId, "c11-admin", "Fresh note", "Existing private note")).Succeeded);
        await using (var stale = Db())
        {
            var result = await Service(stale).SetAdminNotesAsync(seed.EventId, seed.DepartedId, seed.AdminId, "c11-admin", "stale overwrite", "Existing private note");
            Assert.False(result.Succeeded);
            Assert.Contains("changed elsewhere", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        await using var verify = Db();
        Assert.Equal("Fresh note", (await verify.EventParticipants.SingleAsync(x => x.Id == seed.DepartedId)).AdminNotes);
        var removed = await RemoveFinalizedAsync(seed, seed.DepartedId);
        Assert.True(removed.Succeeded, removed.Error);
        await using var after = Db();
        Assert.Equal("Fresh note", (await after.EventParticipants.SingleAsync(x => x.Id == seed.DepartedId)).AdminNotes);
    }

    [Theory]
    [InlineData("Running")]
    [InlineData("Paused")]
    [InlineData("Expired")]
    [InlineData("FinalReview")]
    [InlineData("Finalized")]
    [InlineData("Archived")]
    [InlineData("Cancelled")]
    [InlineData("Hidden")]
    [InlineData("Setup")]
    [InlineData("NoPublication")]
    [InlineData("NoDraft")]
    [InlineData("Discarded")]
    public async Task ServiceAndHttpDenyUnsupportedStatesWithoutRosterWrites(string state)
    {
        var seed = await SeedAsync();
        await using (var db = Db())
        {
            var item = await db.Events.SingleAsync(x => x.Id == seed.EventId);
            var draft = await db.DraftSessions.SingleAsync();
            if (state is "Running" or "Paused" or "Setup") { draft.Reopen(clock.Now); if (state == "Paused") draft.Pause(); if (state == "Setup") draft.ReturnToSetup(); }
            else if (state is "NoPublication" or "NoDraft")
            {
                db.DraftPublicationRosters.RemoveRange(await db.DraftPublicationRosters.ToListAsync());
                db.DraftPublicationCycles.RemoveRange(await db.DraftPublicationCycles.ToListAsync());
                if (state == "NoDraft")
                {
                    foreach (var membership in await db.TeamMemberships.ToListAsync()) db.Entry(membership).Property(x => x.AssignedByDraftPickId).CurrentValue = null;
                    db.DraftPicks.RemoveRange(await db.DraftPicks.ToListAsync()); db.DraftSessions.Remove(draft);
                }
            }
            else if (state == "Discarded") db.Entry(item).Property(x => x.State).CurrentValue = EventState.Discarded;
            else if (state == "Expired") clock.Now = item.EventEndsAt!.Value;
            else if (state == "Cancelled") item.Cancel(seed.AdminId, clock.Now, "Fixture cancelled", true);
            else
            {
                item.StartEvent(clock.Now.AddHours(-2)); item.EndEvent(clock.Now.AddHours(-1));
                if (state is "Finalized" or "Archived") item.FinalizeResults(clock.Now.AddMinutes(-30));
                if (state == "Archived") item.Archive(clock.Now.AddMinutes(-20));
                if (state == "Hidden") item.Hide(seed.AdminId, clock.Now, item.Name, "Fixture hidden");
            }
            await db.SaveChangesAsync();
        }
        var before = await StateHashAsync();
        await using (var db = Db())
            Assert.False((await Service(db).WithdrawAsync(seed.EventId, seed.DepartedId, seed.AdminId, "admin", true, null, 1)).Succeeded);
        await using var factory = Factory();
        using var admin = await LoginAsync(factory, "c11-admin");
        var path = ParticipantPath(seed, seed.DepartedId);
        using var page = await admin.GetAsync(path);
        if (state is "Hidden" or "Discarded") Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        else
        {
            var html = await page.Content.ReadAsStringAsync();
            Assert.DoesNotContain("?handler=Withdraw", html);
            Assert.DoesNotContain("Fill open vacancy", html);
            using var response = await PostAsync(admin, path, "Withdraw", html, new() { ["ConfirmLifecycleAction"] = "true", ["ExpectedMembershipVersion"] = "1" });
            Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        Assert.Equal(before, await StateHashAsync());
    }

    [Fact]
    public async Task HttpAuthAntiforgeryConfirmationAndStaleVersionsFailBeforeAnyMutation()
    {
        var seed = await SeedAsync();
        await using var factory = Factory();
        using var admin = await LoginAsync(factory, "c11-admin");
        using var outsider = await LoginAsync(factory, "c11-outsider");
        using var anonymous = factory.CreateClient(new() { AllowAutoRedirect = false });
        var path = ParticipantPath(seed, seed.DepartedId);
        var page = await admin.GetStringAsync(path);
        var before = await StateHashAsync();
        using (var denied = await anonymous.GetAsync(path)) Assert.NotEqual(HttpStatusCode.OK, denied.StatusCode);
        using (var denied = await outsider.GetAsync(path)) Assert.NotEqual(HttpStatusCode.OK, denied.StatusCode);
        using (var missing = await admin.PostAsync(path + "?handler=Withdraw", new FormUrlEncodedContent(new Dictionary<string, string> { ["ConfirmLifecycleAction"] = "true" })))
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using (var invalid = await admin.PostAsync(path + "?handler=Withdraw", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["ConfirmLifecycleAction"] = "true", ["__RequestVerificationToken"] = "not-an-antiforgery-token" }))) Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using (var unconfirmed = await PostAsync(admin, path, "Withdraw", page, new())) Assert.Equal(HttpStatusCode.Redirect, unconfirmed.StatusCode);
        using (var stale = await PostAsync(admin, path, "Withdraw", page, new() { ["ConfirmLifecycleAction"] = "true", ["ExpectedMembershipVersion"] = "999" }))
            Assert.Contains("changed elsewhere", await stale.Content.ReadAsStringAsync());
        await using (var db = Db())
        {
            Assert.False((await Service(db).WithdrawAsync(seed.EventId, seed.DepartedId, seed.OutsiderId, "outsider", true, null, 1)).Succeeded);
            Assert.False((await Service(db).WithdrawAsync(seed.EventId, seed.DepartedId, seed.DepartedOwnerId, "owner", false, null, 1)).Succeeded);
        }
        Assert.Equal(before, await StateHashAsync());
        await WithdrawAsync(seed);
        before = await StateHashAsync();
        await using (var db = Db())
        {
            Assert.False((await Service(db).ReplaceVacancyAsync(new(seed.EventId, seed.DepartedMembershipId, seed.AdminId, "admin", seed.WaitingId, ExpectedVacancyVersion: 999))).Succeeded);
            Assert.False((await Service(db).ReplaceVacancyAsync(new(seed.EventId, Guid.NewGuid(), seed.AdminId, "admin", seed.WaitingId))).Succeeded);
            Assert.False((await Service(db).ReplaceVacancyAsync(new(seed.EventId, seed.DepartedMembershipId, seed.AdminId, "admin", seed.LeaderId))).Succeeded);
        }
        Assert.Equal(before, await StateHashAsync());
    }

    [Theory]
    [InlineData("publication")]
    [InlineData("role-audit")]
    public async Task HttpRolePublicationFailureRollsBackRoleHistoryNoticeAndCycleThenRetryWorks(string failurePoint)
    {
        var seed = await SeedAsync(); await WithdrawAsync(seed);
        var failure = new SaveFailure(failurePoint);
        await using var factory = Factory(failure);
        using var admin = await LoginAsync(factory, "c11-admin");
        var path = $"/Admin/Events/Draft/{seed.EventId}";
        var page = await admin.GetStringAsync(path);
        Guid memberId; long version;
        await using (var db = Db()) { var member = await db.TeamMemberships.SingleAsync(x => x.EventParticipantId == seed.LeaderId && x.LeftAt == null); memberId = member.Id; version = member.Version; }
        var form = new Dictionary<string, string> { ["membershipId"] = memberId.ToString(), ["membershipVersion"] = version.ToString(CultureInfo.InvariantCulture), ["role"] = "Captain" };
        var before = await StateHashAsync();
        using (var failed = await PostAsync(admin, path, "ChangeRole", page, form)) Assert.Equal(HttpStatusCode.Redirect, failed.StatusCode);
        Assert.True(failure.Triggered); Assert.Equal(before, await StateHashAsync());
        await using (var db = Db()) Assert.Empty(await db.AuditEntries.Where(x => x.Action == "team.role_roster_published").ToListAsync());
        Assert.Contains("role correction could not be saved", await admin.GetStringAsync(path));
        failure.Enabled = false;
        using (var retry = await PostAsync(admin, path, "ChangeRole", page, form)) Assert.Equal(HttpStatusCode.Redirect, retry.StatusCode);
        var after = await StateHashAsync();
        Assert.NotEqual(before, after);
        using (var stale = await PostAsync(admin, path, "ChangeRole", page, form)) Assert.Equal(HttpStatusCode.Redirect, stale.StatusCode);
        Assert.Equal(after, await StateHashAsync());
        await using (var db = Db())
        {
            var audit = Assert.Single(await db.AuditEntries.Where(x => x.Action == "team.role_roster_published").ToListAsync());
            Assert.Equal(seed.AdminId, audit.ActorAccountId);
            Assert.Equal("c11-admin", audit.ActorUsername);
            Assert.Equal("draft_publication", audit.TargetType);
            Assert.Equal(memberId.ToString(), audit.TargetId);
            Assert.Equal(seed.EventId, audit.EventId);
            Assert.Contains("Superseded publication cycle", audit.Details);
            Assert.Contains("Pre-Live Captain role correction", audit.Details);
            Assert.Null(audit.BeforeState);
            Assert.Null(audit.AfterState);
        }
        const string actionRow = "<dt>Action key</dt><dd><code>team.role_roster_published</code></dd>";
        var auditPage = await admin.GetStringAsync($"/Admin/Audit?EventId={seed.EventId}");
        Assert.Contains("team.role_roster_published", auditPage);
        Assert.Contains(actionRow, auditPage);
        await using (var db = Db())
        {
            var item = await db.Events.SingleAsync(x => x.Id == seed.EventId);
            item.StartEvent(clock.Now.AddHours(-2));
            item.EndEvent(clock.Now.AddHours(-1));
            item.Hide(seed.AdminId, clock.Now, item.Name, "Controlled audit visibility check");
            await db.SaveChangesAsync();
        }
        var hiddenAuditPage = await admin.GetStringAsync("/Admin/Audit?Action=team.role_roster_published");
        Assert.Contains("No audit entries found", hiddenAuditPage);
        Assert.DoesNotContain(actionRow, hiddenAuditPage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyPublishedTeamsRetainHistoryAndSeparateOwnStatusFromIndependentAdminAlerts(bool adminOwner)
    {
        var seed = await SeedAsync();
        await using (var db = Db())
        {
            var participant = await db.EventParticipants.SingleAsync(x => x.Id == seed.DepartedId);
            if (adminOwner) (await db.Accounts.SingleAsync(x => x.Id == seed.DepartedOwnerId)).SetGlobalRole(GlobalRole.Admin);
            else db.Entry(participant).Property(x => x.AccountId).CurrentValue = null;
            await db.SaveChangesAsync();
        }
        var removed = await RemoveFinalizedAsync(seed, seed.DepartedId);
        Assert.True(removed.Succeeded, removed.Error);
        await using (var db = Db())
        {
            var notices = await db.PersonalNotifications.Where(x => x.RecipientAccountId == seed.DepartedOwnerId).ToListAsync();
            Assert.Empty(notices);
            Assert.Equal(SignupStatus.Confirmed, (await db.EventParticipants.SingleAsync(x => x.Id == seed.DepartedId)).SignupStatus);
        }
        List<Guid> remaining;
        await using (var db = Db()) remaining = await db.TeamMemberships.Where(x => x.LeftAt == null).Select(x => x.EventParticipantId).ToListAsync();
        foreach (var participant in remaining)
        {
            var result = await RemoveFinalizedAsync(seed, participant);
            Assert.True(result.Succeeded, result.Error);
        }
        await using var factory = Factory(); using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using (var page = await client.GetAsync(TeamsPath(seed))) Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        await using var final = Db();
        var cycle = await final.DraftPublicationCycles.SingleAsync(x => x.SupersededAt == null);
        Assert.Empty(await final.DraftPublicationRosters.Where(x => x.DraftPublicationCycleId == cycle.Id).ToListAsync());
        Assert.Equal(2, await final.DraftPicks.CountAsync());
        Assert.Equal(4, await final.DraftPublicationCycles.CountAsync());
        Assert.Equal(0, await final.PersonalNotifications.CountAsync());
    }

    [Fact]
    public async Task MutableCurrentNameCannotRewritePublishedPickAndOrdinaryReleasedWithdrawalStillHasIdentity()
    {
        var seed = await SeedAsync();
        var removed = await RemoveFinalizedAsync(seed, seed.DepartedId);
        Assert.True(removed.Succeeded, removed.Error);
        await using (var db = Db())
        {
            var character = await db.OsrsCharacters.SingleAsync(x => x.DisplayName == "Departed C");
            db.Entry(character).Property(x => x.DisplayName).CurrentValue = "Current renamed player";
            db.Entry(character).Property(x => x.NormalizedName).CurrentValue = "CURRENT RENAMED PLAYER";
            var ordinary = await db.EventParticipants.SingleAsync(x => x.Id == seed.OtherWaitingId);
            ordinary.Withdraw(clock.Now, "Ordinary fixture departure", seed.AdminId);
            foreach (var assignment in await db.EventParticipantCharacters.Where(x => x.EventParticipantId == ordinary.Id && x.ReleasedAt == null).ToListAsync()) assignment.Release(seed.AdminId, clock.Now);
            await db.SaveChangesAsync();
        }
        await using var factory = Factory(); using var admin = await LoginAsync(factory, "c11-admin");
        var search = await admin.GetStringAsync($"/Admin/Events/Participants/{seed.EventId}?ParticipantStatus=Confirmed&ParticipantSearch=Current%20renamed%20player");
        Assert.Contains($"participant-row-{seed.DepartedId}", search); Assert.Contains(ParticipantPath(seed, seed.DepartedId), search); Assert.Contains("Current renamed player", search);
        var nonmatching = await admin.GetStringAsync($"/Admin/Events/Participants/{seed.EventId}?ParticipantStatus=Confirmed&ParticipantSearch=Departed%20C");
        Assert.DoesNotContain($"participant-row-{seed.DepartedId}", nonmatching);
        Assert.Contains("Current renamed player", await admin.GetStringAsync(ParticipantPath(seed, seed.DepartedId)));
        var withdrawnSearch = await admin.GetStringAsync($"/Admin/Events/Participants/{seed.EventId}?ParticipantStatus=Withdrawn&ParticipantSearch=First%20waiting");
        Assert.Contains(ParticipantPath(seed, seed.OtherWaitingId), withdrawnSearch);
        Assert.Contains("First waiting", await admin.GetStringAsync(ParticipantPath(seed, seed.OtherWaitingId)));
        var draft = await admin.GetStringAsync($"/Admin/Events/Draft/{seed.EventId}");
        var finalizedRoster = Regex.Match(draft, "<ol class=\"finalized-roster-list\">(?<roster>[\\s\\S]*?)</ol>").Groups["roster"].Value;
        Assert.Contains("Leader", finalizedRoster);
        Assert.DoesNotContain("Departed C", finalizedRoster);
        Assert.DoesNotContain("Current renamed player", finalizedRoster);
        var published = await admin.GetStringAsync(TeamsPath(seed));
        Assert.Contains("Departed C", PickSection(published)); Assert.DoesNotContain("Current renamed player", PickSection(published));
    }

    [Fact]
    public async Task CrossEventUnavailableAndDisabledActorRequestsFailWithoutWrites()
    {
        var seed = await SeedAsync(); await WithdrawAsync(seed);
        Guid foreignVacancy; Guid foreignParticipant;
        await using (var db = Db())
        {
            var item = new BingoEvent(Guid.NewGuid(), "Other C11 event", "other-c11", "UTC", seed.AdminId, clock.Now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
            var team = new Team(Guid.NewGuid(), item.Id, "Other team", "other", TeamFormationType.Drafted, null, true, clock.Now);
            var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.WaitingList, 1, clock.Now, SignupSource.AdminCreated);
            var member = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, clock.Now, null, "Foreign fixture");
            member.Leave(clock.Now, "Foreign vacancy"); foreignVacancy = member.Id; foreignParticipant = participant.Id;
            db.AddRange(item, team, participant, member); await db.SaveChangesAsync();
        }
        await using var factory = Factory(); using var admin = await LoginAsync(factory, "c11-admin");
        var path = ParticipantPath(seed, seed.DepartedId); var page = await admin.GetStringAsync(path);
        var before = await StateHashAsync();
        foreach (var (vacancy, candidate) in new[] { (foreignVacancy, seed.WaitingId), (seed.DepartedMembershipId, foreignParticipant) })
        {
            using var result = await PostAsync(admin, path, "FillVacancy", page, new() { ["VacancyMembershipId"] = vacancy.ToString(), ["ReplacementWaitingParticipantId"] = candidate.ToString() });
            Assert.Equal(HttpStatusCode.Redirect, result.StatusCode); Assert.Equal(before, await StateHashAsync());
        }
        await using (var db = Db()) { (await db.Accounts.SingleAsync(x => x.Id == seed.AdminId)).Disable(clock.Now); await db.SaveChangesAsync(); }
        using (var denied = await PostAsync(admin, path, "FillVacancy", page, new() { ["VacancyMembershipId"] = seed.DepartedMembershipId.ToString(), ["ReplacementWaitingParticipantId"] = seed.WaitingId.ToString() }))
            Assert.NotEqual(HttpStatusCode.OK, denied.StatusCode);
        await using (var db = Db()) Assert.False((await Service(db).ReplaceVacancyAsync(new(seed.EventId, seed.DepartedMembershipId, seed.AdminId, "disabled", seed.WaitingId))).Succeeded);
        Assert.Equal(before, await StateHashAsync());
    }

    [Fact]
    public async Task TwoVacanciesCannotPromoteTheSameWaitingParticipant()
    {
        var seed = await SeedAsync();
        Assert.True((await RemoveFinalizedAsync(seed, seed.DepartedId)).Succeeded);
        Assert.True((await RemoveFinalizedAsync(seed, seed.LeaderId)).Succeeded);
        long firstTeamVersion;
        long secondTeamVersion;
        await using (var db = Db())
        {
            firstTeamVersion = await db.Teams.Where(x => x.Id == seed.TeamId).Select(x => x.Version).SingleAsync();
            secondTeamVersion = await db.Teams.Where(x => x.Id == seed.SecondTeamId).Select(x => x.Version).SingleAsync();
        }
        async Task<FinalizedRosterMutationResult> Add(Guid teamId, long version)
        {
            await using var db = Db();
            return await Service(db).AddFinalizedRosterParticipantAsync(new(seed.EventId, teamId, seed.AdminId, "c11-admin", ParticipantId: seed.WaitingId, ExpectedTeamVersion: version));
        }
        var results = await Task.WhenAll(Add(seed.TeamId, firstTeamVersion), Add(seed.SecondTeamId, secondTeamVersion));
        Assert.Single(results, x => x.Succeeded); Assert.Single(results, x => !x.Succeeded);
        await using var final = Db();
        Assert.Single(await final.TeamMemberships.Where(x => x.EventParticipantId == seed.WaitingId && x.LeftAt == null).ToListAsync());
        Assert.Empty(await final.WaitingListPromotionFollowUps.ToListAsync());
        Assert.Empty(await final.PersonalNotifications.ToListAsync());
        Assert.Equal(4, await final.DraftPublicationCycles.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleRolePostAfterWithdrawalOrActualStartCannotPublishOrChangeRoles(bool startInstead)
    {
        var seed = await SeedAsync(); if (startInstead) await PublishBoardAsync(seed);
        var barrier = new BeforeEventLock();
        await using var factory = Factory(barrier); using var admin = await LoginAsync(factory, "c11-admin");
        using var starter = await LoginAsync(factory, "c11-admin");
        var path = $"/Admin/Events/Draft/{seed.EventId}"; var page = await admin.GetStringAsync(path);
        var pending = PostAsync(admin, path, "ChangeRole", page, new()
        {
            ["membershipId"] = seed.DepartedMembershipId.ToString(),
            ["membershipVersion"] = "1",
            ["role"] = "Participant"
        });
        await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(20));
        try
        {
            if (startInstead)
            {
                var managePath = $"/Admin/Events/Manage/{seed.EventId}"; var manage = await starter.GetStringAsync(managePath);
                using var start = await PostAsync(starter, managePath, "StartEvent", manage, new()
                {
                    ["EventVersion"] = Input(manage, "EventVersion"),
                    ["ConfirmStartEvent"] = "true",
                    ["StartReason"] = "Controlled early start"
                });
                Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
                await using var db = Db(); Assert.Equal(EventState.Live, (await db.Events.SingleAsync(x => x.Id == seed.EventId)).State);
            }
            else await WithdrawAsync(seed);
            var beforeResume = await StateHashAsync();
            barrier.Release.TrySetResult();
            using var response = await pending; Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(beforeResume, await StateHashAsync());
        }
        finally { barrier.Release.TrySetResult(); }
    }

    [Fact]
    public async Task ExactNoteLimitAndMissedScheduledStartRemainUsable()
    {
        var seed = await SeedAsync();
        clock.Now = clock.Now.AddHours(2); // configured start missed, configured end still future
        var exact = new string('x', 2000);
        await using (var notes = Db())
        {
            var result = await Service(notes).SetAdminNotesAsync(seed.EventId, seed.DepartedId, seed.AdminId, "c11-admin", exact, "Existing private note");
            Assert.True(result.Succeeded, result.Error);
        }
        Assert.True((await RemoveFinalizedAsync(seed, seed.DepartedId)).Succeeded);
        await using var db = Db();
        Assert.Equal(2000, (await db.EventParticipants.SingleAsync(x => x.Id == seed.DepartedId)).AdminNotes!.Length);
        Assert.Null((await db.Events.SingleAsync(x => x.Id == seed.EventId)).ActualStartedAt);
        Assert.True((await AddFinalizedAsync(seed, seed.TeamId, participantId: seed.WaitingId)).Succeeded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LiveMinuteBoundaryPreservesRequestInstantAcrossEventLockContention(bool replacement)
    {
        var seed = await SeedAsync();
        await using (var db = Db()) { (await db.Events.SingleAsync(x => x.Id == seed.EventId)).StartEvent(clock.Now.AddHours(-1)); await db.SaveChangesAsync(); }
        var before = await StateHashAsync();
        long membershipVersion;
        await using (var db = Db())
            membershipVersion = await db.TeamMemberships.Where(x => x.Id == seed.DepartedMembershipId).Select(x => x.Version).SingleAsync();
        if (replacement)
        {
            await using var db = Db();
            var withdrawn = await Service(db).WithdrawLiveAsync(new(seed.EventId, seed.DepartedId, seed.AdminId, "admin", membershipVersion));
            Assert.False(withdrawn.Succeeded);
            Assert.Contains("Roster membership is fixed after the event first goes Live.", withdrawn.Error, StringComparison.Ordinal);
            var replaced = await Service(db).ReplaceVacancyAsync(new(seed.EventId, seed.DepartedMembershipId, seed.AdminId, "admin", seed.WaitingId));
            Assert.False(replaced.Succeeded);
            Assert.Contains("replacements and vacancies are retired", replaced.Error, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            await using var db = Db();
            var withdrawn = await Service(db).WithdrawLiveAsync(new(seed.EventId, seed.DepartedId, seed.AdminId, "admin", membershipVersion));
            Assert.False(withdrawn.Succeeded);
            Assert.Contains("Roster membership is fixed after the event first goes Live.", withdrawn.Error, StringComparison.Ordinal);
        }
        Assert.Equal(before, await StateHashAsync());
    }

    [Theory]
    [InlineData(false, "en")]
    [InlineData(false, "da")]
    [InlineData(true, "en")]
    [InlineData(true, "da")]
    public async Task InboxTitlesMatchPreLiveAndLivePhaseInEnglishAndDanish(bool live, string culture)
    {
        var seed = await SeedAsync();
        if (live)
        {
            await using var db = Db();
            (await db.Events.SingleAsync(x => x.Id == seed.EventId)).StartEvent(clock.Now.AddHours(-1));
            await db.SaveChangesAsync();
        }
        await using var factory = Factory();
        using var admin = await LoginAsync(factory, "c11-admin");
        if (live)
        {
            var before = await StateHashAsync();
            long membershipVersion;
            await using (var db = Db())
                membershipVersion = await db.TeamMemberships.Where(x => x.Id == seed.DepartedMembershipId).Select(x => x.Version).SingleAsync();
            await using (var db = Db())
            {
                var withdrawn = await Service(db).WithdrawLiveAsync(new(seed.EventId, seed.DepartedId, seed.AdminId, "admin", membershipVersion));
                Assert.False(withdrawn.Succeeded);
                Assert.Contains("Roster membership is fixed after the event first goes Live.", withdrawn.Error, StringComparison.Ordinal);
            }
            await using (var db = Db())
            {
                var retiredReplacement = await Service(db).ReplaceVacancyAsync(new(seed.EventId, seed.DepartedMembershipId, seed.AdminId, "admin", seed.WaitingId));
                Assert.False(retiredReplacement.Succeeded);
                Assert.Contains("replacements and vacancies are retired", retiredReplacement.Error, StringComparison.OrdinalIgnoreCase);
            }
            Assert.Equal(before, await StateHashAsync());
            var participantPage = await admin.GetStringAsync(ParticipantPath(seed, seed.DepartedId));
            Assert.DoesNotContain("Fill open vacancy", participantPage, StringComparison.Ordinal);
            var notifications = await admin.GetStringAsync("/Notifications");
            Assert.DoesNotContain("Live participant withdrawn", notifications, StringComparison.Ordinal);
            return;
        }
        var removed = await RemoveFinalizedAsync(seed, seed.DepartedId);
        Assert.True(removed.Succeeded, removed.Error);
        var added = await AddFinalizedAsync(seed, seed.TeamId, participantId: seed.WaitingId);
        Assert.True(added.Succeeded, added.Error);
        await using var context = Db();
        var notices = await context.PersonalNotifications.Where(x => x.RecipientAccountId == seed.AdminId).OrderBy(x => x.CreatedAt).ToListAsync();
        Assert.Empty(notices);
        admin.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
        var html = WebUtility.HtmlDecode(await admin.GetStringAsync("/Notifications"));
        Assert.DoesNotContain("participant.withdrawn", html, StringComparison.Ordinal);
        Assert.DoesNotContain("participant.prelive_withdrawn", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Replacement confirmed", html, StringComparison.Ordinal);
        Assert.Contains("Waiting C", RosterSection(await admin.GetStringAsync(TeamsPath(seed))));
    }

    [Fact]
    public async Task FinalizedRosterAddRequiresFreshTeamVersionReusesEligibleAccountsWithoutACap()
    {
        var seed = await SeedAsync();
        await using (var db = Db())
        {
            var first = await db.Accounts.SingleAsync(x => x.Id == seed.InternalOwnerId);
            var second = await db.Accounts.SingleAsync(x => x.Id == seed.OutsiderId);
            var firstCharacter = new OsrsCharacter(Guid.NewGuid(), "Add reuse one", "ADD REUSE ONE", clock.Now);
            var secondCharacter = new OsrsCharacter(Guid.NewGuid(), "Add reuse two", "ADD REUSE TWO", clock.Now);
            db.AddRange(firstCharacter, secondCharacter,
                new AccountOsrsCharacter(Guid.NewGuid(), first.Id, firstCharacter.Id, first.Id, true, 0, null, 44m, clock.Now),
                new AccountOsrsCharacter(Guid.NewGuid(), second.Id, secondCharacter.Id, second.Id, true, 0, null, 45m, clock.Now));
            await db.SaveChangesAsync();
        }

        long expectedTeamVersion;
        await using (var db = Db()) expectedTeamVersion = await db.Teams.Where(x => x.Id == seed.TeamId).Select(x => x.Version).SingleAsync();
        await using (var missing = Db())
        {
            var rejected = await Service(missing).AddFinalizedRosterParticipantAsync(new(seed.EventId, seed.TeamId, seed.AdminId, "admin", seed.InternalOwnerId));
            Assert.False(rejected.Succeeded);
            Assert.Contains("version is required", rejected.Error, StringComparison.OrdinalIgnoreCase);
        }
        await using (var changed = Db())
        {
            var team = await changed.Teams.SingleAsync(x => x.Id == seed.TeamId);
            team.AdvanceVersion();
            await changed.SaveChangesAsync();
        }
        await using (var stale = Db())
        {
            var rejected = await Service(stale).AddFinalizedRosterParticipantAsync(new(seed.EventId, seed.TeamId, seed.AdminId, "admin", seed.InternalOwnerId, ExpectedTeamVersion: expectedTeamVersion));
            Assert.False(rejected.Succeeded);
            Assert.Contains("changed elsewhere", rejected.Error, StringComparison.OrdinalIgnoreCase);
        }
        await using (var restore = Db())
        {
            var team = await restore.Teams.SingleAsync(x => x.Id == seed.TeamId);
            expectedTeamVersion = team.Version;
        }

        FinalizedRosterMutationResult firstResult;
        FinalizedRosterMutationResult secondResult;
        await using (var db = Db())
            firstResult = await Service(db).AddFinalizedRosterParticipantAsync(new(seed.EventId, seed.TeamId, seed.AdminId, "admin", seed.InternalOwnerId, ExpectedTeamVersion: expectedTeamVersion));
        await using (var db = Db())
            secondResult = await Service(db).AddFinalizedRosterParticipantAsync(new(seed.EventId, seed.TeamId, seed.AdminId, "admin", seed.OutsiderId, ExpectedTeamVersion: expectedTeamVersion));
        Assert.True(firstResult.Succeeded, firstResult.Error);
        Assert.True(secondResult.Succeeded, secondResult.Error);
        Assert.Equal("NotManaged", firstResult.WomSyncStatus);
        await using (var duplicate = Db())
        {
            var rejected = await Service(duplicate).AddFinalizedRosterParticipantAsync(new(seed.EventId, seed.TeamId, seed.AdminId, "admin", seed.InternalOwnerId, ExpectedTeamVersion: expectedTeamVersion));
            Assert.False(rejected.Succeeded);
            Assert.Contains("already on a current roster", rejected.Error, StringComparison.OrdinalIgnoreCase);
        }
        await using (var ownershipConflict = Db())
        {
            var rejected = await Service(ownershipConflict).AddFinalizedRosterParticipantAsync(new(seed.EventId, seed.TeamId, seed.AdminId, "admin", seed.OutsiderId, ParticipantId: seed.DepartedId, ExpectedTeamVersion: expectedTeamVersion));
            Assert.False(rejected.Succeeded);
            Assert.Contains("does not own", rejected.Error, StringComparison.OrdinalIgnoreCase);
        }

        await using var verify = Db();
        Assert.Equal(4, await verify.TeamMemberships.CountAsync(x => x.TeamId == seed.TeamId && x.LeftAt == null));
        Assert.Equal(5, await verify.EventParticipants.CountAsync(x => x.EventId == seed.EventId && x.SignupStatus == SignupStatus.Confirmed && verify.TeamMemberships.Any(m => m.EventParticipantId == x.Id && m.LeftAt == null)));
        var addAudits = await verify.AuditEntries.Where(x => x.EventId == seed.EventId && x.Action == "roster.finalized_added").ToListAsync();
        Assert.Equal(2, addAudits.Count);
        Assert.Contains(addAudits, audit => audit.Details?.Contains(seed.InternalOwnerId.ToString(), StringComparison.Ordinal) == true);
        Assert.Contains(addAudits, audit => audit.Details?.Contains("created-participant", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task FinalizedRosterAddPublishesCaptainRolesAndRejectsInvalidRoleWithoutWrites()
    {
        var seed = await SeedAsync();
        await using (var db = Db())
        {
            var captain = await db.Accounts.SingleAsync(x => x.Id == seed.InternalOwnerId);
            var coCaptain = await db.Accounts.SingleAsync(x => x.Id == seed.OutsiderId);
            var captainCharacter = new OsrsCharacter(Guid.NewGuid(), "Added Captain", "ADDED CAPTAIN", clock.Now);
            var coCaptainCharacter = new OsrsCharacter(Guid.NewGuid(), "Added Co-captain", "ADDED CO-CAPTAIN", clock.Now);
            db.AddRange(captainCharacter, coCaptainCharacter,
                new AccountOsrsCharacter(Guid.NewGuid(), captain.Id, captainCharacter.Id, captain.Id, true, 0, null, 44m, clock.Now),
                new AccountOsrsCharacter(Guid.NewGuid(), coCaptain.Id, coCaptainCharacter.Id, coCaptain.Id, true, 0, null, 45m, clock.Now));
            await db.SaveChangesAsync();
        }

        var captainAdd = await AddFinalizedAsync(seed, seed.TeamId, websiteAccountId: seed.InternalOwnerId, role: TeamMembershipRole.Captain);
        Assert.True(captainAdd.Succeeded, captainAdd.Error);
        await using (var afterCaptain = Db())
        {
            var cycle = await afterCaptain.DraftPublicationCycles.SingleAsync(x => x.SupersededAt == null);
            Assert.Equal(2, await afterCaptain.DraftPublicationCycles.CountAsync());
            Assert.Equal(TeamMembershipRole.Captain, await afterCaptain.TeamMemberships.Where(x => x.Id == captainAdd.MembershipId).Select(x => x.Role).SingleAsync());
            Assert.Equal(TeamMembershipRole.Captain, await afterCaptain.DraftPublicationRosters.Where(x => x.DraftPublicationCycleId == cycle.Id && x.EventParticipantId == captainAdd.ParticipantId).Select(x => x.Role).SingleAsync());
        }

        var coCaptainAdd = await AddFinalizedAsync(seed, seed.TeamId, websiteAccountId: seed.OutsiderId, role: TeamMembershipRole.CoCaptain);
        Assert.True(coCaptainAdd.Succeeded, coCaptainAdd.Error);
        await using (var afterCoCaptain = Db())
        {
            var cycle = await afterCoCaptain.DraftPublicationCycles.SingleAsync(x => x.SupersededAt == null);
            Assert.Equal(3, await afterCoCaptain.DraftPublicationCycles.CountAsync());
            Assert.Equal(TeamMembershipRole.CoCaptain, await afterCoCaptain.TeamMemberships.Where(x => x.Id == coCaptainAdd.MembershipId).Select(x => x.Role).SingleAsync());
            Assert.Equal(TeamMembershipRole.CoCaptain, await afterCoCaptain.DraftPublicationRosters.Where(x => x.DraftPublicationCycleId == cycle.Id && x.EventParticipantId == coCaptainAdd.ParticipantId).Select(x => x.Role).SingleAsync());
        }

        var beforeInvalid = await StateHashAsync();
        var invalid = await AddFinalizedAsync(seed, seed.TeamId, websiteAccountId: seed.InternalOwnerId, role: (TeamMembershipRole)999);
        Assert.False(invalid.Succeeded);
        Assert.Contains("Choose Participant", invalid.Error, StringComparison.Ordinal);
        Assert.Equal(beforeInvalid, await StateHashAsync());

        await using var factory = Factory();
        using var adminClient = await LoginAsync(factory, "c11-admin");
        var draftPage = await adminClient.GetStringAsync($"/Admin/Events/Draft/{seed.EventId}");
        Assert.Contains("name=\"role\"", draftPage, StringComparison.Ordinal);
        Assert.Contains("value=\"Captain\"", draftPage, StringComparison.Ordinal);
        Assert.Contains("value=\"CoCaptain\"", draftPage, StringComparison.Ordinal);
        using var publicClient = factory.CreateClient();
        var publicPage = RosterSection(await publicClient.GetStringAsync(TeamsPath(seed)));
        Assert.Matches("(?s)<li class=\"is-captain\">\\s*<span>Added Captain</span>", publicPage);
        Assert.Matches("(?s)<li class=\"is-co-captain\">\\s*<span>Added Co-captain</span>", publicPage);
    }

    [Fact]
    public async Task FinalizedRosterAddRequiresAnAuthoritativeLinkedPlayingSelectionForReusedParticipants()
    {
        var seed = await SeedAsync();
        Guid secondAlternativeId;
        await using (var db = Db())
        {
            var participant = await db.EventParticipants.SingleAsync(x => x.Id == seed.OtherWaitingId);
            var ownerId = participant.AccountId!.Value;
            var current = await db.EventParticipantCharacters.SingleAsync(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null && x.EventRole == EventCharacterRole.Playing && x.SignupQuestionId == seed.PrimaryQuestionId);
            var currentLink = await db.AccountOsrsCharacters.SingleAsync(x => x.AccountId == ownerId && x.OsrsCharacterId == current.OsrsCharacterId && x.Active);
            currentLink.Unlink(clock.Now);
            var first = new OsrsCharacter(Guid.NewGuid(), "Reused choice one", "REUSED CHOICE ONE", clock.Now);
            var second = new OsrsCharacter(Guid.NewGuid(), "Reused choice two", "REUSED CHOICE TWO", clock.Now);
            db.AddRange(first, second,
                new AccountOsrsCharacter(Guid.NewGuid(), ownerId, first.Id, ownerId, true, 0, null, 61m, clock.Now),
                new AccountOsrsCharacter(Guid.NewGuid(), ownerId, second.Id, ownerId, false, 1, null, 62m, clock.Now));
            await db.SaveChangesAsync();
            secondAlternativeId = second.Id;
        }

        long teamVersion;
        await using (var db = Db()) teamVersion = await db.Teams.Where(x => x.Id == seed.TeamId).Select(x => x.Version).SingleAsync();
        var beforeRejected = await StateHashAsync();
        await using (var ambiguous = Db())
        {
            var rejected = await Service(ambiguous).AddFinalizedRosterParticipantAsync(new(
                seed.EventId, seed.TeamId, seed.AdminId, "admin", ParticipantId: seed.OtherWaitingId, ExpectedTeamVersion: teamVersion));
            Assert.False(rejected.Succeeded);
            Assert.Contains("Select which linked Playing account", rejected.Error, StringComparison.Ordinal);
        }
        Assert.Equal(beforeRejected, await StateHashAsync());

        FinalizedRosterMutationResult added;
        await using (var db = Db())
            added = await Service(db).AddFinalizedRosterParticipantAsync(new(
                seed.EventId, seed.TeamId, seed.AdminId, "admin", ParticipantId: seed.OtherWaitingId,
                ExpectedTeamVersion: teamVersion, PlayingCharacterId: secondAlternativeId, PlayingEhb: 88.75m));
        Assert.True(added.Succeeded, added.Error);

        await using var verify = Db();
        var assignment = await verify.EventParticipantCharacters.SingleAsync(x => x.EventParticipantId == seed.OtherWaitingId && x.ReleasedAt == null && x.EventRole == EventCharacterRole.Playing && x.SignupQuestionId == seed.PrimaryQuestionId);
        Assert.Equal(secondAlternativeId, assignment.OsrsCharacterId);
        Assert.Equal(88.75m, assignment.EhbSnapshot);
        Assert.Equal(secondAlternativeId, await verify.SignupAnswers.Where(x => x.EventParticipantId == seed.OtherWaitingId && x.SignupQuestionId == seed.PrimaryQuestionId).Select(x => x.OsrsCharacterId).SingleAsync());
        Assert.Equal(secondAlternativeId, await verify.DraftPublicationRosters
            .Where(x => x.DraftPublicationCycleId == verify.DraftPublicationCycles.Where(c => c.DraftSessionId == verify.DraftSessions.Where(d => d.EventId == seed.EventId).Select(d => d.Id).Single() && c.SupersededAt == null).Select(c => c.Id).Single() && x.EventParticipantId == seed.OtherWaitingId)
            .Join(verify.OsrsCharacters, x => x.PublicCharacterName, x => x.DisplayName, (_, character) => character.Id)
            .SingleAsync());
        Assert.Single(await verify.TeamMemberships.Where(x => x.EventParticipantId == seed.OtherWaitingId && x.LeftAt == null).ToListAsync());
    }

    [Fact]
    public async Task FinalizedRosterReuseReconcilesPrimaryWithoutChangingOptionalPlayingAssignment()
    {
        var seed = await SeedAsync();
        Guid primaryId;
        Guid optionalAssignmentId;
        Guid optionalCharacterId;
        Guid optionalQuestionId;
        decimal optionalEhb;
        Guid optionalAnswerId;
        await using (var db = Db())
        {
            var assignments = await db.EventParticipantCharacters
                .Where(x => x.EventParticipantId == seed.WaitingId && x.ReleasedAt == null && x.EventRole == EventCharacterRole.Playing)
                .OrderBy(x => x.RegistrationOrder)
                .ToListAsync();
            var primary = assignments.Single(x => x.SignupQuestionId == seed.PrimaryQuestionId);
            var optional = assignments.Single(x => x.SignupQuestionId != seed.PrimaryQuestionId);
            var optionalAnswer = await db.SignupAnswers.SingleAsync(x => x.EventParticipantId == seed.WaitingId && x.SignupQuestionId == optional.SignupQuestionId);
            primaryId = primary.OsrsCharacterId;
            optionalAssignmentId = optional.Id;
            optionalCharacterId = optional.OsrsCharacterId;
            optionalQuestionId = optional.SignupQuestionId!.Value;
            optionalEhb = optional.EhbSnapshot!.Value;
            optionalAnswerId = optionalAnswer.Id;
        }

        long teamVersion;
        await using (var db = Db()) teamVersion = await db.Teams.Where(x => x.Id == seed.TeamId).Select(x => x.Version).SingleAsync();
        FinalizedRosterMutationResult added;
        await using (var db = Db())
            added = await Service(db).AddFinalizedRosterParticipantAsync(new(
                seed.EventId, seed.TeamId, seed.AdminId, "admin", ParticipantId: seed.WaitingId,
                ExpectedTeamVersion: teamVersion, PlayingCharacterId: primaryId, PlayingEhb: 77.25m));
        Assert.True(added.Succeeded, added.Error);

        await using var verify = Db();
        var primaryAfter = await verify.EventParticipantCharacters.SingleAsync(x => x.EventParticipantId == seed.WaitingId && x.ReleasedAt == null && x.SignupQuestionId == seed.PrimaryQuestionId);
        var optionalAfter = await verify.EventParticipantCharacters.SingleAsync(x => x.Id == optionalAssignmentId);
        var optionalAnswerAfter = await verify.SignupAnswers.SingleAsync(x => x.Id == optionalAnswerId);
        Assert.Equal(primaryId, primaryAfter.OsrsCharacterId);
        Assert.Equal(77.25m, primaryAfter.EhbSnapshot);
        Assert.Equal(optionalCharacterId, optionalAfter.OsrsCharacterId);
        Assert.Equal(optionalQuestionId, optionalAfter.SignupQuestionId);
        Assert.Equal(optionalEhb, optionalAfter.EhbSnapshot);
        Assert.Equal(optionalCharacterId, optionalAnswerAfter.OsrsCharacterId);
        Assert.Equal(primaryId, await verify.DraftPublicationRosters
            .Where(x => x.DraftPublicationCycleId == verify.DraftPublicationCycles.Where(c => c.DraftSessionId == verify.DraftSessions.Where(d => d.EventId == seed.EventId).Select(d => d.Id).Single() && c.SupersededAt == null).Select(c => c.Id).Single() && x.EventParticipantId == seed.WaitingId)
            .Join(verify.OsrsCharacters, x => x.PublicCharacterName, x => x.DisplayName, (_, character) => character.Id)
            .SingleAsync());
    }

    [Fact]
    public async Task FinalizedRosterReuseCreatesMissingPrimaryAndUpdatesExistingAnswerWithoutChangingOptionalPlayingAssignment()
    {
        var seed = await SeedAsync();
        Guid replacementId;
        Guid oldPrimaryId;
        Guid optionalAssignmentId;
        Guid optionalCharacterId;
        Guid optionalQuestionId;
        decimal optionalEhb;
        Guid optionalAnswerId;
        await using (var db = Db())
        {
            var participant = await db.EventParticipants.SingleAsync(x => x.Id == seed.WaitingId);
            var assignments = await db.EventParticipantCharacters
                .Where(x => x.EventParticipantId == seed.WaitingId && x.ReleasedAt == null && x.EventRole == EventCharacterRole.Playing)
                .OrderBy(x => x.RegistrationOrder)
                .ToListAsync();
            var primary = assignments.Single(x => x.SignupQuestionId == seed.PrimaryQuestionId);
            var optional = assignments.Single(x => x.SignupQuestionId != seed.PrimaryQuestionId);
            var optionalAnswer = await db.SignupAnswers.SingleAsync(x => x.EventParticipantId == seed.WaitingId && x.SignupQuestionId == optional.SignupQuestionId);
            oldPrimaryId = primary.OsrsCharacterId;
            primary.Release(participant.AccountId!.Value, clock.Now.AddMinutes(-1));
            var replacement = new OsrsCharacter(Guid.NewGuid(), "Replacement primary", "REPLACEMENT PRIMARY", clock.Now);
            db.AddRange(replacement, new AccountOsrsCharacter(Guid.NewGuid(), participant.AccountId.Value, replacement.Id, participant.AccountId.Value, false, 2, null, 73m, clock.Now));
            optionalAssignmentId = optional.Id;
            optionalCharacterId = optional.OsrsCharacterId;
            optionalQuestionId = optional.SignupQuestionId!.Value;
            optionalEhb = optional.EhbSnapshot!.Value;
            optionalAnswerId = optionalAnswer.Id;
            replacementId = replacement.Id;
            await db.SaveChangesAsync();
        }

        long teamVersion;
        await using (var db = Db()) teamVersion = await db.Teams.Where(x => x.Id == seed.TeamId).Select(x => x.Version).SingleAsync();
        FinalizedRosterMutationResult added;
        await using (var db = Db())
            added = await Service(db).AddFinalizedRosterParticipantAsync(new(
                seed.EventId, seed.TeamId, seed.AdminId, "admin", ParticipantId: seed.WaitingId,
                ExpectedTeamVersion: teamVersion, PlayingCharacterId: replacementId, PlayingEhb: 86.5m));
        Assert.True(added.Succeeded, added.Error);

        await using var verify = Db();
        var primaryAfter = await verify.EventParticipantCharacters.SingleAsync(x => x.EventParticipantId == seed.WaitingId && x.ReleasedAt == null && x.SignupQuestionId == seed.PrimaryQuestionId);
        var primaryAnswer = await verify.SignupAnswers.SingleAsync(x => x.EventParticipantId == seed.WaitingId && x.SignupQuestionId == seed.PrimaryQuestionId);
        var optionalAfter = await verify.EventParticipantCharacters.SingleAsync(x => x.Id == optionalAssignmentId);
        var optionalAnswerAfter = await verify.SignupAnswers.SingleAsync(x => x.Id == optionalAnswerId);
        Assert.Equal(replacementId, primaryAfter.OsrsCharacterId);
        Assert.Equal(86.5m, primaryAfter.EhbSnapshot);
        Assert.Equal(replacementId, primaryAnswer.OsrsCharacterId);
        Assert.Equal(optionalCharacterId, optionalAfter.OsrsCharacterId);
        Assert.Equal(optionalQuestionId, optionalAfter.SignupQuestionId);
        Assert.Equal(optionalEhb, optionalAfter.EhbSnapshot);
        Assert.Equal(optionalCharacterId, optionalAnswerAfter.OsrsCharacterId);
        Assert.NotEqual(oldPrimaryId, primaryAnswer.OsrsCharacterId);
        Assert.Equal(replacementId, await verify.DraftPublicationRosters
            .Where(x => x.DraftPublicationCycleId == verify.DraftPublicationCycles.Where(c => c.DraftSessionId == verify.DraftSessions.Where(d => d.EventId == seed.EventId).Select(d => d.Id).Single() && c.SupersededAt == null).Select(c => c.Id).Single() && x.EventParticipantId == seed.WaitingId)
            .Join(verify.OsrsCharacters, x => x.PublicCharacterName, x => x.DisplayName, (_, character) => character.Id)
            .SingleAsync());
    }

    [Fact]
    public async Task FinalizedRosterReuseRejectsDuplicatePrimaryAssignmentsWithoutWrites()
    {
        var seed = await SeedAsync();
        Guid primaryId;
        await using (var db = Db())
        {
            var participant = await db.EventParticipants.SingleAsync(x => x.Id == seed.OtherWaitingId);
            var primary = await db.EventParticipantCharacters.SingleAsync(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null && x.SignupQuestionId == seed.PrimaryQuestionId);
            var duplicateCharacter = new OsrsCharacter(Guid.NewGuid(), "Duplicate primary", "DUPLICATE PRIMARY", clock.Now);
            db.AddRange(duplicateCharacter, new EventParticipantCharacter(Guid.NewGuid(), seed.EventId, participant.Id, duplicateCharacter.Id, primary.RegistrationOrder + 1,
                clock.Now, participant.AccountId, seed.PrimaryQuestionId, EventCharacterRole.Playing, 31m, EhbSource.Manual, null));
            await db.SaveChangesAsync();
            primaryId = primary.OsrsCharacterId;
        }

        long teamVersion;
        await using (var db = Db()) teamVersion = await db.Teams.Where(x => x.Id == seed.TeamId).Select(x => x.Version).SingleAsync();
        var before = await StateHashAsync();
        await using (var omitted = Db())
        {
            var rejected = await Service(omitted).AddFinalizedRosterParticipantAsync(new(
                seed.EventId, seed.TeamId, seed.AdminId, "admin", ParticipantId: seed.OtherWaitingId, ExpectedTeamVersion: teamVersion));
            Assert.False(rejected.Succeeded);
            Assert.Contains("multiple active primary", rejected.Error, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(before, await StateHashAsync());
        await using (var explicitSelection = Db())
        {
            var rejected = await Service(explicitSelection).AddFinalizedRosterParticipantAsync(new(
                seed.EventId, seed.TeamId, seed.AdminId, "admin", ParticipantId: seed.OtherWaitingId,
                ExpectedTeamVersion: teamVersion, PlayingCharacterId: primaryId, PlayingEhb: 77.5m));
            Assert.False(rejected.Succeeded);
            Assert.Contains("multiple active primary", rejected.Error, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(before, await StateHashAsync());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    public async Task FinalizedRosterAddRejectsMissingOrMalformedPrimaryDefinitionWithoutWrites(string condition)
    {
        var seed = await SeedAsync();
        await using (var db = Db())
        {
            var primary = await db.SignupQuestions.SingleAsync(x => x.Id == seed.PrimaryQuestionId);
            if (condition == "missing")
            {
                db.Entry(primary).Property(x => x.Active).CurrentValue = false;
                db.Entry(primary).Property(x => x.DisabledAt).CurrentValue = clock.Now;
                db.Entry(primary).Property(x => x.DisabledReason).CurrentValue = "malformed fixture";
            }
            else
            {
                db.Entry(primary).Property(x => x.Type).CurrentValue = SignupQuestionType.Text;
                db.Entry(primary).Property(x => x.AccountAnswerRole).CurrentValue = null;
            }
            await db.SaveChangesAsync();
        }
        long teamVersion;
        await using (var db = Db()) teamVersion = await db.Teams.Where(x => x.Id == seed.TeamId).Select(x => x.Version).SingleAsync();
        var before = await StateHashAsync();
        await using var attempt = Db();
        var rejected = await Service(attempt).AddFinalizedRosterParticipantAsync(new(
            seed.EventId, seed.TeamId, seed.AdminId, "admin", ParticipantId: seed.OtherWaitingId, ExpectedTeamVersion: teamVersion));
        Assert.False(rejected.Succeeded);
        Assert.Contains("active required Playing account", rejected.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await StateHashAsync());
    }

    [Fact]
    public async Task FinalizedRosterAddRejectsUnresolvableLegacyPlayingAssignmentWithoutWrites()
    {
        var seed = await SeedAsync();
        await using (var db = Db())
        {
            var participant = await db.EventParticipants.SingleAsync(x => x.Id == seed.OtherWaitingId);
            var legacyCharacter = new OsrsCharacter(Guid.NewGuid(), "Legacy unresolved", "LEGACY UNRESOLVED", clock.Now);
            db.AddRange(legacyCharacter, new EventParticipantCharacter(Guid.NewGuid(), seed.EventId, participant.Id, legacyCharacter.Id, 1,
                clock.Now, participant.AccountId, null, EventCharacterRole.Playing, 19m, EhbSource.Manual, null));
            await db.SaveChangesAsync();
        }
        long teamVersion;
        await using (var db = Db()) teamVersion = await db.Teams.Where(x => x.Id == seed.TeamId).Select(x => x.Version).SingleAsync();
        var before = await StateHashAsync();
        await using var attempt = Db();
        var rejected = await Service(attempt).AddFinalizedRosterParticipantAsync(new(
            seed.EventId, seed.TeamId, seed.AdminId, "admin", ParticipantId: seed.OtherWaitingId, ExpectedTeamVersion: teamVersion));
        Assert.False(rejected.Succeeded);
        Assert.Contains("unresolved active account assignment", rejected.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await StateHashAsync());
    }

    [Fact]
    public async Task FinalizedRosterRemoveRequiresFreshMembershipVersionRetainsHistoryAndSupportsIndependentReAdd()
    {
        var seed = await SeedAsync();
        string answersBefore;
        string assignmentsBefore;
        int picksBefore;
        await using (var snapshot = Db())
        {
            answersBefore = await RowsAsync("signup_answers", $"event_participant_id = '{seed.DepartedId}'");
            assignmentsBefore = await RowsAsync("event_participant_characters", $"event_participant_id = '{seed.DepartedId}'");
            picksBefore = await snapshot.DraftPicks.CountAsync(x => x.DraftSessionId == snapshot.DraftSessions.Where(d => d.EventId == seed.EventId).Select(d => d.Id).Single());
        }
        long version;
        await using (var db = Db()) version = await db.TeamMemberships.Where(x => x.Id == seed.DepartedMembershipId).Select(x => x.Version).SingleAsync();
        await using (var missing = Db())
        {
            var rejected = await Service(missing).RemoveFinalizedRosterParticipantAsync(new(seed.EventId, seed.DepartedId, seed.AdminId, "admin", true));
            Assert.False(rejected.Succeeded);
            Assert.Contains("version is required", rejected.Error, StringComparison.OrdinalIgnoreCase);
        }
        await using (var changed = Db())
        {
            var member = await changed.TeamMemberships.SingleAsync(x => x.Id == seed.DepartedMembershipId);
            member.AdvanceVersion();
            await changed.SaveChangesAsync();
        }
        await using (var stale = Db())
        {
            var rejected = await Service(stale).RemoveFinalizedRosterParticipantAsync(new(seed.EventId, seed.DepartedId, seed.AdminId, "admin", true, version));
            Assert.False(rejected.Succeeded);
            Assert.Contains("changed elsewhere", rejected.Error, StringComparison.OrdinalIgnoreCase);
        }

        long currentVersion;
        await using (var db = Db()) currentVersion = await db.TeamMemberships.Where(x => x.Id == seed.DepartedMembershipId).Select(x => x.Version).SingleAsync();
        await using (var remove = Db())
        {
            var result = await Service(remove, new CompetitionManagementStub(new(true, Status: "Failed", Error: "provider unavailable"))).RemoveFinalizedRosterParticipantAsync(new(seed.EventId, seed.DepartedId, seed.AdminId, "admin", true, currentVersion));
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal("Failed", result.WomSyncStatus);
            Assert.Equal("provider unavailable", result.WomSyncError);
        }
        await using (var verify = Db())
        {
            var member = await verify.TeamMemberships.SingleAsync(x => x.Id == seed.DepartedMembershipId);
            Assert.NotNull(member.LeftAt);
            Assert.Equal(SignupStatus.Confirmed, await verify.EventParticipants.Where(x => x.Id == seed.DepartedId).Select(x => x.SignupStatus).SingleAsync());
            Assert.Equal(answersBefore, await RowsAsync("signup_answers", $"event_participant_id = '{seed.DepartedId}'"));
            Assert.Equal(assignmentsBefore, await RowsAsync("event_participant_characters", $"event_participant_id = '{seed.DepartedId}'"));
            Assert.Equal(picksBefore, await verify.DraftPicks.CountAsync(x => x.DraftSessionId == verify.DraftSessions.Where(d => d.EventId == seed.EventId).Select(d => d.Id).Single()));
            var activeCycle = await verify.DraftPublicationCycles.Where(x => x.DraftSessionId == verify.DraftSessions.Where(d => d.EventId == seed.EventId).Select(d => d.Id).Single() && x.SupersededAt == null).Select(x => x.Id).SingleAsync();
            Assert.DoesNotContain(await verify.DraftPublicationRosters.Where(x => x.DraftPublicationCycleId == activeCycle).ToListAsync(), x => x.EventParticipantId == seed.DepartedId);
        }
        await using var publicFactory = Factory();
        using var publicClient = publicFactory.CreateClient();
        var currentPublic = await publicClient.GetStringAsync(TeamsPath(seed));
        Assert.DoesNotContain("Departed C", RosterSection(currentPublic));
        Assert.Contains("Departed C", PickSection(currentPublic));

        long secondTeamVersion;
        await using (var db = Db()) secondTeamVersion = await db.Teams.Where(x => x.Id == seed.SecondTeamId).Select(x => x.Version).SingleAsync();
        await using (var changed = Db())
        {
            var team = await changed.Teams.SingleAsync(x => x.Id == seed.SecondTeamId);
            team.AdvanceVersion();
            await changed.SaveChangesAsync();
        }
        await using (var failedAdd = Db())
        {
            var rejected = await Service(failedAdd).AddFinalizedRosterParticipantAsync(new(seed.EventId, seed.SecondTeamId, seed.AdminId, "admin", ParticipantId: seed.DepartedId, ExpectedTeamVersion: secondTeamVersion));
            Assert.False(rejected.Succeeded);
            Assert.Contains("changed elsewhere", rejected.Error, StringComparison.OrdinalIgnoreCase);
        }
        await using (var db = Db()) secondTeamVersion = await db.Teams.Where(x => x.Id == seed.SecondTeamId).Select(x => x.Version).SingleAsync();
        await using (var add = Db())
        {
            var result = await Service(add).AddFinalizedRosterParticipantAsync(new(seed.EventId, seed.SecondTeamId, seed.AdminId, "admin", ParticipantId: seed.DepartedId, ExpectedTeamVersion: secondTeamVersion));
            Assert.True(result.Succeeded, result.Error);
        }
        await using var final = Db();
        Assert.Equal(seed.SecondTeamId, await final.TeamMemberships.Where(x => x.EventParticipantId == seed.DepartedId && x.LeftAt == null).Select(x => x.TeamId).SingleAsync());
        Assert.Equal(assignmentsBefore, await RowsAsync("event_participant_characters", $"event_participant_id = '{seed.DepartedId}'"));
        Assert.Contains("reused-participant", (await final.AuditEntries.Where(x => x.Action == "roster.finalized_added").OrderBy(x => x.OccurredAt).ToListAsync()).Last().Details);
        var readdedPublic = await publicClient.GetStringAsync(TeamsPath(seed));
        Assert.Contains("Departed C", RosterSection(readdedPublic));
    }

    [Fact]
    public async Task FinalizedWomProjectionUsesEveryCurrentPlayingAssignmentWithinPublishedMembershipBoundary()
    {
        var seed = await SeedAsync();
        await using var factory = Factory();
        using var scope = factory.Services.CreateScope();
        var management = scope.ServiceProvider.GetRequiredService<IEventCompetitionManagementService>();
        var preview = await management.PreviewAsync(seed.EventId);
        Assert.NotNull(preview);
        var firstTeam = Assert.Single(preview!.Teams, x => x.TeamId == seed.TeamId);
        Assert.Contains("Departed C", firstTeam.Participants);
        Assert.Contains("Departed B", firstTeam.Participants);
        await using var db = Db();
        Assert.Single(await db.DraftPublicationRosters.Where(x => x.DraftPublicationCycleId == seed.InitialCycleId && x.EventParticipantId == seed.DepartedId).ToListAsync());
    }

    [Fact]
    public async Task FinalizedRosterCorrectionsRejectAfterTheEventFirstGoesLive()
    {
        var seed = await SeedAsync();
        await using (var startedDb = Db())
        {
            var item = await startedDb.Events.SingleAsync(x => x.Id == seed.EventId);
            item.StartEvent(clock.Now);
            await startedDb.SaveChangesAsync();
        }
        await using var liveDb = Db();
        var teamVersion = await liveDb.Teams.Where(x => x.Id == seed.TeamId).Select(x => x.Version).SingleAsync();
        var add = await Service(liveDb).AddFinalizedRosterParticipantAsync(new(seed.EventId, seed.TeamId, seed.AdminId, "admin", ParticipantId: seed.DepartedId, ExpectedTeamVersion: teamVersion));
        Assert.False(add.Succeeded);
        var membershipVersion = await liveDb.TeamMemberships.Where(x => x.Id == seed.DepartedMembershipId).Select(x => x.Version).SingleAsync();
        var remove = await Service(liveDb).RemoveFinalizedRosterParticipantAsync(new(seed.EventId, seed.DepartedId, seed.AdminId, "admin", true, membershipVersion));
        Assert.False(remove.Succeeded);
        Assert.Null((await liveDb.TeamMemberships.SingleAsync(x => x.Id == seed.DepartedMembershipId)).LeftAt);
    }

    [Fact]
    public async Task FinalizedRosterAddLinearizesAgainstAConcurrentLiveStart()
    {
        var seed = await SeedAsync();
        await using (var db = Db())
        {
            var owner = await db.Accounts.SingleAsync(x => x.Id == seed.InternalOwnerId);
            var character = new OsrsCharacter(Guid.NewGuid(), "Live race add", "LIVE RACE ADD", clock.Now);
            db.AddRange(character, new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, character.Id, owner.Id, true, 0, null, 20m, clock.Now));
            await db.SaveChangesAsync();
        }
        long teamVersion;
        await using (var db = Db()) teamVersion = await db.Teams.Where(x => x.Id == seed.TeamId).Select(x => x.Version).SingleAsync();
        var barrier = new BeforeEventLock();
        await using var mutationDb = Db(barrier);
        var mutation = Service(mutationDb).AddFinalizedRosterParticipantAsync(new(seed.EventId, seed.TeamId, seed.AdminId, "admin", seed.InternalOwnerId, ExpectedTeamVersion: teamVersion));
        await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var start = StartLiveAsync(seed.EventId);
        barrier.Release.TrySetResult();
        var add = await mutation;
        await start;
        Assert.True(add.Succeeded, add.Error);
        await using var verify = Db();
        Assert.Equal(EventState.Live, await verify.Events.Where(x => x.Id == seed.EventId).Select(x => x.State).SingleAsync());
        Assert.NotNull(await verify.TeamMemberships.Where(x => x.Id == add.MembershipId && x.LeftAt == null).SingleOrDefaultAsync());
    }

    [Fact]
    public async Task PendingAddAndFailedRemoveKeepLocalRosterCommittedAndReturnProviderState()
    {
        var seed = await SeedAsync();
        await using (var db = Db())
        {
            var owner = await db.Accounts.SingleAsync(x => x.Id == seed.InternalOwnerId);
            var character = new OsrsCharacter(Guid.NewGuid(), "Pending add", "PENDING ADD", clock.Now);
            db.AddRange(character, new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, character.Id, owner.Id, true, 0, null, 20m, clock.Now));
            await db.SaveChangesAsync();
        }
        long teamVersion;
        await using (var db = Db()) teamVersion = await db.Teams.Where(x => x.Id == seed.TeamId).Select(x => x.Version).SingleAsync();
        FinalizedRosterMutationResult add;
        await using (var db = Db()) add = await Service(db, new CompetitionManagementStub(new(true, Status: "Pending", Error: "queued for worker"))).AddFinalizedRosterParticipantAsync(new(seed.EventId, seed.TeamId, seed.AdminId, "admin", seed.InternalOwnerId, ExpectedTeamVersion: teamVersion));
        Assert.True(add.Succeeded, add.Error);
        Assert.Equal("Pending", add.WomSyncStatus);
        Assert.NotNull(add.MembershipId);
        long membershipVersion;
        await using (var db = Db()) membershipVersion = await db.TeamMemberships.Where(x => x.Id == add.MembershipId).Select(x => x.Version).SingleAsync();
        await using (var db = Db())
        {
            var remove = await Service(db, new CompetitionManagementStub(new(false, Status: "Failed", Error: "provider failed"))).RemoveFinalizedRosterParticipantAsync(new(seed.EventId, add.ParticipantId!.Value, seed.AdminId, "admin", true, membershipVersion));
            Assert.True(remove.Succeeded, remove.Error);
            Assert.Equal("Failed", remove.WomSyncStatus);
            Assert.Equal("provider failed", remove.WomSyncError);
        }
        await using var verify = Db();
        Assert.NotNull(await verify.TeamMemberships.Where(x => x.Id == add.MembershipId).Select(x => x.LeftAt).SingleAsync());
        Assert.Contains(await verify.AuditEntries.Where(x => x.EventId == seed.EventId && x.Action.EndsWith(".wom_sync")).ToListAsync(), x => x.Details?.Contains("provider failed", StringComparison.Ordinal) == true);
    }

    private async Task StartLiveAsync(Guid eventId)
    {
        await using var db = Db();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var item = await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} AND hidden_at IS NULL FOR UPDATE").SingleAsync();
        item.StartEvent(clock.Now);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task EndLiveForReviewAsync(Guid eventId)
    {
        await using var db = Db();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var item = await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} AND hidden_at IS NULL FOR UPDATE").SingleAsync();
        item.EndEvent(clock.Now.AddMinutes(-5));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private WebApplicationFactory<Program> Factory(IInterceptor? interceptor = null, bool unavailablePlayerLookup = false) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
            services.RemoveAll<IWiseOldManPlayerLookup>();
            services.AddSingleton<IWiseOldManPlayerLookup>(unavailablePlayerLookup ? new UnavailableWiseOldManPlayerLookup() : new SuccessfulWiseOldManPlayerLookup());
            // Exercise lifecycle through its real HTTP action without a background scheduler racing the deterministic fixture clock.
            services.RemoveAll<IHostedService>();
            if (interceptor is not null) services.AddDbContext<ApplicationDbContext>(configuration => configuration.AddInterceptors(interceptor));
        }));

    private async Task<Seed> SeedAsync()
    {
        await using var db = Db();
        var now = clock.Now;
        Account Owner(string name, GlobalRole role = GlobalRole.User)
        {
            var account = Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), now.AddDays(-3));
            account.SetGlobalRole(role);
            account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "password"), false, now, false);
            db.Accounts.Add(account); return account;
        }
        var admin = Owner("c11-admin", GlobalRole.Admin); var super = Owner("c11-super", GlobalRole.SuperAdmin);
        var departedOwner = Owner("c11-departed"); var leaderOwner = Owner("c11-leader"); var otherCaptain = Owner("c11-other-captain");
        var waitingOwner = Owner("c11-waiting"); var otherWaitingOwner = Owner("c11-other-waiting"); var internalOwner = Owner("c11-internal"); var outsider = Owner("c11-outsider");
        var item = new BingoEvent(Guid.NewGuid(), "C11 fixture event", "c11-fixture", "UTC", admin.Id, now.AddDays(-3), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        item.ConfigureInitialSchedule(now.AddDays(-2), now.AddDays(-1), now.AddDays(-1), now.AddHours(1), now.AddHours(5), 3);
        item.ConfigureSignup(true, false, null); item.OpenSignups(now.AddDays(-2)); item.CloseSignups(now.AddDays(-1)); item.SetDraftLocked(true); item.SetDraftRosterPublication(true);
        var form = new SignupForm(Guid.NewGuid(), item.Id, now.AddDays(-3)); form.Publish(now.AddDays(-2)); form.Close(now.AddDays(-1));
        var primary = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary", "Primary", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var secondary = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "secondary", "Secondary", SignupQuestionType.Account, false, 1, null, SignupSystemField.None, EventCharacterRole.Playing);
        var answer = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "private", "Private answer", SignupQuestionType.Text, true, 2, null);
        var team = new Team(Guid.NewGuid(), item.Id, "C11 team one", "c11-one", TeamFormationType.Drafted, null, true, now.AddDays(-1));
        var second = new Team(Guid.NewGuid(), item.Id, "C11 team two", "c11-two", TeamFormationType.Drafted, null, true, now.AddDays(-1));
        team.SetDraftPosition(1); second.SetDraftPosition(2); team.Finalize(now.AddHours(-1)); second.Finalize(now.AddHours(-1));
        var draft = new DraftSession(Guid.NewGuid(), item.Id, 2); draft.Start(now.AddHours(-2)); draft.RecordFirstPick(now.AddMinutes(-90)); draft.Finalize(now.AddHours(-1));
        db.AddRange(item, form, primary, secondary, answer, team, second, draft);
        long sequence = 0;
        (EventParticipant Participant, OsrsCharacter Primary) Participant(Account owner, string name, SignupStatus status, bool corrected = false)
        {
            var participant = new EventParticipant(Guid.NewGuid(), item.Id, status, ++sequence, now.AddDays(-2).AddMinutes(sequence), SignupSource.Website);
            participant.AssignOwner(owner); db.EventParticipants.Add(participant);
            var character = new OsrsCharacter(Guid.NewGuid(), name, name.ToUpperInvariant(), now.AddDays(-2));
            db.OsrsCharacters.Add(character);
            db.AccountOsrsCharacters.Add(new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, character.Id, owner.Id, true, 0, null, 25m, now));
            if (corrected)
            {
                var old = new OsrsCharacter(Guid.NewGuid(), name.Replace(" C", " A"), name.Replace(" C", " A").ToUpperInvariant(), now.AddDays(-2));
                var oldAssignment = new EventParticipantCharacter(Guid.NewGuid(), item.Id, participant.Id, old.Id, 0, now.AddDays(-2), owner.Id, primary.Id, EventCharacterRole.Playing, 10, EhbSource.Manual, null);
                oldAssignment.Release(owner.Id, now.AddHours(-4));
                var secondCharacter = new OsrsCharacter(Guid.NewGuid(), name.Replace(" C", " B"), name.Replace(" C", " B").ToUpperInvariant(), now.AddDays(-2));
                db.AddRange(old, oldAssignment, secondCharacter,
                    new EventParticipantCharacter(Guid.NewGuid(), item.Id, participant.Id, secondCharacter.Id, 1, now.AddDays(-2), owner.Id, secondary.Id, EventCharacterRole.Playing, 15, EhbSource.Manual, null),
                    new SignupAnswer(Guid.NewGuid(), participant.Id, secondary.Id, secondary.Label, "", secondCharacter.Id));
            }
            db.AddRange(new EventParticipantCharacter(Guid.NewGuid(), item.Id, participant.Id, character.Id, corrected ? 2 : 0, now.AddHours(-4), owner.Id, primary.Id, EventCharacterRole.Playing, 25, EhbSource.Manual, null),
                new SignupAnswer(Guid.NewGuid(), participant.Id, primary.Id, primary.Label, "", character.Id),
                new SignupAnswer(Guid.NewGuid(), participant.Id, answer.Id, answer.Label, "fixture confidential answer"));
            return (participant, character);
        }
        var departed = Participant(departedOwner, "Departed C", SignupStatus.Confirmed, true); departed.Participant.SetAdminNotes("Existing private note");
        var leader = Participant(leaderOwner, "Leader", SignupStatus.Confirmed);
        var secondCaptain = Participant(otherCaptain, "Second Captain", SignupStatus.Confirmed);
        var otherWaiting = Participant(otherWaitingOwner, "First waiting", SignupStatus.WaitingList);
        var waiting = Participant(waitingOwner, "Waiting C", SignupStatus.WaitingList, true);
        var pick1 = new DraftPick(Guid.NewGuid(), draft.Id, second.Id, secondCaptain.Participant.Id, 1, 1, now.AddMinutes(-90));
        var pick2 = new DraftPick(Guid.NewGuid(), draft.Id, team.Id, departed.Participant.Id, 2, 1, now.AddMinutes(-89));
        var departedMember = new TeamMembership(Guid.NewGuid(), team.Id, departed.Participant.Id, TeamMembershipRole.Captain, now.AddMinutes(-89), pick2.Id, "Fixture pick");
        departedMember.SetSource(TeamMembershipSource.DraftPick);
        var leaderMember = new TeamMembership(Guid.NewGuid(), team.Id, leader.Participant.Id, TeamMembershipRole.CoCaptain, now.AddHours(-2), null, "Fixture preassignment");
        var secondMember = new TeamMembership(Guid.NewGuid(), second.Id, secondCaptain.Participant.Id, TeamMembershipRole.Captain, now.AddMinutes(-90), pick1.Id, "Fixture pick");
        secondMember.SetSource(TeamMembershipSource.DraftPick);
        var cycle = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, now.AddHours(-1), admin.Id);
        db.AddRange(pick1, pick2, departedMember, leaderMember, secondMember, cycle,
            new DraftPublicationRoster(Guid.NewGuid(), cycle.Id, team.Id, departed.Participant.Id, TeamMembershipRole.Captain, 2, "Departed C"),
            new DraftPublicationRoster(Guid.NewGuid(), cycle.Id, team.Id, leader.Participant.Id, TeamMembershipRole.CoCaptain, null, "Leader"),
            new DraftPublicationRoster(Guid.NewGuid(), cycle.Id, second.Id, secondCaptain.Participant.Id, TeamMembershipRole.Captain, 1, "Second Captain"));
        await db.SaveChangesAsync();
        return new(item.Id, item.Slug, admin.Id, departedOwner.Id, leaderOwner.Id, waitingOwner.Id, internalOwner.Id, outsider.Id, team.Id, second.Id, departed.Participant.Id, leader.Participant.Id,
            waiting.Participant.Id, otherWaiting.Participant.Id, departedMember.Id, primary.Id, answer.Id, waiting.Primary.Id, cycle.Id);
    }

    private async Task PublishBoardAsync(Seed seed)
    {
        await using var db = Db();
        var board = new Board(Guid.NewGuid(), seed.EventId, "C11 board", 1, 1);
        db.Boards.Add(board); await db.SaveChangesAsync();
        var approval = new BoardApprovalSnapshot(Guid.NewGuid(), board.Id, 1, clock.Now.AddHours(-1), seed.AdminId, null, board.Name, 1, 1, 0, 1, board.Version);
        db.BoardApprovalSnapshots.Add(approval); await db.SaveChangesAsync();
        board.Approve(approval.Id); board.Publish(clock.Now.AddHours(-1));
        (await db.Events.SingleAsync(x => x.Id == seed.EventId)).SetBoardPublication(true, clock.Now);
        await db.SaveChangesAsync();
    }

    private async Task WithdrawAsync(Seed seed, string? note = null)
    {
        await using var db = Db();
        var version = await db.TeamMemberships.Where(x => x.Id == seed.DepartedMembershipId).Select(x => x.Version).SingleAsync();
        var result = await Service(db).WithdrawAsync(seed.EventId, seed.DepartedId, seed.AdminId, "admin", true, note, version);
        Assert.True(result.Succeeded, result.Error);
    }
    private async Task<LiveParticipantResult> FillAsync(Seed seed, Guid candidate)
    {
        await using var db = Db();
        return await Service(db).ReplaceVacancyAsync(new(seed.EventId, seed.DepartedMembershipId, seed.AdminId, "admin", candidate));
    }
    private async Task<FinalizedRosterMutationResult> AddFinalizedAsync(Seed seed, Guid teamId, Guid? participantId = null, Guid? websiteAccountId = null, IEventCompetitionManagementService? competitionManagement = null, TeamMembershipRole role = TeamMembershipRole.Participant)
    {
        await using var db = Db();
        var teamVersion = await db.Teams.Where(x => x.Id == teamId).Select(x => x.Version).SingleAsync();
        return await Service(db, competitionManagement).AddFinalizedRosterParticipantAsync(new(seed.EventId, teamId, seed.AdminId, "c11-admin", websiteAccountId, participantId, role, ExpectedTeamVersion: teamVersion));
    }
    private async Task<FinalizedRosterMutationResult> RemoveFinalizedAsync(Seed seed, Guid participantId)
    {
        await using var db = Db();
        var membershipVersion = await db.TeamMemberships.Where(x => x.EventParticipantId == participantId && x.LeftAt == null).Select(x => x.Version).SingleAsync();
        return await Service(db).RemoveFinalizedRosterParticipantAsync(new(seed.EventId, participantId, seed.AdminId, "c11-admin", true, membershipVersion));
    }
    private static AdminParticipantChangeRequest Internal(Seed seed) => new(seed.EventId, null, seed.AdminId, "admin", null,
        new Dictionary<Guid, AdminAccountAnswer> { [seed.PrimaryQuestionId] = new("Internal fixture", 35) }, new Dictionary<Guid, string> { [seed.AnswerQuestionId] = "fixture answer" });
    private async Task<Guid> CurrentCycleAsync() { await using var db = Db(); return await db.DraftPublicationCycles.Where(x => x.SupersededAt == null).Select(x => x.Id).SingleAsync(); }
    private async Task AssertOldRosterAsync(Seed seed, string original) => Assert.Equal(original, await RowsAsync("draft_publication_rosters", $"draft_publication_cycle_id = '{seed.InitialCycleId}'"));
    private async Task<string> StateHashAsync()
    {
        var tables = new[] { "events", "event_participants", "event_participant_characters", "osrs_characters", "signup_answers", "team_memberships", "team_membership_role_transitions", "draft_sessions", "draft_picks", "draft_publication_cycles", "draft_publication_rosters", "audit_entries", "personal_notifications", "waiting_list_promotion_follow_ups", "event_participant_character_swaps" };
        var state = new StringBuilder();
        foreach (var table in tables) state.Append(await RowsAsync(table, table == "audit_entries" ? "event_id IS NOT NULL" : "true"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state.ToString())));
    }
    private async Task<string> RowsAsync(string table, string where = "true")
    {
        await using var connection = new NpgsqlConnection(database.GetConnectionString()); await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT COALESCE(jsonb_agg(to_jsonb(row) ORDER BY to_jsonb(row)::text), '[]'::jsonb)::text FROM {table} row WHERE {where}", connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }
    private async Task AssertPreStartSubmissionDeniedAsync(Seed seed, Guid participant, Guid owner)
    {
        await using var db = Db(); var storage = new UnusedStorage();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => new SubmissionService(db, storage, clock).CreateAsync(
            new(owner, seed.EventId, seed.TeamId, Guid.NewGuid(), Guid.NewGuid(), null, participant, 1, null, "fixture.png", new MemoryStream([1]))));
        Assert.Contains("not currently open", exception.Message);
        Assert.Equal(0, storage.Calls);
        Assert.Empty(await db.Submissions.ToListAsync());
    }
    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> factory, string username)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var page = await client.GetStringAsync("/Account/Login");
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Input.Username"] = username, ["Input.Password"] = "password", ["__RequestVerificationToken"] = Token(page) }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); return client;
    }
    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string handler, string page, Dictionary<string, string> values)
    { values["__RequestVerificationToken"] = Token(page); return client.PostAsync(path + "?handler=" + handler, new FormUrlEncodedContent(values)); }
    private static string Input(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html, $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private static string Token(string page) { var token = Input(page, "__RequestVerificationToken"); Assert.NotEmpty(token); return token; }
    private static string ParticipantPath(Seed seed, Guid participant) => $"/Admin/Events/Participant/{seed.EventId}/Participants/{participant}";
    private static string TeamsPath(Seed seed) => $"/Events/{seed.Slug}/Teams";
    private static string RosterSection(string page) => page.Split("data-public-ui-team-roster")[1].Split("data-public-ui-draft-results")[0];
    private static string PickSection(string page) => page.Split("data-public-ui-draft-results")[1];
    private sealed class UnavailableWiseOldManPlayerLookup : IWiseOldManPlayerLookup
    {
        public Task<WiseOldManPlayerLookupResult> LookupPlayerAsync(string characterName, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WiseOldManPlayerLookupResult(WiseOldManLookupStatus.Unavailable));
    }
    private static async Task<string> FollowNoticeAsync(HttpClient client, Guid notice, string target)
    {
        Assert.Contains(notice.ToString(), await client.GetStringAsync("/Notifications"));
        using var response = await client.GetAsync($"/Notifications?read={notice}");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); Assert.Equal(target, response.Headers.Location?.OriginalString);
        using var destination = await client.GetAsync(target); Assert.Equal(HttpStatusCode.OK, destination.StatusCode);
        return await destination.Content.ReadAsStringAsync();
    }
    private sealed record Seed(Guid EventId, string Slug, Guid AdminId, Guid DepartedOwnerId, Guid LeaderOwnerId, Guid WaitingOwnerId, Guid InternalOwnerId, Guid OutsiderId,
        Guid TeamId, Guid SecondTeamId, Guid DepartedId, Guid LeaderId, Guid WaitingId, Guid OtherWaitingId, Guid DepartedMembershipId, Guid PrimaryQuestionId, Guid AnswerQuestionId, Guid WaitingPrimaryId, Guid InitialCycleId);
    private sealed class CompetitionManagementStub(EventCompetitionManagementResult result) : IEventCompetitionManagementService
    {
        public Task<EventCompetitionManagementPreview?> PreviewAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.FromResult<EventCompetitionManagementPreview?>(null);
        public Task<EventCompetitionManagementView?> GetAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.FromResult<EventCompetitionManagementView?>(null);
        public Task<EventCompetitionManagementResult> CreateAsync(Guid eventId, long expectedEventVersion, LifecycleActor actor, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<EventCompetitionManagementResult> AdoptCredentialAsync(Guid eventId, long expectedEventVersion, string verificationCode, LifecycleActor actor, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<EventCompetitionManagementResult> ReplaceCredentialAsync(Guid eventId, long expectedEventVersion, string verificationCode, LifecycleActor actor, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<EventCompetitionManagementResult> QueueUpdateAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<EventCompetitionManagementResult> DeleteAsync(Guid eventId, long expectedEventVersion, long targetCompetitionId, bool confirmed, LifecycleActor actor, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task ProcessDueAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class MutableClock(DateTimeOffset now) : TimeProvider { public DateTimeOffset Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class UnusedStorage : IEvidenceStorage
    {
        public int Calls { get; private set; }
        public Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default) { Calls++; throw new InvalidOperationException("Storage must not run before actual start."); }
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class BeforeEventLock : DbCommandInterceptor
    {
        private int entered;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("SELECT * FROM events WHERE id", StringComparison.Ordinal) && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal) && Interlocked.CompareExchange(ref entered, 1, 0) == 0)
            {
                Reached.TrySetResult(); await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class SaveFailure(string target) : SaveChangesInterceptor
    {
        public bool Triggered { get; private set; }
        public bool Enabled { get; set; } = true;
        private bool failAfterSave;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var db = eventData.Context!;
            var hit = target switch
            {
                "publication" => db.ChangeTracker.Entries<DraftPublicationCycle>().Any(x => x.State == EntityState.Added),
                "audit" => db.ChangeTracker.Entries<AuditEntry>().Any(x => x.State == EntityState.Added && x.Entity.Action is "roster.finalized_removed" or "roster.finalized_added"),
                "notification" => db.ChangeTracker.Entries<DraftPublicationRoster>().Any(x => x.State == EntityState.Added),
                "role-audit" => db.ChangeTracker.Entries<AuditEntry>().Any(x => x.State == EntityState.Added && x.Entity.Action == "team.role_roster_published"),
                _ => false
            };
            if (hit && Enabled)
            {
                if (target == "publication") { Triggered = true; throw new InvalidOperationException("Controlled C11 publication failure after earlier flush"); }
                failAfterSave = true;
            }
            return ValueTask.FromResult(result);
        }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (failAfterSave) { Triggered = true; failAfterSave = false; throw new InvalidOperationException("Controlled C11 failure after persisted audit/notification flush"); }
            return ValueTask.FromResult(result);
        }
    }
}
