using Bingo.Domain.Access;
using Bingo.Application.Boards;
using Bingo.Application.Evidence;
using Bingo.Domain.Boards;
using Bingo.Domain.Evidence;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Bingo.Web.TestData;

public sealed class DevelopmentScenarioSeeder(
    ApplicationDbContext db,
    IWebHostEnvironment environment,
    IPasswordHasher<Account> passwordHasher,
    IEvidenceStorage evidenceStorage,
    TimeProvider timeProvider)
{
    public const string CaptainPassword = "SeedCaptain!1234";
    public const string SecondaryAdminUsername = "SeedAdminTwo";
    public const string SecondaryAdminPassword = "SeedAdmin!1234";

    public async Task<SeedResult> ResetAndSeedAsync(CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("Test scenario seeding is available only in Development.");
        }

        var admin = await db.Accounts.AsNoTracking()
            .Where(account => account.Role == AccountRole.Admin && account.DisabledAt == null)
            .OrderBy(account => account.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Create a local administrator before resetting test data.");

        var blueprint = await BuildCanonicalBlueprintAsync(cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await ClearWorkflowDataAsync(cancellationToken);

        var current = timeProvider.GetUtcNow();
        var now = new DateTimeOffset(current.Year, current.Month, current.Day, current.Hour, current.Minute < 30 ? 0 : 30, 0, TimeSpan.Zero);
        var secondaryAdmin = await EnsureSecondaryAdminAsync(now, cancellationToken);
        var seeded = new List<SeededScenario>();

        seeded.Add(SeedScenario(
            "TEST 00 — Setup",
            "test-00-private-setup",
            ScenarioStage.PrivateSetup,
            blueprint,
            admin.Id,
            now));
        seeded.Add(SeedScenario(
            "TEST 01 — Signup",
            "test-01-signups-open",
            ScenarioStage.SignupsOpen,
            blueprint,
            admin.Id,
            now));
        seeded.Add(SeedScenario(
            "TEST 02 — Closed",
            "test-02-pre-board",
            ScenarioStage.PreBoard,
            blueprint,
            admin.Id,
            now));
        seeded.Add(SeedScenario(
            "TEST 03 — Board",
            "test-03-board-draft",
            ScenarioStage.BoardDraft,
            blueprint,
            admin.Id,
            now));
        seeded.Add(SeedScenario(
            "TEST 04 — Teams",
            "test-04-draft-setup",
            ScenarioStage.DraftSetup,
            blueprint,
            admin.Id,
            now));
        seeded.Add(SeedScenario(
            "TEST 05 — Draft",
            "test-05-draft-running",
            ScenarioStage.DraftRunning,
            blueprint,
            admin.Id,
            now));
        seeded.Add(SeedScenario(
            "TEST 06 — Ready",
            "test-06-post-draft",
            ScenarioStage.PostDraft,
            blueprint,
            admin.Id,
            now));
        seeded.Add(SeedScenario(
            "TEST 07 — Live",
            "test-07-live",
            ScenarioStage.Live,
            blueprint,
            admin.Id,
            now));
        seeded.Add(SeedScenario(
            "TEST 08 — Review",
            "test-08-final-review",
            ScenarioStage.FinalReview,
            blueprint,
            admin.Id,
            now));
        var reviewScenario = SeedScenario(
            "TEST 09 — Evidence",
            "test-09-submission-review",
            ScenarioStage.ReviewCases,
            blueprint,
            admin.Id,
            now);
        seeded.Add(reviewScenario);
        await AddReviewCasesAsync(reviewScenario.EventId, admin.Id, now, cancellationToken);
        var finalizedScenario = SeedScenario(
            "TEST 10 — Finished",
            "test-10-finalized-results",
            ScenarioStage.Finalized,
            blueprint,
            admin.Id,
            now);
        seeded.Add(finalizedScenario);
        await AddCompletedBoardAsync(finalizedScenario.EventId, admin.Id, now, cancellationToken);
        var completedScenario = SeedScenario(
            "TEST 11 — Complete",
            "test-11-completed-board",
            ScenarioStage.CompletedFinalReview,
            blueprint,
            admin.Id,
            now);
        seeded.Add(completedScenario);
        await AddCompletedBoardAsync(completedScenario.EventId, admin.Id, now, cancellationToken);
        seeded.Add(SeedLargeDraftScenario(blueprint, admin.Id, now));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SeedResult(admin.Username, secondaryAdmin.Username, SecondaryAdminPassword, blueprint.Name, seeded, CaptainPassword);
    }

    private async Task<Account> EnsureSecondaryAdminAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var normalized = SecondaryAdminUsername.ToUpperInvariant();
        var account = await db.Accounts.SingleOrDefaultAsync(value => value.NormalizedUsername == normalized, cancellationToken);
        if (account is null)
        {
            account = new Account(Guid.NewGuid(), SecondaryAdminUsername, normalized, AccountRole.Admin, now);
            db.Accounts.Add(account);
        }
        account.SetPasswordHash(passwordHasher.HashPassword(account, SecondaryAdminPassword), mustChangePassword: false);
        account.Enable(null);
        return account;
    }

    private SeededScenario SeedScenario(
        string name,
        string slug,
        ScenarioStage stage,
        BoardBlueprint blueprint,
        Guid adminId,
        DateTimeOffset now)
    {
        var signupOpens = now.AddDays(-14);
        var signupCloses = stage == ScenarioStage.SignupsOpen ? now.AddDays(7) : now.AddDays(-1);
        var eventStarts = stage switch
        {
            ScenarioStage.Live => now.AddHours(-1),
            ScenarioStage.ReviewCases => now.AddMinutes(-10),
            ScenarioStage.FinalReview or ScenarioStage.Finalized or ScenarioStage.CompletedFinalReview => now.AddDays(-3),
            _ => now.AddDays(7)
        };
        var eventEnds = stage switch
        {
            ScenarioStage.Live or ScenarioStage.ReviewCases => now.AddDays(5),
            ScenarioStage.FinalReview or ScenarioStage.Finalized or ScenarioStage.CompletedFinalReview => now.AddHours(-1),
            _ => now.AddDays(12)
        };

        var bingoEvent = new BingoEvent(
            Guid.NewGuid(), name, slug,
            $"Development seed scenario: {Describe(stage)}",
            "Europe/Copenhagen", signupOpens, signupCloses, eventStarts, eventEnds,
            eventEnds.AddMinutes(30), stage == ScenarioStage.SignupsOpen ? 6 : 20,
            adminId, now);
        bingoEvent.ConfigureSignup(true, true, false, null);
        bingoEvent.ConfigurePlanning(
            "Seeded rules for manual workflow testing.", null, null, 2, 3,
            blueprint.Rows, blueprint.Columns);
        if (stage == ScenarioStage.SignupsOpen) bingoEvent.OpenSignups(now.AddDays(-1));
        else if (stage != ScenarioStage.PrivateSetup) bingoEvent.CloseSignups();
        if (stage is ScenarioStage.Live or ScenarioStage.ReviewCases) bingoEvent.StartEvent(now);
        if (stage is ScenarioStage.FinalReview or ScenarioStage.Finalized or ScenarioStage.CompletedFinalReview)
        {
            bingoEvent.StartEvent(eventStarts);
            bingoEvent.EndEvent();
            if (stage == ScenarioStage.Finalized) bingoEvent.FinalizeResults(now.AddMinutes(-30));
        }
        if (stage == ScenarioStage.ReviewCases)
        {
            bingoEvent.SetEvidenceCodeEnabled(true);
            var retiredCode = new EvidenceCode(
                Guid.NewGuid(), bingoEvent.Id, "OLD-DROP", now.AddDays(-2), adminId, now.AddDays(-2),
                "Previous seeded screenshot code.");
            retiredCode.SetRetiresAt(now.AddHours(-1));
            db.EvidenceCodes.AddRange(
                retiredCode,
                new EvidenceCode(
                    Guid.NewGuid(), bingoEvent.Id, "LIVE-DROP", now.AddHours(-1), adminId, now.AddHours(-1),
                    "Current seeded screenshot code."));
        }
        db.Events.Add(bingoEvent);

        var participants = AddParticipants(bingoEvent.Id, stage, now);
        Board? board = null;
        if (stage >= ScenarioStage.BoardDraft)
        {
            board = AddBoard(bingoEvent.Id, blueprint, stage >= ScenarioStage.DraftSetup, now);
        }

        var captainUsernames = new List<string>();
        if (stage >= ScenarioStage.DraftSetup)
        {
            var finalized = stage >= ScenarioStage.PostDraft;
            var draftState = stage == ScenarioStage.DraftRunning
                ? DraftSeedState.Running
                : finalized ? DraftSeedState.Finalized : DraftSeedState.Setup;
            captainUsernames.AddRange(AddTeamsAndDraft(
                bingoEvent, participants, draftState, finalized, now));
            if (stage == ScenarioStage.Finalized) AddFinalizedResults(bingoEvent, adminId, now);
        }

        return new SeededScenario(bingoEvent.Id, bingoEvent.Name, bingoEvent.State, board?.State, captainUsernames);
    }

    private void AddFinalizedResults(BingoEvent bingoEvent, Guid adminId, DateTimeOffset now)
    {
        var finalization = new EventFinalizationSnapshot(
            Guid.NewGuid(), bingoEvent.Id, 1, now.AddMinutes(-30), adminId);
        db.EventFinalizations.Add(finalization);

        var teams = db.Teams.Local
            .Where(team => team.EventId == bingoEvent.Id && team.Active)
            .OrderBy(team => team.Name == "Seeded Ravens" ? 0 : 1)
            .ThenBy(team => team.Name)
            .ToList();
        for (var index = 0; index < teams.Count; index++)
        {
            var team = teams[index];
            db.OfficialPlacements.Add(new OfficialPlacementSnapshot(
                Guid.NewGuid(), finalization.Id, bingoEvent.Id, team.Id, team.Name,
                index + 1, index == 0, index == 0 ? now.AddHours(-2) : null,
                index == 0 ? 7 : 1, index == 0 ? 12 : Math.Max(1, 8 - index),
                Math.Max(1, 25 - index * 4)));
        }

        foreach (var account in db.Accounts.Local.Where(account =>
                     account.EventId == bingoEvent.Id && account.Role == AccountRole.Captain))
        {
            account.ScheduleExpiry(now.AddHours(24));
        }
    }

    private SeededScenario SeedLargeDraftScenario(
        BoardBlueprint blueprint,
        Guid adminId,
        DateTimeOffset now)
    {
        const int participantCount = 60;
        const int teamCount = 4;
        const int targetTeamSize = 15;

        var rosters = new (string Team, string Captain, string CoCaptain, string[] Picks)[]
        {
            (
                "Touch kids, not grass",
                "Rasmus Zebak",
                "crunch704",
                ["ZemaFios", "Detoned", "Frette", "Thuebob", "Spacecreator", "Raffineret", "I use x22", "Kongherodes", "itsMKN", "Gimgonduth", "Corgisiron", "NoobNicoline", "Zop1"]),
            (
                "Såeh cs?",
                "zakk0",
                "Mikkel-IT",
                ["Thylegend", "Ezzi", "W olles", "Completeius", "Calm Chris", "IM Latry", "im iftic", "Zanshock", "stoltze", "Myrupz", "Maxzen", "Bubber", "Freakingpand"]),
            (
                "Morytania Monkeys",
                "Karl Knast",
                "Macdroppet",
                ["N L C K O", "Siswet19", "Sunny Boy110", "oegget", "3lite men x", "200iq p2W", "Ricebarrage", "Mrtopfresh", "MindMySnipe", "GIM Wemox", "Røllemester", "Maxe2968", "Backshotbaby"]),
            (
                "The Agency",
                "Agent Groth",
                "Agent Slidt",
                ["R33Con", "pappresseren", "Elite ca", "Jern Jakob", "MesterMudder", "Sanddrage", "MrDryhard", "User IM", "Skade", "Tanzania Tim", "Uganda ulrik", "Kenya Kaj", "BotF"])
        };
        var participantNames = rosters
            .SelectMany(roster => new[] { roster.Captain, roster.CoCaptain }.Concat(roster.Picks))
            .ToList();
        var captainNames = rosters
            .Select(roster => roster.Captain)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var eventStarts = now.AddDays(7);
        var eventEnds = now.AddDays(12);
        var bingoEvent = new BingoEvent(
            Guid.NewGuid(),
            "TEST 12 — Large Draft",
            "test-12-large-draft",
            "Large draft setup used to test scrambling and starting a full participant pool.",
            "Europe/Copenhagen",
            now.AddDays(-14),
            now.AddDays(-1),
            eventStarts,
            eventEnds,
            eventEnds.AddMinutes(30),
            participantCount,
            adminId,
            now);
        bingoEvent.ConfigureSignup(true, true, false, null);
        bingoEvent.ConfigurePlanning(
            "Seeded rules for large-draft testing.",
            null,
            null,
            teamCount,
            targetTeamSize,
            blueprint.Rows,
            blueprint.Columns);
        bingoEvent.CloseSignups();
        db.Events.Add(bingoEvent);

        var participants = new List<EventParticipant>(participantCount);
        for (var index = 0; index < participantCount; index++)
        {
            var number = index + 1;
            var name = participantNames[index];
            var ehb = 175 + index * 83;
            var participant = new EventParticipant(
                Guid.NewGuid(),
                bingoEvent.Id,
                name,
                Normalize(name),
                ehb,
                SignupStatus.Confirmed,
                number,
                now.AddMinutes(-participantCount + index),
                SignupSource.Website,
                null);
            participant.UpdatePublicDetails(
                name,
                Normalize(name),
                ehb,
                null,
                $"large-draft-{number:00}",
                null,
                captainNames.Contains(name));
            participants.Add(participant);
        }
        db.EventParticipants.AddRange(participants);
        var participantsByName = participants.ToDictionary(
            participant => participant.PrimaryAccountName,
            StringComparer.OrdinalIgnoreCase);

        var board = AddBoard(bingoEvent.Id, blueprint, publish: true, now);
        var draft = new DraftSession(Guid.NewGuid(), bingoEvent.Id, targetTeamSize);
        db.DraftSessions.Add(draft);

        for (var index = 0; index < teamCount; index++)
        {
            var roster = rosters[index];
            var team = new Team(
                Guid.NewGuid(),
                bingoEvent.Id,
                roster.Team,
                $"large-draft-team-{index + 1}",
                TeamFormationType.Drafted,
                null,
                true);
            db.Teams.Add(team);
            db.TeamMemberships.Add(new TeamMembership(
                Guid.NewGuid(),
                team.Id,
                participantsByName[roster.Captain].Id,
                TeamMembershipRole.Captain,
                now.AddMinutes(-10),
                null,
                "Seeded captain for large draft"));
            db.TeamMemberships.Add(new TeamMembership(
                Guid.NewGuid(),
                team.Id,
                participantsByName[roster.CoCaptain].Id,
                TeamMembershipRole.CoCaptain,
                now.AddMinutes(-10),
                null,
                "Seeded co-captain for large draft"));
        }

        return new SeededScenario(
            bingoEvent.Id,
            bingoEvent.Name,
            bingoEvent.State,
            board.State,
            []);
    }

    private List<EventParticipant> AddParticipants(Guid eventId, ScenarioStage stage, DateTimeOffset now)
    {
        if (stage == ScenarioStage.PrivateSetup) return [];

        var prefix = ((int)stage).ToString("00", CultureInfo.InvariantCulture);
        var participants = new List<EventParticipant>();
        for (var index = 0; index < 9; index++)
        {
            var waiting = stage == ScenarioStage.SignupsOpen && index >= 6;
            var name = index switch
            {
                0 => $"{prefix} Captain Alpha",
                1 => $"{prefix} Captain Bravo",
                _ => $"{prefix} Player {index + 1:00}"
            };
            var participant = new EventParticipant(
                Guid.NewGuid(), eventId, name, Normalize(name), 250 + index * 275,
                waiting ? SignupStatus.WaitingList : SignupStatus.Confirmed,
                index + 1, now.AddMinutes(-90 + index), SignupSource.Website, null);
            participant.UpdatePublicDetails(
                name, Normalize(name), 250 + index * 275,
                index % 3 == 0 ? $"{name} Alt" : null,
                $"seed-user-{prefix}-{index + 1}",
                index == 5 ? "Seeded participant with a scheduling comment." : null,
                index < 2);
            participants.Add(participant);
        }
        db.EventParticipants.AddRange(participants);
        return participants;
    }

    private Board AddBoard(Guid eventId, BoardBlueprint blueprint, bool publish, DateTimeOffset now)
    {
        var board = new Board(Guid.NewGuid(), eventId, "Edge-case test board", blueprint.Rows, blueprint.Columns);
        db.Boards.Add(board);
        decimal total = 0;
        foreach (var tileData in blueprint.Tiles)
        {
            var template = new TileTemplate(
                Guid.NewGuid(), tileData.Name, tileData.Description,
                tileData.Requirements.All(requirement => requirement.Manual)
                    ? ObjectiveType.Manual
                    : ObjectiveType.DropRequirements,
                tileData.EvidenceInstructions,
                tileData.Requirements.Any(requirement => requirement.Manual) ? tileData.Ehb : null);
            var tile = new BoardTile(
                Guid.NewGuid(), board.Id, template.Id, tileData.Row, tileData.Column,
                tileData.Name, tileData.Description, tileData.EvidenceInstructions, tileData.Ehb);
            db.TileTemplates.Add(template);
            db.BoardTiles.Add(tile);
            total += tileData.Ehb;

            foreach (var requirementData in tileData.Requirements)
            {
                var templateRequirement = new TileTemplateRequirement(
                    Guid.NewGuid(), template.Id, requirementData.Position, requirementData.Target,
                    requirementData.Duplicates, requirementData.HigherWeights,
                    requirementData.Description, requirementData.Manual);
                var snapshot = new BoardRequirementSnapshot(
                    Guid.NewGuid(), tile.Id, requirementData.Position, requirementData.Target,
                    requirementData.Duplicates, requirementData.HigherWeights,
                    requirementData.Description, requirementData.Manual);
                db.TileTemplateRequirements.Add(templateRequirement);
                db.BoardRequirementSnapshots.Add(snapshot);
                foreach (var boss in requirementData.Bosses)
                {
                    db.TemplateRequirementBosses.Add(new TemplateRequirementBoss(Guid.NewGuid(), templateRequirement.Id, boss.Id));
                    db.BoardRequirementBossSnapshots.Add(new BoardRequirementBossSnapshot(
                        Guid.NewGuid(), snapshot.Id, boss.Id, boss.Name, boss.Rate));
                }
                foreach (var drop in requirementData.Drops)
                {
                    db.TemplateRequirementDrops.Add(new TemplateRequirementDrop(
                        Guid.NewGuid(), templateRequirement.Id, drop.Id, drop.Maximum));
                    db.BoardRequirementDropSnapshots.Add(new BoardRequirementDropSnapshot(
                        Guid.NewGuid(), snapshot.Id, drop.Id, drop.Boss, drop.Item,
                        drop.DisplayRate, drop.Probability, drop.Maximum, drop.Ehb));
                }
            }
        }
        board.SetTotalEhb(total);
        if (publish) board.Publish(now);
        return board;
    }

    private string[] AddTeamsAndDraft(
        BingoEvent bingoEvent,
        List<EventParticipant> participants,
        DraftSeedState state,
        bool finalized,
        DateTimeOffset now)
    {
        var alpha = new Team(Guid.NewGuid(), bingoEvent.Id, "Seeded Ravens", "seeded-ravens", TeamFormationType.Drafted, null, true);
        var bravo = new Team(Guid.NewGuid(), bingoEvent.Id, "Seeded Wolves", "seeded-wolves", TeamFormationType.Drafted, null, true);
        if (state is DraftSeedState.Running or DraftSeedState.Finalized)
        {
            alpha.SetDraftPosition(1);
            bravo.SetDraftPosition(2);
        }
        db.Teams.AddRange(alpha, bravo);

        var session = new DraftSession(Guid.NewGuid(), bingoEvent.Id, 3);
        db.DraftSessions.Add(session);
        if (state is DraftSeedState.Running or DraftSeedState.Finalized) session.Start(now.AddHours(-2));
        bingoEvent.SetDraftLocked(state is DraftSeedState.Running or DraftSeedState.Finalized);

        db.TeamMemberships.Add(new TeamMembership(
            Guid.NewGuid(), alpha.Id, participants[0].Id, TeamMembershipRole.Captain,
            now.AddHours(-3), null, "Seeded captain"));
        db.TeamMemberships.Add(new TeamMembership(
            Guid.NewGuid(), bravo.Id, participants[1].Id, TeamMembershipRole.Captain,
            now.AddHours(-3), null, "Seeded captain"));

        if (state is DraftSeedState.Running or DraftSeedState.Finalized)
        {
            AddPick(session, alpha, participants[2], 1, 1, now);
            AddPick(session, bravo, participants[3], 2, 1, now);
        }
        if (state == DraftSeedState.Finalized)
        {
            AddPick(session, bravo, participants[4], 3, 2, now);
            AddPick(session, alpha, participants[5], 4, 2, now);
            session.Finalize(now.AddHours(-1));
            alpha.Finalize(now.AddHours(-1));
            bravo.Finalize(now.AddHours(-1));

            var externalCaptain = new EventParticipant(
                Guid.NewGuid(), bingoEvent.Id, "External Clan Captain", "external clan captain", 1,
                SignupStatus.Confirmed, 100, now.AddDays(-7), SignupSource.AdminCreated, null);
            var externalMember = new EventParticipant(
                Guid.NewGuid(), bingoEvent.Id, "External Clan Member", "external clan member", 1,
                SignupStatus.Confirmed, 101, now.AddDays(-7), SignupSource.AdminCreated, null);
            var external = new Team(
                Guid.NewGuid(), bingoEvent.Id, "External Clan Team", "external-clan-team",
                TeamFormationType.Preformed, "External test clan", false);
            external.Finalize(now.AddHours(-1));
            db.EventParticipants.AddRange(externalCaptain, externalMember);
            db.Teams.Add(external);
            db.TeamMemberships.AddRange(
                new TeamMembership(Guid.NewGuid(), external.Id, externalCaptain.Id, TeamMembershipRole.Captain, now, null, "Externally drafted roster"),
                new TeamMembership(Guid.NewGuid(), external.Id, externalMember.Id, TeamMembershipRole.Participant, now, null, "Externally drafted roster"));

            var usernames = new[]
            {
                AddCaptainAccount(bingoEvent, alpha, participants[0], CaptainDigits(bingoEvent, 1), now),
                AddCaptainAccount(bingoEvent, bravo, participants[1], CaptainDigits(bingoEvent, 2), now),
                AddCaptainAccount(bingoEvent, external, externalCaptain, CaptainDigits(bingoEvent, 3), now)
            };
            return usernames;
        }

        return [];
    }

    private void AddPick(DraftSession session, Team team, EventParticipant participant, int pickNumber, int round, DateTimeOffset now)
    {
        var pick = new DraftPick(Guid.NewGuid(), session.Id, team.Id, participant.Id, pickNumber, round, now.AddMinutes(-30 + pickNumber));
        db.DraftPicks.Add(pick);
        db.TeamMemberships.Add(new TeamMembership(
            Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant,
            pick.PickedAt, pick.Id, "Seeded snake-draft pick"));
    }

    private string AddCaptainAccount(
        BingoEvent bingoEvent,
        Team team,
        EventParticipant participant,
        string digits,
        DateTimeOffset now)
    {
        var baseName = new string(participant.PrimaryAccountName.Where(char.IsLetterOrDigit).ToArray());
        var username = $"{baseName}{digits}";
        var account = new Account(Guid.NewGuid(), username, Normalize(username), AccountRole.Captain, now);
        account.SetPasswordHash(passwordHasher.HashPassword(account, CaptainPassword), mustChangePassword: false);
        account.ScopeCaptain(
            bingoEvent.Id, team.Id, bingoEvent.EventStartsAt,
            bingoEvent.SubmissionCutoffAt, bingoEvent.EventEndsAt.AddHours(24), participant.Id);
        db.Accounts.Add(account);
        return username;
    }

    private static string CaptainDigits(BingoEvent bingoEvent, int accountNumber)
    {
        var scenarioDigits = new string(bingoEvent.Slug.Where(char.IsDigit).Take(2).ToArray());
        return $"{scenarioDigits.PadLeft(2, '0')}{accountNumber:00}";
    }

    private async Task AddReviewCasesAsync(Guid eventId, Guid adminId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var board = db.Boards.Local.Single(value => value.EventId == eventId);
        var tiles = db.BoardTiles.Local.Where(value => value.BoardId == board.Id).ToDictionary(value => value.NameSnapshot);
        var team = db.Teams.Local.Single(value => value.EventId == eventId && value.Name == "Seeded Ravens");
        var membership = db.TeamMemberships.Local.First(value => value.TeamId == team.Id && value.Role == TeamMembershipRole.Captain);
        var player = db.EventParticipants.Local.Single(value => value.Id == membership.EventParticipantId);
        var captain = db.Accounts.Local.Single(value => value.TeamId == team.Id && value.CaptainParticipantId == player.Id);

        BoardRequirementSnapshot Requirement(string tileName, int position = 1) =>
            db.BoardRequirementSnapshots.Local.Single(value => value.BoardTileId == tiles[tileName].Id && value.Position == position);
        BoardRequirementDropSnapshot Drop(BoardRequirementSnapshot requirement) =>
            db.BoardRequirementDropSnapshots.Local.First(value => value.RequirementId == requirement.Id);

        async Task<(Submission Submission, EvidenceAsset Asset)> Create(
            string tileName, int claimed, string note, byte color, bool privacy = false,
            BoardRequirementSnapshot? selectedRequirement = null,
            BoardRequirementDropSnapshot? selectedDrop = null,
            byte? sharedColor = null)
        {
            var tile = tiles[tileName];
            var requirement = selectedRequirement ?? Requirement(tileName);
            var drop = requirement.ManualObjective ? null : selectedDrop ?? Drop(requirement);
            var submittedAt = now.AddMinutes(-120 + db.Submissions.Local.Count * 3);
            var submission = new Submission(
                Guid.NewGuid(), eventId, team.Id, tile.Id, requirement.Id, drop?.Id,
                player.Id, captain.Id, claimed, submittedAt, note, null, privacy);
            var stored = await StoreSeedImageAsync(eventId, submission.Id, $"{tileName}-proof.png", sharedColor ?? color, cancellationToken);
            var asset = SeedAsset(submission.Id, captain.Id, stored, EvidenceAssetRole.OriginalEvidence, submittedAt);
            db.Submissions.Add(submission);
            db.EvidenceAssets.Add(asset);
            db.ReviewActions.Add(SeedAction(submission.Id, ReviewActionType.Submitted, captain.Id, submittedAt, note));
            return (submission, asset);
        }

        await Create("Sarachnis", 1, "Pending standard drop for approval testing.", 25);

        var voidwaker = Requirement("Voidwaker");
        var voidwakerDrop = db.BoardRequirementDropSnapshots.Local.First(value => value.RequirementId == voidwaker.Id);
        await Create("Voidwaker", 1, "First pending copy of an identical screenshot.", 45, selectedRequirement: voidwaker, selectedDrop: voidwakerDrop, sharedColor: 77);
        await Create("Voidwaker", 1, "Second pending copy: checksum warning and duplicate-action test.", 46, selectedRequirement: voidwaker, selectedDrop: voidwakerDrop, sharedColor: 77);

        var changes = await Create("Zulrah unique table", 1, "Changes-requested workflow fixture.", 95);
        changes.Submission.RequestChanges("Please upload a clearer screenshot containing the complete game message.", now.AddMinutes(-14));
        db.ReviewActions.Add(SeedAction(changes.Submission.Id, ReviewActionType.RequestChanges, adminId, now.AddMinutes(-14), "Please upload a clearer screenshot containing the complete game message."));

        var rejected = await Create("Araxxor pet or uniques", 1, "Rejected-history fixture.", 115);
        rejected.Submission.Reject("The screenshot does not show the required drop message.", now.AddMinutes(-12));
        db.ReviewActions.Add(SeedAction(rejected.Submission.Id, ReviewActionType.Reject, adminId, now.AddMinutes(-12), "The screenshot does not show the required drop message."));

        var withdrawn = await Create("Alchemical Hydra", 1, "Withdrawn captain-mistake fixture.", 135);
        withdrawn.Submission.Withdraw(now.AddMinutes(-10));
        db.ReviewActions.Add(SeedAction(withdrawn.Submission.Id, ReviewActionType.Withdraw, captain.Id, now.AddMinutes(-10), "Seeded captain withdrawal"));

        var approved = await Create("Nex", 1, "Approved visible evidence fixture.", 155);
        ApproveSeeded(approved.Submission, adminId, 1, now.AddMinutes(-8));

        var privateApproval = await Create("Alchemical Hydra", 1, "Captain requested public privacy.", 175, privacy: true);
        ApproveSeeded(privateApproval.Submission, adminId, 1, now.AddMinutes(-7));
        privateApproval.Submission.SetPublicEvidenceHidden(true);

        var theatreRequirement = Requirement("Theatre of Blood megarares");
        var theatreDrop = Drop(theatreRequirement);
        var weightedFive = await Create("Theatre of Blood megarares", 5, "Approved weight 5; reverse this to test rebalancing.", 195, selectedRequirement: theatreRequirement, selectedDrop: theatreDrop);
        ApproveSeeded(weightedFive.Submission, adminId, 5, now.AddMinutes(-6));
        var cappedOne = await Create("Theatre of Blood megarares", 5, "Claimed 5 but capped to 1 while the requirement is full.", 215, selectedRequirement: theatreRequirement, selectedDrop: theatreDrop);
        ApproveSeeded(cappedOne.Submission, adminId, 1, now.AddMinutes(-5));

        var replacement = await Create("Chambers of Xeric megarares", 2, "Replacement evidence history fixture.", 235);
        replacement.Asset.Deactivate();
        var replacementStored = await StoreSeedImageAsync(eventId, replacement.Submission.Id, "replacement-proof.png", 245, cancellationToken);
        db.EvidenceAssets.Add(SeedAsset(replacement.Submission.Id, captain.Id, replacementStored, EvidenceAssetRole.ReplacementEvidence, now.AddMinutes(-3)));
        db.ReviewActions.Add(SeedAction(replacement.Submission.Id, ReviewActionType.ReplaceEvidence, captain.Id, now.AddMinutes(-3), "Seeded replacement image"));

        // Finish the remainder of row one so Milestone 7 has a visible completed-line fixture.
        foreach (var position in new[] { 1, 2 })
        {
            var chestRequirement = Requirement("Barrows + Lunar chests", position);
            var chestDrop = Drop(chestRequirement);
            for (var count = 0; count < 5; count++)
            {
                var chest = await Create(
                    "Barrows + Lunar chests", 1, "Approved public-board line fixture.",
                    (byte)(30 + position * 20 + count), selectedRequirement: chestRequirement, selectedDrop: chestDrop);
                ApproveSeeded(chest.Submission, adminId, 1, now.AddMinutes(-20 + position * 5 + count));
            }
        }

        var speedRequirement = Requirement("Theatre Trio Speed Run");
        for (var count = 0; count < 3; count++)
        {
            var speed = await Create(
                "Theatre Trio Speed Run", 1, "Approved public-board line fixture.",
                (byte)(85 + count), selectedRequirement: speedRequirement);
            ApproveSeeded(speed.Submission, adminId, 1, now.AddMinutes(-4 + count));
        }
    }

    private async Task AddCompletedBoardAsync(Guid eventId, Guid adminId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var board = db.Boards.Local.Single(value => value.EventId == eventId);
        var team = db.Teams.Local.Single(value => value.EventId == eventId && value.Name == "Seeded Ravens");
        var membership = db.TeamMemberships.Local.First(value => value.TeamId == team.Id && value.Role == TeamMembershipRole.Captain);
        var player = db.EventParticipants.Local.Single(value => value.Id == membership.EventParticipantId);
        var captain = db.Accounts.Local.Single(value => value.TeamId == team.Id && value.CaptainParticipantId == player.Id);
        var tiles = db.BoardTiles.Local.Where(value => value.BoardId == board.Id).OrderBy(value => value.RowIndex).ThenBy(value => value.ColumnIndex).ToList();
        var counter = 0;

        foreach (var tile in tiles)
        {
            var requirements = db.BoardRequirementSnapshots.Local.Where(value => value.BoardTileId == tile.Id).OrderBy(value => value.Position).ToList();
            foreach (var requirement in requirements)
            {
                var drops = db.BoardRequirementDropSnapshots.Local.Where(value => value.RequirementId == requirement.Id).OrderBy(value => value.ItemName).ToList();
                for (var amount = 0; amount < requirement.TargetContribution; amount++)
                {
                    var drop = requirement.ManualObjective ? null : drops[amount % drops.Count];
                    var submittedAt = now.AddHours(-3).AddMinutes(counter);
                    var submission = new Submission(
                        Guid.NewGuid(), eventId, team.Id, tile.Id, requirement.Id, drop?.Id,
                        player.Id, captain.Id, 1, submittedAt,
                        "Seeded approved evidence for completed-board finalization testing.", null);
                    var stored = await StoreSeedImageAsync(
                        eventId, submission.Id, $"completed-{counter + 1}.png",
                        (byte)(25 + counter % 200), cancellationToken);
                    db.Submissions.Add(submission);
                    db.EvidenceAssets.Add(SeedAsset(
                        submission.Id, captain.Id, stored, EvidenceAssetRole.OriginalEvidence, submittedAt));
                    db.ReviewActions.Add(SeedAction(
                        submission.Id, ReviewActionType.Submitted, captain.Id, submittedAt, "Seeded completed-board evidence"));
                    ApproveSeeded(submission, adminId, 1, now.AddMinutes(-50).AddSeconds(counter));
                    counter++;
                }
            }
        }
    }

    private void ApproveSeeded(Submission submission, Guid adminId, int contribution, DateTimeOffset approvedAt)
    {
        submission.Approve(contribution, approvedAt);
        db.SubmissionContributions.Add(new SubmissionContribution(
            Guid.NewGuid(), submission.Id, submission.TeamId, submission.RequirementId,
            submission.DropSnapshotId, submission.CreditedParticipantId, contribution, approvedAt));
        db.ReviewActions.Add(SeedAction(submission.Id, ReviewActionType.Approve, adminId, approvedAt, $"Seeded approved contribution: {contribution}"));
    }

    private async Task<StoredEvidence> StoreSeedImageAsync(Guid eventId, Guid submissionId, string filename, byte color, CancellationToken cancellationToken)
    {
        using var image = new Image<Rgba32>(640, 360, new Rgba32(color, (byte)(255 - color), (byte)(40 + color / 2)));
        await using var stream = new MemoryStream();
        await image.SaveAsPngAsync(stream, cancellationToken);
        stream.Position = 0;
        return await evidenceStorage.StoreAsync(eventId, submissionId, filename, stream, cancellationToken);
    }

    private static EvidenceAsset SeedAsset(Guid submissionId, Guid captainId, StoredEvidence stored, EvidenceAssetRole role, DateTimeOffset at) =>
        new(Guid.NewGuid(), submissionId, stored.StorageKey, stored.OriginalFilename, stored.MediaType,
            stored.ByteSize, stored.Width, stored.Height, stored.Checksum, at, captainId, role);

    private static ReviewAction SeedAction(Guid submissionId, ReviewActionType type, Guid accountId, DateTimeOffset at, string? note) =>
        new(Guid.NewGuid(), submissionId, type, accountId, at, note, null, null);

    private async Task<BoardBlueprint> BuildCanonicalBlueprintAsync(CancellationToken cancellationToken)
    {
        var specifications = new[]
        {
            new SeedTile("Alchemical Hydra", null, [new(["Alchemical Hydra"], 1, true, false, null, "Collect 1 eligible Hydra drop")]),
            new SeedTile("Barrows + Lunar chests", null,
            [
                new(["Barrows Chests"], 5, true, false, null, "Open 5 Barrows reward chests"),
                new(["Lunar Chests"], 5, true, false, null, "Open 5 Lunar reward chests")
            ]),
            new SeedTile("Theatre Trio Speed Run", 5,
            [
                new([], 3, true, false, null, "Complete Theatre of Blood in a 3-person scale in under 15:00", true)
            ]),
            new SeedTile("Theatre of Blood megarares", null,
            [
                new(["Theatre of Blood"], 6, true, true, null, "Collect 6 purple-chest contributions; a megarare may count for 2")
            ]),
            new SeedTile("Zulrah unique table", null, [new(["Zulrah"], 5, true, false, null, "Collect 5 drops from Zulrah's eligible table")]),
            new SeedTile("Araxxor pet or uniques", null, [new(["Araxxor"], 3, true, true, null, "Collect 3 eligible contributions; a pet may carry a higher weight")]),
            new SeedTile("Nex", null, [new(["Nex"], 3, true, false, null, "Collect 3 eligible Nex drops")]),
            new SeedTile("Sarachnis", null, [new(["Sarachnis"], 3, true, false, null, "Collect 3 eligible Sarachnis drops")]),
            new SeedTile("Chambers of Xeric megarares", null,
            [
                new(["Chambers of Xeric"], 6, true, true, null, "Collect 6 purple-chest contributions; a megarare may count for 2")
            ]),
            new SeedTile("Voidwaker", null,
            [
                new(["Artio", "Calvar'ion", "Spindel"], 3, false, false, "Voidwaker", "Collect the 3 different Voidwaker pieces")
            ]),
            new SeedTile("Inferno completions", 15,
            [
                new([], 3, true, false, null, "Complete 3 Inferno runs", true)
            ]),
            new SeedTile("God Wars unique collection", null,
            [
                new(["General Graardor", "Kree'arra", "K'ril Tsutsaroth", "Commander Zilyana"], 10, false, false, null, "Collect 10 different eligible God Wars drops")
            ])
        };
        var bossNames = specifications.SelectMany(specification => specification.Requirements).SelectMany(requirement => requirement.Bosses).Distinct().ToList();
        var bosses = await db.BossActivities.AsNoTracking().Where(boss => bossNames.Contains(boss.Name)).ToListAsync(cancellationToken);
        var bossIds = bosses.Select(boss => boss.Id).ToList();
        var dropRows = await (from drop in db.SourceDrops.AsNoTracking()
                              join item in db.CatalogueItems.AsNoTracking() on drop.ItemId equals item.Id
                              join boss in db.BossActivities.AsNoTracking() on drop.BossActivityId equals boss.Id
                              where bossIds.Contains(boss.Id) && drop.Active && item.Active
                              select new { drop, item, boss }).ToListAsync(cancellationToken);
        var tiles = new List<TileBlueprint>();
        for (var index = 0; index < specifications.Length; index++)
        {
            var specification = specifications[index];
            var requirements = new List<RequirementBlueprint>();
            var estimates = new List<decimal?>();
            for (var requirementIndex = 0; requirementIndex < specification.Requirements.Length; requirementIndex++)
            {
                var requirement = specification.Requirements[requirementIndex];
                var selectedBosses = bosses.Where(boss => requirement.Bosses.Contains(boss.Name)).ToList();
                var selectedDrops = dropRows.Where(row =>
                    requirement.Bosses.Contains(row.boss.Name) &&
                    (requirement.ItemNameContains is null || row.item.Name.Contains(requirement.ItemNameContains, StringComparison.OrdinalIgnoreCase))).ToList();
                if (!requirement.Manual && selectedDrops.Count == 0)
                {
                    throw new InvalidOperationException($"The retained catalogue has no eligible drops for seeded tile '{specification.Name}'.");
                }
                requirements.Add(new RequirementBlueprint(
                    requirementIndex + 1, requirement.Target, requirement.Duplicates, requirement.HigherWeights,
                    requirement.Description, requirement.Manual,
                    selectedBosses.Select(boss => new BossBlueprint(boss.Id, boss.Name, boss.EfficientCompletionsPerHour)).ToList(),
                    selectedDrops.Select(row => new DropBlueprint(
                        row.drop.Id, row.boss.Name, row.item.Name, row.drop.DisplayRate,
                        row.drop.NumericProbability, requirement.Duplicates ? null : 1, row.drop.DefaultEhbEstimate)).ToList()));
                estimates.Add(requirement.Manual
                    ? null
                    : EhbCalculator.CalculateDropRequirement(
                        requirement.Target,
                        selectedDrops.Select(row => new EligibleDropRate(
                            row.boss.EfficientCompletionsPerHour, row.drop.NumericProbability, row.drop.ItemId)),
                        requirement.Duplicates));
            }
            var description = string.Join("; ", specification.Requirements.Select(requirement => requirement.Description));
            var ehb = EhbCalculator.SumRequirements(estimates, specification.ManualEhb);
            tiles.Add(new TileBlueprint(
                index / 4, index % 4, specification.Name, description,
                "Submit one screenshot showing the player name and game message.",
                Math.Max(1, ehb), requirements));
        }
        return new BoardBlueprint("Canonical edge-case board", 3, 4, tiles);
    }

    private Task<int> ClearWorkflowDataAsync(CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                official_placements, event_finalizations, final_review_resolutions, team_completion_corrections,
                submission_contributions, review_actions, evidence_assets, submissions, evidence_codes,
                draft_picks, team_memberships, draft_sessions, teams,
                board_requirement_drop_snapshots, board_requirement_boss_snapshots, board_requirement_snapshots,
                board_tiles, template_requirement_drops, template_requirement_bosses, tile_template_requirements,
                tile_templates, boards, signup_answers, signup_questions, event_participants,
                event_state_transitions, events, audit_entries
            RESTART IDENTITY;
            DELETE FROM accounts WHERE role = 'Captain';
            """,
            cancellationToken);

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static string Describe(ScenarioStage stage) => stage switch
    {
        ScenarioStage.PrivateSetup => "the event is private and signups have not opened",
        ScenarioStage.SignupsOpen => "signups are open, the cap is full, and three people are waiting",
        ScenarioStage.PreBoard => "signups are closed and no board exists yet",
        ScenarioStage.BoardDraft => "the edge-case board exists and remains editable",
        ScenarioStage.DraftSetup => "the board is published and the draft is ready to configure",
        ScenarioStage.DraftRunning => "two picks have been made and the draft can be continued or undone",
        ScenarioStage.PostDraft => "the board and all team rosters are published",
        ScenarioStage.Live => "captain accounts can submit evidence",
        ScenarioStage.FinalReview => "the event ended and is waiting for review or corrections",
        ScenarioStage.ReviewCases => "the live review queue contains seeded submission lifecycle and evidence edge cases",
        ScenarioStage.Finalized => "official placements are snapshotted and ready for archive or correction-history testing",
        ScenarioStage.CompletedFinalReview => "one team has completed the full board and its completion time can be corrected",
        _ => stage.ToString()
    };

    private enum ScenarioStage
    {
        PrivateSetup = 0,
        SignupsOpen = 1,
        PreBoard = 2,
        BoardDraft = 3,
        DraftSetup = 4,
        DraftRunning = 5,
        PostDraft = 6,
        Live = 7,
        FinalReview = 8,
        ReviewCases = 9,
        Finalized = 10,
        CompletedFinalReview = 11
    }

    private enum DraftSeedState { Setup, Running, Finalized }
    private sealed record SeedTile(string Name, decimal? ManualEhb, SeedRequirement[] Requirements);
    private sealed record SeedRequirement(string[] Bosses, int Target, bool Duplicates, bool HigherWeights, string? ItemNameContains, string Description, bool Manual = false);
    private sealed record BoardBlueprint(string Name, int Rows, int Columns, IReadOnlyList<TileBlueprint> Tiles);
    private sealed record TileBlueprint(int Row, int Column, string Name, string Description, string EvidenceInstructions, decimal Ehb, IReadOnlyList<RequirementBlueprint> Requirements);
    private sealed record RequirementBlueprint(int Position, int Target, bool Duplicates, bool HigherWeights, string Description, bool Manual, IReadOnlyList<BossBlueprint> Bosses, IReadOnlyList<DropBlueprint> Drops);
    private sealed record BossBlueprint(Guid Id, string Name, decimal? Rate);
    private sealed record DropBlueprint(Guid Id, string Boss, string Item, string DisplayRate, decimal? Probability, int? Maximum, decimal? Ehb);
}

public sealed record SeedResult(
    string AdminUsername,
    string SecondaryAdminUsername,
    string SecondaryAdminPassword,
    string BoardBlueprint,
    IReadOnlyList<SeededScenario> Scenarios,
    string CaptainPassword);

public sealed record SeededScenario(
    Guid EventId,
    string EventName,
    EventState EventState,
    BoardState? BoardState,
    IReadOnlyList<string> CaptainUsernames);
