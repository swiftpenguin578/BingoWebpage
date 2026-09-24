using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private SignupService Service(ApplicationDbContext db) => new(db, new SecretHasher(), clock, accountValidation: new SuccessfulWiseOldManAccountValidation());

    [Fact]
    public async Task HttpDepartureWaitingFillPreservesPublicationPicksNotesReservationsAndNotificationAccess()
    {
        var seed = await SeedAsync();
        await using var factory = Factory();
        using var admin = await LoginAsync(factory, "c11-admin");
        using var owner = await LoginAsync(factory, "c11-departed");
        using var leader = await LoginAsync(factory, "c11-leader");
        using var replacement = await LoginAsync(factory, "c11-waiting");
        using var publicClient = factory.CreateClient();
        var rosterBefore = await RowsAsync("draft_publication_rosters");
        var picksBefore = await RowsAsync("draft_picks");
        var assignmentsBefore = await RowsAsync("event_participant_characters");
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
        Assert.Contains("(vacancy)", vacancy);
        Assert.Contains("Fill open vacancy", vacancy);
        Assert.Contains("Existing private note&#xA;C11 private departure detail", vacancy);
        var search = await admin.GetStringAsync($"/Admin/Events/Participants/{seed.EventId}?ParticipantStatus=Withdrawn&ParticipantSearch=Departed%20C");
        Assert.Contains(route, search);
        Assert.Contains("Departed C", search);
        Assert.Contains("Departed C", await admin.GetStringAsync($"/Admin/Events/Draft/{seed.EventId}"));
        Assert.Equal(assignmentsBefore, await RowsAsync("event_participant_characters"));
        Assert.Equal(picksBefore, await RowsAsync("draft_picks"));
        await AssertOldRosterAsync(seed, rosterBefore);
        var published = await publicClient.GetStringAsync(TeamsPath(seed));
        Assert.DoesNotContain("Departed C", RosterSection(published));
        Assert.Contains("Departed C", PickSection(published));
        Assert.DoesNotContain("private departure detail", published);
        Assert.DoesNotContain("Existing private note", published);

        await using (var db = Db())
        {
            Assert.Equal(clock.Now, (await db.EventParticipants.SingleAsync(x => x.Id == seed.DepartedId)).WithdrawnAt);
            Assert.Equal(clock.Now, (await db.TeamMemberships.SingleAsync(x => x.Id == seed.DepartedMembershipId)).LeftAt);
            Assert.Equal(SignupStatus.WaitingList, (await db.EventParticipants.SingleAsync(x => x.Id == seed.WaitingId)).SignupStatus);
            Assert.Empty(await db.EventParticipantCharacterSwaps.ToListAsync());
            var own = Assert.Single(await db.PersonalNotifications.Where(x => x.RecipientAccountId == seed.DepartedOwnerId && x.Title == "participant.withdrawn").ToListAsync());
            Assert.Equal(TeamsPath(seed), own.Route);
            Assert.Empty(await db.PersonalNotifications.Where(x => x.RecipientAccountId == seed.DepartedOwnerId && x.Title == "participant.prelive_withdrawn").ToListAsync());
            await FollowNoticeAsync(owner, own.Id, TeamsPath(seed));
            var leadership = Assert.Single(await db.PersonalNotifications.Where(x => x.RecipientAccountId == seed.LeaderOwnerId && x.Title == "participant.prelive_withdrawn").ToListAsync());
            await FollowNoticeAsync(leader, leadership.Id, TeamsPath(seed));
            var action = Assert.Single(await db.PersonalNotifications.Where(x => x.RecipientAccountId == seed.AdminId && x.Title == "participant.prelive_withdrawn").ToListAsync());
            await FollowNoticeAsync(admin, action.Id, route);
            Assert.All(await db.PersonalNotifications.ToListAsync(), notice => Assert.DoesNotContain("private", notice.Detail, StringComparison.OrdinalIgnoreCase));
            await Assert.ThrowsAsync<InvalidOperationException>(() => new EvidenceAuthority(db).ResolveActorAsync(seed.DepartedOwnerId, seed.EventId, seed.TeamId, clock.Now));
        }
        using (var denied = await owner.GetAsync(route)) Assert.NotEqual(HttpStatusCode.OK, denied.StatusCode);
        using (var denied = await owner.GetAsync(TeamsPath(seed) + $"?participantId={seed.DepartedId}"))
        {
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.Contains("/Account/AccessDenied", denied.Headers.Location!.OriginalString);
        }
        var inbox = await admin.GetStringAsync("/Notifications");
        Assert.Contains("Open vacancy", inbox);
        Assert.Contains(route, inbox);
        // Explicitly select the later of two waiting people; no automatic first-in-line promotion.
        Assert.True(vacancy.IndexOf(seed.OtherWaitingId.ToString(), StringComparison.Ordinal) < vacancy.IndexOf(seed.WaitingId.ToString(), StringComparison.Ordinal));
        using (var response = await PostAsync(admin, route, "FillVacancy", vacancy, new()
        {
            ["VacancyMembershipId"] = Input(vacancy, "VacancyMembershipId"),
            ["VacancyMembershipVersion"] = Input(vacancy, "VacancyMembershipVersion"),
            ["ReplacementWaitingParticipantId"] = seed.WaitingId.ToString()
        })) Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        published = await publicClient.GetStringAsync(TeamsPath(seed));
        Assert.Contains("Waiting C", RosterSection(published));
        Assert.DoesNotContain("Waiting C", PickSection(published));
        Assert.Contains("Departed C", PickSection(published));
        await AssertOldRosterAsync(seed, rosterBefore);
        Assert.Equal(picksBefore, await RowsAsync("draft_picks"));
        Assert.Equal(assignmentsBefore, await RowsAsync("event_participant_characters"));
        Guid followUpId;
        await using (var db = Db())
        {
            var member = await db.TeamMemberships.SingleAsync(x => x.EventParticipantId == seed.WaitingId && x.LeftAt == null);
            Assert.Equal(seed.DepartedMembershipId, member.ReplacesMembershipId);
            Assert.Null(member.AssignedByDraftPickId);
            Assert.Equal(TeamMembershipRole.Participant, member.Role);
            Assert.Equal(clock.Now, member.JoinedAt);
            Assert.Empty(await db.EventParticipantCharacterSwaps.ToListAsync());
            Assert.Equal(SignupStatus.WaitingList, (await db.EventParticipants.SingleAsync(x => x.Id == seed.OtherWaitingId)).SignupStatus);
            followUpId = (await db.WaitingListPromotionFollowUps.SingleAsync()).Id;
            var notice = Assert.Single(await db.PersonalNotifications.Where(x => x.RecipientAccountId == seed.WaitingOwnerId && x.Title == "participant.prelive_replaced").ToListAsync());
            await FollowNoticeAsync(replacement, notice.Id, TeamsPath(seed));
        }
        var followPath = ParticipantPath(seed, seed.WaitingId);
        Assert.Contains(followPath, await admin.GetStringAsync("/Notifications"));
        var followPage = await admin.GetStringAsync(followPath);
        Assert.Contains("Mark follow-up complete", followPage);
        using (var done = await PostAsync(admin, followPath, "CompletePromotionFollowUp", followPage, new() { ["followUpId"] = followUpId.ToString() }))
            Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        await using var final = Db();
        Assert.NotNull((await final.WaitingListPromotionFollowUps.SingleAsync()).CompletedAt);
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
        if (fillVacancy) Assert.True((await FillAsync(seed, seed.WaitingId)).Succeeded);
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
    public async Task HttpInternalReplacementValidatesReservationsAndCreatesNoPickOrPreStartActivation(bool linked)
    {
        var seed = await SeedAsync();
        await WithdrawAsync(seed);
        await using var factory = Factory();
        using var admin = await LoginAsync(factory, "c11-admin");
        var path = ParticipantPath(seed, seed.DepartedId);
        var page = await admin.GetStringAsync(path);
        var form = new Dictionary<string, string>
        {
            ["VacancyMembershipId"] = Input(page, "VacancyMembershipId"),
            ["VacancyMembershipVersion"] = Input(page, "VacancyMembershipVersion"),
            [$"InternalReplacement.AccountAnswers[{seed.PrimaryQuestionId}].CharacterName"] = "Departed C",
            [$"InternalReplacement.AccountAnswers[{seed.PrimaryQuestionId}].Ehb"] = "42",
            [$"InternalReplacement.Answers[{seed.AnswerQuestionId}]"] = "internal private answer",
            ["InternalReplacement.OwnerUsername"] = linked ? "c11-internal" : ""
        };
        var unchanged = await StateHashAsync();
        using (var rejected = await PostAsync(admin, path, "FillVacancy", page, form)) Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        Assert.Equal(unchanged, await StateHashAsync());
        Assert.Contains("reserved", await admin.GetStringAsync(path));
        form[$"InternalReplacement.AccountAnswers[{seed.PrimaryQuestionId}].CharacterName"] = "Internal replacement";
        form[$"InternalReplacement.AccountAnswers[{seed.PrimaryQuestionId}].Ehb"] = "-1";
        using (var rejected = await PostAsync(admin, path, "FillVacancy", page, form)) Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        Assert.Equal(unchanged, await StateHashAsync());
        form[$"InternalReplacement.AccountAnswers[{seed.PrimaryQuestionId}].Ehb"] = "42";
        using (var added = await PostAsync(admin, path, "FillVacancy", page, form)) Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        await using var db = Db();
        var member = await db.TeamMemberships.SingleAsync(x => x.ReplacesMembershipId == seed.DepartedMembershipId);
        var participant = await db.EventParticipants.SingleAsync(x => x.Id == member.EventParticipantId);
        Assert.Equal(SignupSource.AdminCreated, participant.Source);
        Assert.Equal(linked ? seed.InternalOwnerId : (Guid?)null, participant.AccountId);
        Assert.Null(member.AssignedByDraftPickId);
        Assert.Equal(TeamMembershipRole.Participant, member.Role);
        Assert.Empty(await db.EventParticipantCharacterSwaps.ToListAsync());
        Assert.Equal(3, await db.DraftPublicationCycles.CountAsync());
        Assert.Contains("Internal replacement", RosterSection(await admin.GetStringAsync(TeamsPath(seed))));
        Assert.DoesNotContain("internal private answer", await admin.GetStringAsync(TeamsPath(seed)));
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
        var form = new Dictionary<string, string>
        {
            ["VacancyMembershipId"] = Input(page, "VacancyMembershipId"),
            ["VacancyMembershipVersion"] = Input(page, "VacancyMembershipVersion"),
            [$"InternalReplacement.AccountAnswers[{seed.PrimaryQuestionId}].CharacterName"] = "Internal outage replacement",
            [$"InternalReplacement.AccountAnswers[{seed.PrimaryQuestionId}].Ehb"] = "42",
            [$"InternalReplacement.Answers[{seed.AnswerQuestionId}]"] = "retained replacement answer"
        };
        var unchanged = await StateHashAsync();

        using var response = await PostAsync(admin, path, "FillVacancy", page, form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("data-wom-validation-confirmation", html);
        Assert.Contains("Internal outage replacement", html);
        Assert.Contains("Wise Old Man is unavailable", html);
        Assert.Equal(form[$"InternalReplacement.AccountAnswers[{seed.PrimaryQuestionId}].CharacterName"], Input(html, $"InternalReplacement.AccountAnswers[{seed.PrimaryQuestionId}].CharacterName"));
        Assert.Equal(form[$"InternalReplacement.AccountAnswers[{seed.PrimaryQuestionId}].Ehb"], Input(html, $"InternalReplacement.AccountAnswers[{seed.PrimaryQuestionId}].Ehb"));
        Assert.Equal(form[$"InternalReplacement.Answers[{seed.AnswerQuestionId}]"], Input(html, $"InternalReplacement.Answers[{seed.AnswerQuestionId}]"));
        Assert.Equal(form["VacancyMembershipId"], Input(html, "VacancyMembershipId"));
        Assert.Equal(form["VacancyMembershipVersion"], Input(html, "VacancyMembershipVersion"));
        var confirmationToken = Input(html, "InternalReplacement.WomValidationConfirmationToken");
        Assert.NotEmpty(confirmationToken);
        var cancelButton = Regex.Match(html, "<button(?=[^>]*data-wom-validation-cancel)[^>]*>").Value;
        Assert.Contains("type=\"submit\"", cancelButton, StringComparison.Ordinal);
        Assert.Contains("formaction=\"", cancelButton, StringComparison.Ordinal);
        Assert.Contains("handler=CancelWomValidation", cancelButton, StringComparison.Ordinal);

        var cancelFields = new Dictionary<string, string>(form)
        {
            ["overlay"] = "false",
            ["WomValidationConfirmationToken"] = Input(html, "WomValidationConfirmationToken"),
            ["InternalReplacement.WomValidationConfirmationToken"] = confirmationToken
        };
        using var cancelled = await PostAsync(admin, path, "CancelWomValidation", html, cancelFields);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var cancelledHtml = WebUtility.HtmlDecode(await cancelled.Content.ReadAsStringAsync());
        Assert.DoesNotContain("data-wom-validation-confirmation", cancelledHtml, StringComparison.Ordinal);
        Assert.Equal(form[$"InternalReplacement.AccountAnswers[{seed.PrimaryQuestionId}].CharacterName"], Input(cancelledHtml, $"InternalReplacement.AccountAnswers[{seed.PrimaryQuestionId}].CharacterName"));
        Assert.Equal(form[$"InternalReplacement.AccountAnswers[{seed.PrimaryQuestionId}].Ehb"], Input(cancelledHtml, $"InternalReplacement.AccountAnswers[{seed.PrimaryQuestionId}].Ehb"));
        Assert.Equal(form[$"InternalReplacement.Answers[{seed.AnswerQuestionId}]"], Input(cancelledHtml, $"InternalReplacement.Answers[{seed.AnswerQuestionId}]"));
        Assert.Empty(Input(cancelledHtml, "WomValidationConfirmationToken"));
        Assert.Empty(Input(cancelledHtml, "InternalReplacement.WomValidationConfirmationToken"));
        Assert.Equal(unchanged, await StateHashAsync());
        Assert.Equal(unchanged, await StateHashAsync());
    }

    [Fact]
    public async Task ConcurrentFillsHaveOnlyOneWinner()
    {
        var seed = await SeedAsync();
        await WithdrawAsync(seed);
        var results = await Task.WhenAll(FillAsync(seed, seed.WaitingId), FillAsync(seed, seed.OtherWaitingId));
        Assert.Single(results, x => x.Succeeded);
        Assert.Single(results, x => !x.Succeeded);
        await using (var db = Db())
        {
            Assert.Single(await db.TeamMemberships.Where(x => x.ReplacesMembershipId == seed.DepartedMembershipId).ToListAsync());
            Assert.Single(await db.DraftPublicationCycles.Where(x => x.SupersededAt == null).ToListAsync());
            Assert.Equal(3, await db.DraftPublicationCycles.CountAsync());
        }
        var snapshot = await StateHashAsync();
        Assert.False((await FillAsync(seed, seed.WaitingId)).Succeeded);
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
        if (action != "withdraw") await WithdrawAsync(seed);
        var before = await StateHashAsync();
        var failure = new SaveFailure(fault);
        await using (var db = Db(failure))
        {
            var result = action == "withdraw"
                ? (await Service(db).WithdrawAsync(seed.EventId, seed.DepartedId, seed.AdminId, "c11-admin", true, "must roll back", 1)).Succeeded
                : (await Service(db).ReplaceVacancyAsync(action == "waiting" ? new(seed.EventId, seed.DepartedMembershipId, seed.AdminId, "admin", seed.WaitingId)
                    : new(seed.EventId, seed.DepartedMembershipId, seed.AdminId, "admin", InternalParticipant: Internal(seed)))).Succeeded;
            Assert.False(result);
        }
        Assert.True(failure.Triggered);
        Assert.Equal(before, await StateHashAsync());
        if (action == "withdraw") await WithdrawAsync(seed, "retry note");
        else if (action == "waiting") Assert.True((await FillAsync(seed, seed.WaitingId)).Succeeded);
        else
        {
            await using var db = Db();
            var retried = await Service(db).ReplaceVacancyAsync(new(seed.EventId, seed.DepartedMembershipId, seed.AdminId, "admin", InternalParticipant: Internal(seed)));
            Assert.True(retried.Succeeded, retried.Error);
        }
    }

    [Fact]
    public async Task HttpNoteLengthFailurePreservesInputAndNotesThenBlankAndReplayPreserveStoredValue()
    {
        var seed = await SeedAsync();
        await using var factory = Factory();
        using var admin = await LoginAsync(factory, "c11-admin");
        var path = ParticipantPath(seed, seed.DepartedId);
        var page = await admin.GetStringAsync(path);
        var attempt = new string('x', 2000);
        var before = await StateHashAsync();
        using (var rejected = await PostAsync(admin, path, "Withdraw", page, new()
        {
            ["ConfirmLifecycleAction"] = "true",
            ["ExpectedMembershipVersion"] = Input(page, "ExpectedMembershipVersion"),
            ["PrivateWithdrawalNote"] = attempt
        }))
        {
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            var html = await rejected.Content.ReadAsStringAsync();
            Assert.Contains("must total 2,000", html);
            Assert.Contains(attempt, html);
        }
        Assert.Equal(before, await StateHashAsync());
        await WithdrawAsync(seed, " \n ");
        var after = await StateHashAsync();
        await using (var db = Db())
        {
            var replay = await Service(db).WithdrawAsync(seed.EventId, seed.DepartedId, seed.AdminId, "admin", true, "must not append", 1);
            Assert.True(replay.Succeeded); Assert.False(replay.Changed);
            Assert.Equal("Existing private note", (await db.EventParticipants.SingleAsync(x => x.Id == seed.DepartedId)).AdminNotes);
        }
        Assert.Equal(after, await StateHashAsync());
    }

    [Fact]
    public async Task NoteEditOrderingUsesLatestLockedValueAndRejectsStaleOverwrite()
    {
        var seed = await SeedAsync();
        await using var stale = Db();
        await stale.EventParticipants.SingleAsync(x => x.Id == seed.DepartedId);
        await using (var editor = Db())
            Assert.True((await Service(editor).SetAdminNotesAsync(seed.EventId, seed.DepartedId, seed.AdminId, "admin", "Fresh note", "Existing private note")).Succeeded);
        Assert.True((await Service(stale).WithdrawAsync(seed.EventId, seed.DepartedId, seed.AdminId, "admin", true, "Departure", 1)).Succeeded);
        await using var verify = Db();
        Assert.Equal("Fresh note\nDeparture", (await verify.EventParticipants.SingleAsync(x => x.Id == seed.DepartedId)).AdminNotes);
        Assert.False((await Service(verify).SetAdminNotesAsync(seed.EventId, seed.DepartedId, seed.AdminId, "admin", "stale overwrite", "Fresh note")).Succeeded);
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
            Assert.Equal(seed.EventId, audit.EventId);
        }
        const string actionRow = "<code class=\"admin-audit-action\">team.role_roster_published</code>";
        Assert.Contains(actionRow, await admin.GetStringAsync($"/Admin/Audit?EventId={seed.EventId}"));
        await using (var db = Db())
        {
            var item = await db.Events.SingleAsync(x => x.Id == seed.EventId);
            item.StartEvent(clock.Now.AddHours(-2));
            item.EndEvent(clock.Now.AddHours(-1));
            item.Hide(seed.AdminId, clock.Now, item.Name, "Controlled audit visibility check");
            await db.SaveChangesAsync();
        }
        Assert.DoesNotContain(actionRow, await admin.GetStringAsync("/Admin/Audit?Action=team.role_roster_published"));
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
        await WithdrawAsync(seed);
        await using (var db = Db())
        {
            var notices = await db.PersonalNotifications.Where(x => x.RecipientAccountId == seed.DepartedOwnerId).ToListAsync();
            if (adminOwner)
            {
                Assert.Single(notices, x => x.Title == "participant.withdrawn" && x.Route == TeamsPath(seed));
                Assert.Single(notices, x => x.Title == "participant.prelive_withdrawn" && x.Route == ParticipantPath(seed, seed.DepartedId));
            }
            else Assert.Empty(notices);
        }
        List<Guid> remaining;
        await using (var db = Db()) remaining = await db.TeamMemberships.Where(x => x.LeftAt == null).Select(x => x.EventParticipantId).ToListAsync();
        foreach (var participant in remaining)
        {
            await using var db = Db();
            var result = await Service(db).WithdrawAsync(seed.EventId, participant, seed.AdminId, "admin", true);
            Assert.True(result.Succeeded, result.Error);
        }
        await using var factory = Factory(); using var client = factory.CreateClient();
        var page = await client.GetStringAsync(TeamsPath(seed));
        Assert.Contains("C11 team one", page); Assert.Contains("C11 team two", page);
        Assert.Equal(2, Regex.Count(RosterSection(page), "Open vacancy"));
        Assert.Contains("Departed C", PickSection(page)); Assert.Contains("Second Captain", PickSection(page));
        await using var final = Db();
        var cycle = await final.DraftPublicationCycles.SingleAsync(x => x.SupersededAt == null);
        Assert.Empty(await final.DraftPublicationRosters.Where(x => x.DraftPublicationCycleId == cycle.Id).ToListAsync());
        Assert.Equal(2, await final.DraftPicks.CountAsync());
    }

    [Fact]
    public async Task MutableCurrentNameCannotRewritePublishedPickAndOrdinaryReleasedWithdrawalStillHasIdentity()
    {
        var seed = await SeedAsync(); await WithdrawAsync(seed);
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
        var search = await admin.GetStringAsync($"/Admin/Events/Participants/{seed.EventId}?ParticipantStatus=Withdrawn&ParticipantSearch=Current%20renamed");
        Assert.Contains(ParticipantPath(seed, seed.DepartedId), search); Assert.Contains("Current renamed player", search);
        Assert.Contains("First waiting", await admin.GetStringAsync(ParticipantPath(seed, seed.OtherWaitingId)));
        var draft = await admin.GetStringAsync($"/Admin/Events/Draft/{seed.EventId}");
        Assert.Contains("Departed C", draft); Assert.DoesNotContain("Current renamed player", draft);
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
            var item = new BingoEvent(Guid.NewGuid(), "Other C11 event", "other-c11", "UTC", seed.AdminId, clock.Now);
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
        var seed = await SeedAsync(); await WithdrawAsync(seed);
        Guid otherVacancy;
        await using (var db = Db())
        {
            var result = await Service(db).WithdrawAsync(seed.EventId, seed.LeaderId, seed.AdminId, "admin", true);
            Assert.True(result.Succeeded, result.Error);
            otherVacancy = await db.TeamMemberships.Where(x => x.EventParticipantId == seed.LeaderId).Select(x => x.Id).SingleAsync();
        }
        async Task<LiveParticipantResult> Fill(Guid vacancy)
        {
            await using var db = Db();
            return await Service(db).ReplaceVacancyAsync(new(seed.EventId, vacancy, seed.AdminId, "admin", seed.WaitingId));
        }
        var results = await Task.WhenAll(Fill(seed.DepartedMembershipId), Fill(otherVacancy));
        Assert.Single(results, x => x.Succeeded); Assert.Single(results, x => !x.Succeeded);
        await using var final = Db();
        Assert.Single(await final.TeamMemberships.Where(x => x.EventParticipantId == seed.WaitingId && x.LeftAt == null).ToListAsync());
        Assert.Single(await final.WaitingListPromotionFollowUps.ToListAsync());
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
        var addition = new string('x', 2000 - "Existing private note".Length - 1);
        await WithdrawAsync(seed, addition);
        await using var db = Db();
        Assert.Equal(2000, (await db.EventParticipants.SingleAsync(x => x.Id == seed.DepartedId)).AdminNotes!.Length);
        Assert.Null((await db.Events.SingleAsync(x => x.Id == seed.EventId)).ActualStartedAt);
        Assert.True((await FillAsync(seed, seed.WaitingId)).Succeeded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LiveMinuteBoundaryPreservesRequestInstantAcrossEventLockContention(bool replacement)
    {
        var seed = await SeedAsync();
        await using (var db = Db()) { (await db.Events.SingleAsync(x => x.Id == seed.EventId)).StartEvent(clock.Now.AddHours(-1)); await db.SaveChangesAsync(); }
        if (replacement) await WithdrawAsync(seed);
        var requestInstant = clock.Now;
        var barrier = new BeforeEventLock();
        await using var context = Db(barrier);
        var operation = replacement
            ? Service(context).ReplaceVacancyAsync(new(seed.EventId, seed.DepartedMembershipId, seed.AdminId, "admin", seed.WaitingId))
            : Service(context).WithdrawLiveAsync(new(seed.EventId, seed.DepartedId, seed.AdminId, "admin", 1));
        try
        {
            await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(20));
            clock.Now = clock.Now.AddMinutes(2);
            barrier.Release.TrySetResult();
            var result = await operation;
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(new DateTimeOffset(requestInstant.Year, requestInstant.Month, requestInstant.Day, requestInstant.Hour, requestInstant.Minute, 0, TimeSpan.Zero).AddMinutes(1), result.EffectiveAtUtc);
        }
        finally { barrier.Release.TrySetResult(); }
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
        await WithdrawAsync(seed);
        Assert.True((await FillAsync(seed, seed.WaitingId)).Succeeded);
        await using var context = Db();
        var notices = await context.PersonalNotifications.Where(x => x.RecipientAccountId == seed.AdminId).OrderBy(x => x.CreatedAt).ToListAsync();
        Assert.Equal(2, notices.Count);
        var withdrawal = Assert.Single(notices, x => x.Detail.Contains("withdrew", StringComparison.Ordinal));
        var replacement = Assert.Single(notices, x => x.Detail.Contains("joined", StringComparison.Ordinal));
        admin.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
        var html = WebUtility.HtmlDecode(await admin.GetStringAsync("/Notifications"));
        string Title(Guid notice) => Regex.Match(html, $"<a\\b[^>]*href=\"/notifications\\?read={notice}\"[^>]*>\\s*<strong>([^<]*)</strong>").Groups[1].Value;
        Assert.Equal((live, culture) switch
        {
            (true, "da") => "Live-deltager trukket",
            (true, _) => "Live participant withdrawn",
            (false, "da") => "Deltager trukket før eventstart",
            _ => "Participant withdrawn before event start"
        }, Title(withdrawal.Id));
        Assert.Equal((live, culture) switch
        {
            (true, "da") => "Live-erstatning bekræftet",
            (true, _) => "Live replacement confirmed",
            (false, "da") => "Erstatning bekræftet før eventstart",
            _ => "Replacement confirmed before event start"
        }, Title(replacement.Id));
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
        var item = new BingoEvent(Guid.NewGuid(), "C11 fixture event", "c11-fixture", "UTC", admin.Id, now.AddDays(-3));
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
            var character = new OsrsCharacter(Guid.NewGuid(), name, name.ToUpperInvariant(), now.AddDays(-2)); db.OsrsCharacters.Add(character);
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
                "audit" => db.ChangeTracker.Entries<AuditEntry>().Any(x => x.State == EntityState.Added && x.Entity.Action == "participant.admin_withdrawn"),
                "notification" => db.ChangeTracker.Entries<PersonalNotification>().Any(x => x.State == EntityState.Added),
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
