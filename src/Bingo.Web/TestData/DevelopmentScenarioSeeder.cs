using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bingo.Application.Boards;
using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
    private Dictionary<string, OsrsCharacter> seedCharacters = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, SeedSignupForm> seedForms = [];
    public const string CaptainPassword = "SeedCaptain!1234";
    public const string EvidenceCaptainUsername = "SeedEvidenceCaptain";
    public const string EvidenceCaptainPassword = "SeedEvidence!1234";
    public const string EvidenceCoCaptainUsername = "SeedEvidenceCoCaptain";
    public const string EvidenceCoCaptainPassword = "SeedEvidenceCoCaptain!1234";
    public const string EvidenceParticipantUsername = "SeedEvidenceParticipant";
    public const string EvidenceParticipantPassword = "SeedEvidenceParticipant!1234";
    public const string ReplacementUsername = "SeedReplacement";
    public const string ReplacementPassword = "SeedReplacement!1234";
    public const string EvidenceDisabledEmergencyUsername = "SeedEvidenceEmergencyDisabled";
    public const string SecondaryAdminUsername = "SeedAdminTwo";
    public const string SecondaryAdminPassword = "SeedAdmin!1234";

    public async Task<SeedResult> ResetAndSeedAsync(CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("Test scenario seeding is available only in Development.");
        }

        var admin = await db.Accounts.AsNoTracking()
            .Where(account => account.GlobalRole == GlobalRole.SuperAdmin && account.DisabledAt == null)
            .OrderBy(account => account.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await db.Accounts.AsNoTracking()
                .Where(account => account.GlobalRole == GlobalRole.Admin && account.DisabledAt == null)
                .OrderBy(account => account.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Create a local administrator before resetting test data.");

        var blueprint = await BuildCanonicalBlueprintAsync(cancellationToken);
        var dklBlueprint = await BuildDklBlueprintAsync(cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await ClearWorkflowDataAsync(cancellationToken);
        db.ChangeTracker.Clear();
        seedForms.Clear();
        seedCharacters = await db.OsrsCharacters
            .ToDictionaryAsync(character => character.NormalizedName, StringComparer.Ordinal, cancellationToken);

        var current = timeProvider.GetUtcNow();
        var now = new DateTimeOffset(current.Year, current.Month, current.Day, current.Hour, current.Minute < 30 ? 0 : 30, 0, TimeSpan.Zero);
        var secondaryAdmin = await EnsureSecondaryAdminAsync(now, cancellationToken);
        var evidenceCaptain = await EnsureEvidenceCaptainAsync(now, cancellationToken);
        var evidenceCoCaptain = await EnsureWebsiteAccountAsync(EvidenceCoCaptainUsername, EvidenceCoCaptainPassword, now, cancellationToken);
        var evidenceParticipant = await EnsureWebsiteAccountAsync(EvidenceParticipantUsername, EvidenceParticipantPassword, now, cancellationToken);
        var replacementAccount = await EnsureWebsiteAccountAsync(ReplacementUsername, ReplacementPassword, now, cancellationToken);
        var seeded = new List<SeededScenario>();

        var boardScenario = await SeedScenario(
            "Sommerbingo 2026",
            "test-13-dkl-board",
            ScenarioStage.BoardDraft,
            dklBlueprint,
            admin.Id,
            secondaryAdmin,
            now);
        seeded.Add(boardScenario);
        seeded.Add(SeedMissingSignupFormScenario(admin.Id, now));
        seeded.Add(SeedMalformedSignupQuestionScenario(admin.Id, now));
        seeded.Add(SeedUnusableSignupCodeScenario(admin.Id, now));
        seeded.Add(SeedScheduledConstraintScenario(
            "Forårsbingo 2027",
            "test-95-missing-scheduled-window",
            null, null, now.AddDays(7), now.AddDays(12), admin.Id, now));
        seeded.Add(SeedScheduledConstraintScenario(
            "Efterårsbingo 2027",
            "test-96-invalid-scheduled-window",
            now.AddDays(1), now.AddDays(-1), now.AddDays(7), now.AddDays(12), admin.Id, now));
        seeded.Add(SeedScheduledConstraintScenario(
            "Vinterbingo 2027",
            "test-97-signup-closes-after-event",
            now.AddDays(1), now.AddDays(8), now.AddDays(7), now.AddDays(12), admin.Id, now));
        var missingPlayingScenario = await SeedScenario(
            "Det Store Danske Sommerbingo 2027",
            "test-98-missing-playing-assignment",
            ScenarioStage.PostDraft,
            blueprint,
            admin.Id,
            secondaryAdmin,
            now);
        var missingPlayingParticipant = db.EventParticipants.Local
            .Where(participant => participant.EventId == missingPlayingScenario.EventId)
            .OrderBy(participant => participant.SignupSequence)
            .First();
        db.EventParticipantCharacters.Local
            .Single(assignment => assignment.EventId == missingPlayingScenario.EventId &&
                                  assignment.EventParticipantId == missingPlayingParticipant.Id &&
                                  assignment.EventRole == EventCharacterRole.Playing)
            .Release(admin.Id, now);
        seeded.Add(missingPlayingScenario);
        var scheduledScenario = await SeedScenario(
            "Martsbingo 2026",
            "test-05-scheduled-lifecycle-blockers",
            ScenarioStage.PreBoard,
            blueprint,
            admin.Id,
            secondaryAdmin,
            now);
        db.ScheduledEventStartAttempts.Add(new ScheduledEventStartAttempt(
            Guid.NewGuid(), scheduledScenario.EventId, now.AddHours(-2), now.AddHours(-1), false,
            ["BOARD_NOT_PUBLISHED", "DRAFT_NOT_FINALIZED"]));
        db.ScheduledSignupOpeningAttempts.Add(new ScheduledSignupOpeningAttempt(
            Guid.NewGuid(), scheduledScenario.EventId, now.AddHours(-3), now.AddHours(-2), false,
            ["LIFECYCLE_STATE_INVALID"],
            ["A scheduled signup opening can only be saved for a private draft."]));
        seeded.Add(scheduledScenario);
        seeded.Add(SeedReadinessBlockerScenario(admin.Id, now));
        seeded.Add(await SeedScenario(
            "Januarbingo 2026",
            "test-03-draft-discard-candidate",
            ScenarioStage.PrivateSetup,
            blueprint,
            admin.Id,
            secondaryAdmin,
            now));
        var dklLiveScenario = SeedDklLiveScenario(dklBlueprint, admin.Id, evidenceCaptain, evidenceCoCaptain, evidenceParticipant, now);
        seeded.Add(dklLiveScenario);
        var currentPublicScenario = await SeedScenario(
            "Forårsbingo 2026",
            "test-90-current-public-event",
            ScenarioStage.Live,
            blueprint,
            admin.Id,
            secondaryAdmin,
            now);
        var currentPublicEvent = db.Events.Local.Single(value => value.Id == currentPublicScenario.EventId);
        currentPublicEvent.MarkFirstPublic(now.AddMinutes(-30));
        db.Entry(currentPublicEvent).Property(nameof(BingoEvent.IsDevelopmentFixture)).CurrentValue = false;
        seeded.Add(currentPublicScenario);
        seeded.Add(SeedScheduledWindowOverlapScenario(admin.Id, now));
        var accessBlockerScenario = await SeedScenario(
            "Aftenbingo 2026",
            "test-88-live-access-blocker",
            ScenarioStage.Live,
            blueprint,
            admin.Id,
            secondaryAdmin,
            now);
        db.RemoveRange(db.AccountEventAccesses.Local.Where(access => access.EventId == accessBlockerScenario.EventId).ToList());
        seeded.Add(accessBlockerScenario);
        await EnsureDevelopmentLookupCharacterAsync(secondaryAdmin.Id, now, cancellationToken);
        seeded.Add(SeedSignupLookupScenario(dklBlueprint, admin.Id, now));
        var waitingReplacement = CreateParticipant(
            dklLiveScenario.EventId, "Slice 9 Waiting Replacement", 100, SignupStatus.WaitingList,
            10_000, now.AddMinutes(-5), SignupSource.Website);
        waitingReplacement.AssignOwner(replacementAccount);
        db.EventParticipants.Add(waitingReplacement);
        await AddDklLiveProgress(dklLiveScenario.EventId, admin.Id, now, cancellationToken);
        await AddDklReviewStatesAsync(dklLiveScenario.EventId, admin.Id, now, cancellationToken);
        var evidenceHistoryScenario = await SeedScenario(
            "Påskebingo 2026",
            "test-84-evidence-history",
            ScenarioStage.Finalized,
            blueprint,
            admin.Id,
            evidenceCaptain,
            now);
        var historyParticipant = db.EventParticipants.Local
            .Where(participant => participant.EventId == evidenceHistoryScenario.EventId &&
                                  db.TeamMemberships.Local.Any(membership => membership.EventParticipantId == participant.Id && membership.Role == TeamMembershipRole.Participant))
            .OrderBy(participant => participant.SignupSequence)
            .First();
        historyParticipant.AssignOwner(evidenceParticipant);
        await AddHistoryRejectedStateAsync(evidenceHistoryScenario.EventId, historyParticipant, evidenceParticipant.Id, admin.Id, now, cancellationToken);
        seeded.Add(evidenceHistoryScenario);
        seeded.Add(await SeedScenario(
            "Det Store Danske Forårsbingo 2026",
            "test-21-final-review",
            ScenarioStage.FinalReview,
            blueprint,
            admin.Id,
            secondaryAdmin,
            now));
        seeded.Add(await SeedScenario(
            "Julebingo 2025",
            "test-85-archived-results",
            ScenarioStage.Archived,
            blueprint,
            admin.Id,
            secondaryAdmin,
            now));
        seeded.Add(SeedCancelledScenario(admin.Id, secondaryAdmin, now));
        seeded.Add(SeedDiscardedScenario(admin.Id, now));
        seeded.Add(SeedBoardPublicationSetupScenario(blueprint, admin.Id, secondaryAdmin, now));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SeedResult(admin.LoginName, secondaryAdmin.LoginName, SecondaryAdminPassword, blueprint.Name, seeded, CaptainPassword, ReplacementUsername, ReplacementPassword);
    }

    private SeededScenario SeedReadinessBlockerScenario(Guid adminId, DateTimeOffset now)
    {
        var bingoEvent = new BingoEvent(
            Guid.NewGuid(), "Februarbingo 2026", "test-04-readiness-blockers",
            "Europe/Copenhagen", adminId, now);
        db.Entry(bingoEvent).Property(nameof(BingoEvent.IsDevelopmentFixture)).CurrentValue = true;
        db.Events.Add(bingoEvent);
        AddSignupFoundation(bingoEvent, now);
        return new SeededScenario(bingoEvent.Id, bingoEvent.Name, bingoEvent.State, null, []);
    }

    private SeededScenario SeedMissingSignupFormScenario(Guid adminId, DateTimeOffset now)
    {
        var eventStarts = now.AddDays(7);
        var eventEnds = now.AddDays(12);
        var bingoEvent = new BingoEvent(
            Guid.NewGuid(), "Børnebingo 2026", "test-92-missing-signup-form",
            "Development seed scenario: signup form has not been created.",
            "Europe/Copenhagen", now.AddDays(-1), now.AddDays(1), eventStarts, eventEnds,
            eventEnds.AddMinutes(30), 20, adminId, now);
        bingoEvent.ConfigureSignup(true, false, null);
        bingoEvent.ConfigurePlanning("Seeded missing-form rules.", null, null, 2, 3, 5, 5);
        db.Entry(bingoEvent).Property(nameof(BingoEvent.IsDevelopmentFixture)).CurrentValue = true;
        db.Events.Add(bingoEvent);
        return new SeededScenario(bingoEvent.Id, bingoEvent.Name, bingoEvent.State, null, []);
    }

    private SeededScenario SeedMalformedSignupQuestionScenario(Guid adminId, DateTimeOffset now)
    {
        var eventStarts = now.AddDays(7);
        var eventEnds = now.AddDays(12);
        var bingoEvent = new BingoEvent(
            Guid.NewGuid(), "Familiebingo 2026", "test-93-malformed-signup-questions",
            "Development seed scenario: signup question definition is structurally invalid.",
            "Europe/Copenhagen", now.AddDays(-1), now.AddDays(1), eventStarts, eventEnds,
            eventEnds.AddMinutes(30), 20, adminId, now);
        bingoEvent.ConfigureSignup(true, false, null);
        bingoEvent.ConfigurePlanning("Seeded malformed-question rules.", null, null, 2, 3, 5, 5);
        db.Entry(bingoEvent).Property(nameof(BingoEvent.IsDevelopmentFixture)).CurrentValue = true;
        db.Events.Add(bingoEvent);
        AddSignupFoundation(bingoEvent, now);
        var form = db.SignupForms.Local.Single(item => item.EventId == bingoEvent.Id);
        db.SignupQuestions.Add(new SignupQuestion(
            Guid.NewGuid(), form.Id, bingoEvent.Id, "invalid_required_account", "Invalid required account",
            SignupQuestionType.Account, true, 7, null, SignupSystemField.None, EventCharacterRole.Playing));
        return new SeededScenario(bingoEvent.Id, bingoEvent.Name, bingoEvent.State, null, []);
    }

    private SeededScenario SeedUnusableSignupCodeScenario(Guid adminId, DateTimeOffset now)
    {
        var eventStarts = now.AddDays(7);
        var eventEnds = now.AddDays(12);
        var bingoEvent = new BingoEvent(
            Guid.NewGuid(), "Sommerferiebingo 2026", "test-94-unusable-signup-code",
            "Development seed scenario: signup-code protection has no usable hash.",
            "Europe/Copenhagen", now.AddDays(-1), now.AddDays(1), eventStarts, eventEnds,
            eventEnds.AddMinutes(30), 20, adminId, now);
        bingoEvent.ConfigureSignup(true, true, null);
        bingoEvent.ConfigurePlanning("Seeded signup-code rules.", null, null, 2, 3, 5, 5);
        db.Entry(bingoEvent).Property(nameof(BingoEvent.IsDevelopmentFixture)).CurrentValue = true;
        db.Events.Add(bingoEvent);
        AddSignupFoundation(bingoEvent, now);
        return new SeededScenario(bingoEvent.Id, bingoEvent.Name, bingoEvent.State, null, []);
    }

    private SeededScenario SeedScheduledConstraintScenario(
        string name,
        string slug,
        DateTimeOffset? signupOpensAt,
        DateTimeOffset? signupClosesAt,
        DateTimeOffset eventStartsAt,
        DateTimeOffset eventEndsAt,
        Guid adminId,
        DateTimeOffset now)
    {
        var bingoEvent = new BingoEvent(
            Guid.NewGuid(), name, slug,
            "Development seed scenario: scheduled signup timing requires correction.",
            "Europe/Copenhagen", signupOpensAt, signupClosesAt, eventStartsAt, eventEndsAt,
            eventEndsAt.AddMinutes(30), 20, adminId, now);
        bingoEvent.ConfigureSignup(true, false, null);
        bingoEvent.ConfigurePlanning("Seeded schedule-constraint rules.", null, null, 2, 3, 5, 5);
        db.Entry(bingoEvent).Property(nameof(BingoEvent.IsDevelopmentFixture)).CurrentValue = true;
        db.Events.Add(bingoEvent);
        AddSignupFoundation(bingoEvent, now);
        return new SeededScenario(bingoEvent.Id, bingoEvent.Name, bingoEvent.State, null, []);
    }

    private SeededScenario SeedCancelledScenario(Guid adminId, Account fixtureOwner, DateTimeOffset now)
    {
        var eventStarts = now.AddDays(7);
        var eventEnds = eventStarts.AddDays(12);
        var bingoEvent = new BingoEvent(
            Guid.NewGuid(), "Weekendbingo 2026", "test-86-cancelled-event",
            "Development seed scenario: cancelled after participant history exists.",
            "Europe/Copenhagen", now.AddDays(-14), now.AddDays(-1), eventStarts, eventEnds,
            eventEnds.AddMinutes(30), 20, adminId, now);
        bingoEvent.ConfigureSignup(true, false, null);
        bingoEvent.ConfigurePlanning("Seeded cancelled-event rules.", null, null, 2, 3, 5, 5);
        bingoEvent.OpenSignups();
        bingoEvent.CloseSignups();
        db.Entry(bingoEvent).Property(nameof(BingoEvent.IsDevelopmentFixture)).CurrentValue = true;
        db.Events.Add(bingoEvent);
        AddSignupFoundation(bingoEvent, now);
        var participants = AddParticipants(bingoEvent.Id, ScenarioStage.PreBoard, now);
        participants[0].AssignOwner(fixtureOwner);
        bingoEvent.Cancel(adminId, now, "Seeded cancellation for terminal-state review.", protectedHistoryExists: true);
        return new SeededScenario(bingoEvent.Id, bingoEvent.Name, bingoEvent.State, null, []);
    }

    private SeededScenario SeedDiscardedScenario(Guid adminId, DateTimeOffset now)
    {
        var bingoEvent = new BingoEvent(
            Guid.NewGuid(), "Søndagsbingo 2026", "test-87-discarded-empty-draft",
            "Europe/Copenhagen", adminId, now);
        db.Entry(bingoEvent).Property(nameof(BingoEvent.IsDevelopmentFixture)).CurrentValue = true;
        db.Events.Add(bingoEvent);
        bingoEvent.Discard(adminId, now, protectedHistoryExists: false);
        return new SeededScenario(bingoEvent.Id, bingoEvent.Name, bingoEvent.State, null, []);
    }

    private SeededScenario SeedScheduledWindowOverlapScenario(Guid adminId, DateTimeOffset now)
    {
        var bingoEvent = new BingoEvent(
            Guid.NewGuid(), "Det Store Danske Efterårsbingo 2026", "test-91-overlapping-scheduled-opening",
            "Europe/Copenhagen", adminId, now);
        bingoEvent.ConfigureSignup(true, false, null);
        bingoEvent.ConfigurePlanning("Seeded scheduled-opening overlap rules.", null, null, 2, 3, 5, 5);
        bingoEvent.ConfigureSchedule(
            now.AddHours(-1), now.AddDays(1), null, now.AddDays(2), now.AddDays(7), 20);
        bingoEvent.ConfigureScheduledSignupOpening(true, []);
        db.Entry(bingoEvent).Property(nameof(BingoEvent.IsDevelopmentFixture)).CurrentValue = true;
        db.Events.Add(bingoEvent);
        AddSignupFoundation(bingoEvent, now);
        return new SeededScenario(bingoEvent.Id, bingoEvent.Name, bingoEvent.State, null, []);
    }

    private async Task EnsureDevelopmentLookupCharacterAsync(Guid accountId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string name = "Dev Lookup Player";
        var normalized = Normalize(name);
        if (!seedCharacters.TryGetValue(normalized, out var character))
        {
            character = new OsrsCharacter(Guid.NewGuid(), name, normalized, now);
            db.OsrsCharacters.Add(character);
            seedCharacters.Add(normalized, character);
        }
        var link = await db.AccountOsrsCharacters
            .Where(value => value.AccountId == accountId)
            .OrderByDescending(value => value.Active && value.Preferred)
            .ThenByDescending(value => value.Active)
            .ThenByDescending(value => value.Preferred)
            .ThenBy(value => value.Position)
            .ThenBy(value => value.LinkedAt)
            .ThenBy(value => value.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (link is null)
        {
            db.AccountOsrsCharacters.Add(new AccountOsrsCharacter(
                Guid.NewGuid(), accountId, character.Id, accountId, true, 0,
                "Development lookup account", 12.5m, now));
            return;
        }

        if (!link.Active) link.Relink(accountId, now);
        if (link.OsrsCharacterId != character.Id) link.CorrectCharacter(character.Id, now);
        link.UpdatePreferences("Development lookup account", 0, true, 12.5m, now);
    }

    private SeededScenario SeedSignupLookupScenario(BoardBlueprint blueprint, Guid adminId, DateTimeOffset now)
    {
        var eventStarts = now.AddDays(14);
        var eventEnds = eventStarts.AddDays(5);
        var bingoEvent = new BingoEvent(
            Guid.NewGuid(), "Efterårsbingo 2026", "test-16-signup-lookup",
            "Development seed scenario for explicit Wise Old Man lookup in signup and edit.",
            "Europe/Copenhagen", now.AddDays(-1), now.AddDays(7), eventStarts, eventEnds,
            eventEnds.AddMinutes(30), 6, adminId, now);
        bingoEvent.ConfigureSignup(true, false, null);
        bingoEvent.ConfigurePlanning(
            "Seeded signup lookup journey.", null, null, 2, 3,
            blueprint.Rows, blueprint.Columns);
        bingoEvent.OpenSignups(now.AddHours(-1));
        bingoEvent.MarkFirstPublic(now.AddMinutes(-30));
        db.Entry(bingoEvent).Property(nameof(BingoEvent.IsDevelopmentFixture)).CurrentValue = true;
        db.Events.Add(bingoEvent);
        AddSignupFoundation(bingoEvent, now);
        AddParticipants(bingoEvent.Id, ScenarioStage.SignupsOpen, now);
        return new SeededScenario(bingoEvent.Id, bingoEvent.Name, bingoEvent.State, null, []);
    }

    private async Task<Account> EnsureSecondaryAdminAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var normalized = SecondaryAdminUsername.ToUpperInvariant();
        var account = await db.Accounts.SingleOrDefaultAsync(value => value.NormalizedLoginName == normalized, cancellationToken);
        if (account is null)
        {
            account = Account.CreateWebsite(Guid.NewGuid(), SecondaryAdminUsername, normalized, now);
            account.SetGlobalRole(GlobalRole.Admin);
            db.Accounts.Add(account);
        }
        account.SetPasswordHash(passwordHasher.HashPassword(account, SecondaryAdminPassword), mustChangePassword: false);
        account.Enable();
        return account;
    }

    private async Task<Account> EnsureEvidenceCaptainAsync(DateTimeOffset now, CancellationToken cancellationToken)
        => await EnsureWebsiteAccountAsync(EvidenceCaptainUsername, EvidenceCaptainPassword, now, cancellationToken);

    private async Task<Account> EnsureWebsiteAccountAsync(string username, string password, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var normalized = username.ToUpperInvariant();
        var account = await db.Accounts.SingleOrDefaultAsync(value => value.NormalizedLoginName == normalized, cancellationToken);
        if (account is null)
        {
            account = Account.CreateWebsite(Guid.NewGuid(), username, normalized, now);
            db.Accounts.Add(account);
        }
        account.SetPasswordHash(passwordHasher.HashPassword(account, password), mustChangePassword: false);
        account.Enable();
        return account;
    }

    private async Task<SeededScenario> SeedScenario(
        string name,
        string slug,
        ScenarioStage stage,
        BoardBlueprint blueprint,
        Guid adminId,
        Account fixtureOwner,
        DateTimeOffset now)
    {
        var signupOpens = now.AddDays(-14);
        var signupCloses = stage == ScenarioStage.SignupsOpen ? now.AddDays(7) : now.AddDays(-1);
        var eventStarts = stage switch
        {
            ScenarioStage.Live => now.AddHours(-1),
            ScenarioStage.ReviewCases => now.AddMinutes(-10),
            ScenarioStage.FinalReview or ScenarioStage.Finalized or ScenarioStage.Archived or ScenarioStage.CompletedFinalReview => now.AddDays(-3),
            _ => now.AddDays(7)
        };
        var eventEnds = stage switch
        {
            ScenarioStage.Live or ScenarioStage.ReviewCases => now.AddDays(5),
            ScenarioStage.FinalReview or ScenarioStage.Finalized or ScenarioStage.Archived or ScenarioStage.CompletedFinalReview => now.AddHours(-1),
            _ => now.AddDays(12)
        };

        var bingoEvent = new BingoEvent(
            Guid.NewGuid(), name, slug,
            $"Development seed scenario: {Describe(stage)}",
            "Europe/Copenhagen", signupOpens, signupCloses, eventStarts, eventEnds,
            eventEnds.AddMinutes(30), stage == ScenarioStage.SignupsOpen ? 6 : 20,
            adminId, now);
        Guid? reviewCycleId = null;
        bingoEvent.ConfigureSignup(true, false, null);
        bingoEvent.ConfigurePlanning(
            "Seeded rules for manual workflow testing.", null, null, 2, 3,
            blueprint.Rows, blueprint.Columns);
        if (stage == ScenarioStage.SignupsOpen) bingoEvent.OpenSignups(now.AddDays(-1));
        else if (stage != ScenarioStage.PrivateSetup) { bingoEvent.OpenSignups(); bingoEvent.CloseSignups(); }
        var publishFinalizedDraft = stage >= ScenarioStage.Live;
        if (publishFinalizedDraft) bingoEvent.SetDraftRosterPublication(true);
        if (stage is ScenarioStage.Live or ScenarioStage.ReviewCases) bingoEvent.StartEvent(now);
        if (stage is ScenarioStage.FinalReview or ScenarioStage.Finalized or ScenarioStage.Archived or ScenarioStage.CompletedFinalReview)
        {
            bingoEvent.StartEvent(eventStarts);
            bingoEvent.EndEvent();
            if (stage is ScenarioStage.Finalized or ScenarioStage.Archived)
            {
                bingoEvent.FinalizeResults(now.AddMinutes(-30));
                var reviewCycle = new EventStateTransition(
                    Guid.NewGuid(), bingoEvent.Id, EventState.Live, EventState.AwaitingFinalReview,
                    adminId, eventEnds, "Seeded final-review cycle for archive history testing.");
                db.EventStateTransitions.Add(reviewCycle);
                reviewCycleId = reviewCycle.Id;
            }
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
        db.Entry(bingoEvent).Property(nameof(BingoEvent.IsDevelopmentFixture)).CurrentValue = true;
        db.Events.Add(bingoEvent);
        AddSignupFoundation(bingoEvent, now);

        var participants = AddParticipants(bingoEvent.Id, stage, now);
        if (participants.Count > 0) participants.First().AssignOwner(fixtureOwner);
        Board? board = null;
        if (stage >= ScenarioStage.BoardDraft)
        {
            board = AddBoard(bingoEvent, blueprint, stage >= ScenarioStage.DraftSetup, now);
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
            if (publishFinalizedDraft) AddDraftPublication(bingoEvent, now);
            if (stage is ScenarioStage.Finalized or ScenarioStage.Archived)
            {
                await AddCompletedBoardAsync(bingoEvent.Id, adminId, now, CancellationToken.None);
                AddFinalizedResults(bingoEvent, adminId, now, reviewCycleId ?? throw new InvalidOperationException("Seeded finalized event is missing its review cycle."));
            }
        }

        if (stage == ScenarioStage.Archived) bingoEvent.Archive(now.AddMinutes(-15));

        return new SeededScenario(bingoEvent.Id, bingoEvent.Name, bingoEvent.State, board?.State, captainUsernames);
    }

    private SeededScenario SeedBoardPublicationSetupScenario(
        BoardBlueprint blueprint,
        Guid adminId,
        Account fixtureOwner,
        DateTimeOffset now)
    {
        var eventStarts = now.AddDays(90);
        var eventEnds = eventStarts.AddDays(5);
        var bingoEvent = new BingoEvent(
            Guid.NewGuid(), "Det Store Danske Vinterbingo 2027", "test-62-board-publication-setup",
            "Development seed scenario: finalized rosters with an approved private board ready for separate publication.",
            "Europe/Copenhagen", now.AddDays(-14), now.AddDays(-1), eventStarts, eventEnds,
            eventEnds.AddMinutes(30), 20, adminId, now);
        bingoEvent.ConfigureSignup(true, false, null);
        bingoEvent.ConfigurePlanning(
            "Seeded rules for private board approval and separate publication testing.", null, null, 2, 3,
            blueprint.Rows, blueprint.Columns);
        bingoEvent.OpenSignups();
        bingoEvent.CloseSignups();
        db.Entry(bingoEvent).Property(nameof(BingoEvent.IsDevelopmentFixture)).CurrentValue = true;
        db.Events.Add(bingoEvent);
        AddSignupFoundation(bingoEvent, now);

        var participants = AddParticipants(bingoEvent.Id, ScenarioStage.DraftSetup, now);
        participants[0].AssignOwner(fixtureOwner);
        var board = AddBoard(bingoEvent, blueprint, publish: false, now);
        var captainUsernames = AddTeamsAndDraft(bingoEvent, participants, DraftSeedState.Finalized, finalized: true, now);
        AddDraftPublication(bingoEvent, now);
        bingoEvent.SetDraftRosterPublication(true);
        ApproveSeedBoard(bingoEvent, board, now);
        return new SeededScenario(bingoEvent.Id, bingoEvent.Name, bingoEvent.State, board.State, captainUsernames);
    }

    private void AddFinalizedResults(BingoEvent bingoEvent, Guid adminId, DateTimeOffset now, Guid reviewCycleId)
    {
        var finalization = new EventFinalizationSnapshot(
            Guid.NewGuid(), bingoEvent.Id, 1, now.AddMinutes(-30), adminId, reviewCycleId);
        db.EventFinalizations.Add(finalization);

        var teams = db.Teams.Local
            .Where(team => team.EventId == bingoEvent.Id && team.Active)
            .OrderBy(team => team.Name == "Seeded Ravens" ? 0 : 1)
            .ThenBy(team => team.Name)
            .ToList();
        for (var index = 0; index < teams.Count; index++)
        {
            var team = teams[index];
            var board = db.Boards.Local.Single(value => value.EventId == bingoEvent.Id);
            var completed = index == 0;
            var completedAt = completed
                ? db.Submissions.Local
                    .Where(value => value.EventId == bingoEvent.Id && value.TeamId == team.Id && value.Status == SubmissionStatus.Approved)
                    .Select(value => (DateTimeOffset?)value.SubmittedAt)
                    .Max()
                : null;
            db.OfficialPlacements.Add(new OfficialPlacementSnapshot(
                Guid.NewGuid(), finalization.Id, bingoEvent.Id, team.Id, team.Name,
                completed ? 1 : 2, completed, completedAt,
                completed ? board.Rows + board.Columns : 0, completed ? board.Rows * board.Columns : 0,
                completed ? board.TotalEhbEstimate : 0));
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
                "Large Draft Player 001",
                "Large Draft Player 002",
                ["Large Draft Player 003", "Large Draft Player 004", "Large Draft Player 005", "Large Draft Player 006", "Large Draft Player 007", "Large Draft Player 008", "Large Draft Player 009", "Large Draft Player 010", "Large Draft Player 011", "Large Draft Player 012", "Large Draft Player 013", "Large Draft Player 014", "Large Draft Player 015"]),
            (
                "Såeh cs?",
                "Large Draft Player 016",
                "Large Draft Player 017",
                ["Large Draft Player 018", "Large Draft Player 019", "Large Draft Player 020", "Large Draft Player 021", "Large Draft Player 022", "Large Draft Player 023", "Large Draft Player 024", "Large Draft Player 025", "Large Draft Player 026", "Large Draft Player 027", "Large Draft Player 028", "Large Draft Player 029", "Large Draft Player 030"]),
            (
                "Morytania Monkeys",
                "Large Draft Player 031",
                "Large Draft Player 032",
                ["Large Draft Player 033", "Large Draft Player 034", "Large Draft Player 035", "Large Draft Player 036", "Large Draft Player 037", "Large Draft Player 038", "Large Draft Player 039", "Large Draft Player 040", "Large Draft Player 041", "Large Draft Player 042", "Large Draft Player 043", "Large Draft Player 044", "Large Draft Player 045"]),
            (
                "The Agency",
                "Large Draft Player 046",
                "Large Draft Player 047",
                ["Large Draft Player 048", "Large Draft Player 049", "Large Draft Player 050", "Large Draft Player 051", "Large Draft Player 052", "Large Draft Player 053", "Large Draft Player 054", "Large Draft Player 055", "Large Draft Player 056", "Large Draft Player 057", "Large Draft Player 058", "Large Draft Player 059", "Large Draft Player 060"])
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
            "Eftermiddagsbingo 2026",
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
        bingoEvent.ConfigureSignup(true, false, null);
        bingoEvent.ConfigurePlanning(
            "Seeded rules for large-draft testing.",
            null,
            null,
            teamCount,
            targetTeamSize,
            blueprint.Rows,
            blueprint.Columns);
        bingoEvent.OpenSignups();
        bingoEvent.CloseSignups();
        db.Entry(bingoEvent).Property(nameof(BingoEvent.IsDevelopmentFixture)).CurrentValue = true;
        db.Events.Add(bingoEvent);
        AddSignupFoundation(bingoEvent, now);

        var participants = new List<EventParticipant>(participantCount);
        for (var index = 0; index < participantCount; index++)
        {
            var number = index + 1;
            var name = participantNames[index];
            var ehb = 175 + index * 83;
            var participant = CreateParticipant(
                bingoEvent.Id, name, ehb, SignupStatus.Confirmed, number,
                now.AddMinutes(-participantCount + index), SignupSource.Website,
                null, captainVolunteer: captainNames.Contains(name));
            participants.Add(participant);
        }
        db.EventParticipants.AddRange(participants);
        var participantsByName = participants.ToDictionary(
            participant => PrimaryName(participant),
            StringComparer.OrdinalIgnoreCase);

        var board = AddBoard(bingoEvent, blueprint, publish: true, now);
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

    private SeededScenario SeedDklLiveScenario(
        BoardBlueprint blueprint,
        Guid adminId,
        Account fixtureOwner,
        Account coCaptainOwner,
        Account participantOwner,
        DateTimeOffset now)
    {
        const int teamCount = 6;
        const int targetTeamSize = 10;
        var participantNames = new[]
        {
            "Dev Player 001", "Dev Player 002", "Dev Player 003", "Dev Player 004", "Dev Player 005", "Dev Player 006", "Dev Player 007", "Dev Player 008", "Dev Player 009", "Dev Player 010",
            "Dev Player 011", "Dev Player 012", "Dev Player 013", "Dev Player 014", "Dev Player 015", "Dev Player 016", "Dev Player 017", "Dev Player 018", "Dev Player 019", "Dev Player 020",
            "Dev Player 021", "Dev Player 022", "Dev Player 023", "Dev Player 024", "Dev Player 025", "Dev Player 026", "Dev Player 027", "Dev Player 028", "Dev Player 029", "Dev Player 030",
            "Dev Player 031", "Dev Player 032", "Dev Player 033", "Dev Player 034", "Dev Player 035", "Dev Player 036", "Dev Player 037", "Dev Player 038", "Dev Player 039", "Dev Player 040",
            "Dev Player 041", "Dev Player 042", "Dev Player 043", "Dev Player 044", "Dev Player 045", "Dev Player 046", "Dev Player 047", "Dev Player 048", "Dev Player 049", "Dev Player 050",
            "Dev Player 051", "Dev Player 052", "Dev Player 053", "Dev Player 054", "Dev Player 055", "Dev Player 056", "Dev Player 057", "Dev Player 058", "Dev Player 059", "Dev Player 060"
        };
        var teamSeeds = new[]
        {
            (Name: "Touch kids, not grass", Slug: "touch-kids-not-grass", Captain: "Dev Player 001", CoCaptain: "Dev Player 002"),
            (Name: "Såeh cs?", Slug: "saeh-cs", Captain: "Dev Player 011", CoCaptain: "Dev Player 012"),
            (Name: "Morytania Monkeys", Slug: "morytania-monkeys", Captain: "Dev Player 021", CoCaptain: "Dev Player 022"),
            (Name: "The Agency", Slug: "the-agency", Captain: "Dev Player 031", CoCaptain: "Dev Player 032"),
            (Name: "Xen0%_d_rops", Slug: "xen0-d-rops", Captain: "Dev Player 041", CoCaptain: "Dev Player 042"),
            (Name: "Zalamalikum", Slug: "zalamalikum", Captain: "Dev Player 051", CoCaptain: "Dev Player 052")
        };
        var leaderNames = teamSeeds
            .SelectMany(team => new[] { team.Captain, team.CoCaptain })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var remainingNames = participantNames.Where(name => !leaderNames.Contains(name)).ToArray();

        var eventStarts = now.AddHours(-99);
        var eventEnds = now.AddDays(14);
        var bingoEvent = new BingoEvent(
            Guid.NewGuid(),
            "Vinterbingo 2026",
            "test-15-dkl-live",
            "Live six-team DKL board scenario with complete drafted rosters.",
            "Europe/Copenhagen",
            now.AddDays(-14),
            now.AddDays(-1),
            eventStarts,
            eventEnds,
            eventEnds.AddMinutes(30),
            participantNames.Length,
            adminId,
            now);
        bingoEvent.ConfigureSignup(true, false, null);
        bingoEvent.ConfigurePlanning(
            "Seeded DKL rules for full live-board testing.",
            null,
            null,
            teamCount,
            targetTeamSize,
            blueprint.Rows,
            blueprint.Columns);
        bingoEvent.OpenSignups();
        bingoEvent.CloseSignups();
        bingoEvent.SetDraftLocked(true);
        db.Entry(bingoEvent).Property(nameof(BingoEvent.IsDevelopmentFixture)).CurrentValue = true;
        db.Events.Add(bingoEvent);
        AddSignupFoundation(bingoEvent, now);

        var participants = participantNames.Select((name, index) =>
        {
            return CreateParticipant(
                bingoEvent.Id, name, 175 + index * 83, SignupStatus.Confirmed, index + 1,
                now.AddDays(-2).AddMinutes(index), SignupSource.Website, null,
                captainVolunteer: leaderNames.Contains(name));
        }).ToList();
        db.EventParticipants.AddRange(participants);
        AddActivitySecondRegularAccount(participants.Single(value => PrimaryName(value) == "Dev Player 001"), now);
        var participantsByName = participants.ToDictionary(
            participant => PrimaryName(participant),
            StringComparer.OrdinalIgnoreCase);
        participantsByName[teamSeeds[0].Captain].AssignOwner(fixtureOwner);
        participantsByName[teamSeeds[0].CoCaptain].AssignOwner(coCaptainOwner);
        participantsByName[remainingNames[0]].AssignOwner(participantOwner);

        var board = AddBoard(bingoEvent, blueprint, publish: true, now);
        var draft = new DraftSession(Guid.NewGuid(), bingoEvent.Id, targetTeamSize);
        draft.Start(now.AddHours(-3));
        db.DraftSessions.Add(draft);

        var captainUsernames = new List<string>(teamCount);
        var pickNumber = 1;
        for (var teamIndex = 0; teamIndex < teamSeeds.Length; teamIndex++)
        {
            var seed = teamSeeds[teamIndex];
            var team = new Team(
                Guid.NewGuid(), bingoEvent.Id, seed.Name, seed.Slug,
                TeamFormationType.Drafted, null, true);
            team.SetDraftPosition(teamIndex + 1);
            db.Teams.Add(team);

            var captain = participantsByName[seed.Captain];
            var coCaptain = participantsByName[seed.CoCaptain];
            db.TeamMemberships.AddRange(
                new TeamMembership(Guid.NewGuid(), team.Id, captain.Id, TeamMembershipRole.Captain, now.AddHours(-3), null, "Seeded DKL captain"),
                new TeamMembership(Guid.NewGuid(), team.Id, coCaptain.Id, TeamMembershipRole.CoCaptain, now.AddHours(-3), null, "Seeded DKL co-captain"));

            foreach (var name in remainingNames.Skip(teamIndex * 8).Take(8))
            {
                AddPick(draft, team, participantsByName[name], pickNumber, (pickNumber - 1) / teamCount + 1, now);
                pickNumber++;
            }

            team.Finalize(now.AddHours(-2));
            captainUsernames.Add(AddCaptainAccount(
                bingoEvent, team, captain, CaptainDigits(bingoEvent, teamIndex + 1), now));
            if (teamIndex == 0)
                AddDisabledEmergencyCoverage(bingoEvent, team, now);
        }
        draft.Finalize(now.AddHours(-2));
        AddDraftPublication(bingoEvent, now);
        bingoEvent.SetDraftRosterPublication(true);
        bingoEvent.StartEvent(eventStarts);
        AddCompleteActivityCache(bingoEvent, now);
        var focusTeam = db.Teams.Local.Single(value => value.EventId == bingoEvent.Id && value.DraftPosition == 1);
        var focusTile = db.BoardTiles.Local.Where(value => value.BoardId == board.Id).OrderBy(value => value.RowIndex).ThenBy(value => value.ColumnIndex).Skip(5).First();
        var focusCaptainAccountId = db.Accounts.Local.Single(value => value.LoginName == captainUsernames[0]).Id;
        db.TeamFocusMarkers.AddRange(
            new TeamFocusMarker(Guid.NewGuid(), bingoEvent.Id, focusTeam.Id, TeamFocusTargetKind.Tile, focusTile.Id, null, null, true, now, focusCaptainAccountId),
            new TeamFocusMarker(Guid.NewGuid(), bingoEvent.Id, focusTeam.Id, TeamFocusTargetKind.Row, null, 1, null, true, now, focusCaptainAccountId),
            new TeamFocusMarker(Guid.NewGuid(), bingoEvent.Id, focusTeam.Id, TeamFocusTargetKind.Column, null, null, 1, true, now, focusCaptainAccountId));

        return new SeededScenario(
            bingoEvent.Id,
            bingoEvent.Name,
            bingoEvent.State,
            board.State,
            captainUsernames);
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
            var participant = CreateParticipant(
                eventId, name, 250 + index * 275,
                waiting ? SignupStatus.WaitingList : SignupStatus.Confirmed,
                index + 1, now.AddMinutes(-90 + index), SignupSource.Website,
                index % 3 == 0 ? $"{name} Alt" : null,
                captainVolunteer: index < 2);
            participants.Add(participant);
        }
        db.EventParticipants.AddRange(participants);
        return participants;
    }

    private async Task AddDklLiveProgress(
        Guid eventId,
        Guid adminId,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        IReadOnlyList<int>? completedTileCounts = null,
        DateTimeOffset? progressStart = null,
        string evidenceNote = "Approved Test 15 live-board progress fixture.",
        bool rotateAllPlayingAccounts = false)
    {
        var seedEvidenceDirectory = Path.Combine(environment.ContentRootPath, "data", "seed-evidence");
        var seedEvidenceFiles = Directory.Exists(seedEvidenceDirectory)
            ? Directory.EnumerateFiles(seedEvidenceDirectory)
                .Where(path => new[] { ".png", ".jpg", ".jpeg", ".webp" }
                    .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .ToArray()
            : [];
        var board = db.Boards.Local.Single(value => value.EventId == eventId);
        var tiles = db.BoardTiles.Local
            .Where(value => value.BoardId == board.Id)
            .OrderBy(value => value.RowIndex)
            .ThenBy(value => value.ColumnIndex)
            .ToList();
        var teams = db.Teams.Local
            .Where(value => value.EventId == eventId)
            .OrderBy(value => value.DraftPosition)
            .ToList();
        var eventStarts = db.Events.Local.Single(value => value.Id == eventId).EventStartsAt
            ?? throw new InvalidOperationException("Test 15 requires an event start time before seeding progress.");
        var progressSequence = 0;

        async Task ApproveRequirement(
            Team team,
            IReadOnlyList<(EventParticipant Participant, (Guid Id, string Name) Character)> creditedAccounts,
            Account captainAccount,
            BoardTile tile,
            BoardRequirementSnapshot requirement,
            int amount)
        {
            var remaining = amount;
            var drops = db.BoardRequirementDropSnapshots.Local
                .Where(value => value.RequirementId == requirement.Id)
                .OrderBy(value => value.ItemName)
                .ToList();
            var usedByDrop = new Dictionary<Guid, int>();
            var dropIndex = 0;

            while (remaining > 0)
            {
                BoardRequirementDropSnapshot? drop = null;
                var approvedAmount = 1;
                if (!requirement.ManualObjective)
                {
                    for (var checkedDrops = 0; checkedDrops < drops.Count; checkedDrops++)
                    {
                        var candidate = drops[dropIndex % drops.Count];
                        dropIndex++;
                        var maximum = candidate.MaximumContribution ??
                            (requirement.DuplicatesAllowed ? int.MaxValue : 1);
                        var capacity = maximum - usedByDrop.GetValueOrDefault(candidate.Id);
                        if (capacity <= 0) continue;
                        drop = candidate;
                        approvedAmount = Math.Min(remaining, Math.Min(candidate.CreditedWeight, capacity));
                        usedByDrop[candidate.Id] = usedByDrop.GetValueOrDefault(candidate.Id) + approvedAmount;
                        break;
                    }

                    if (drop is null)
                    {
                        throw new InvalidOperationException(
                            $"Test 15 cannot seed {amount} representative contributions for '{requirement.Description}'.");
                    }
                }

                var sequence = progressSequence++;
                var submittedAt = progressStart is { } historicalStart
                    ? historicalStart.AddMinutes(sequence * 3)
                    : now.AddMinutes(-15 - sequence * 33);
                if (submittedAt < eventStarts || submittedAt > now)
                    throw new InvalidOperationException("Test 15 progress timestamps must remain within the live event window.");
                var approvedAt = submittedAt.AddMinutes(5 + sequence % 7);
                if (approvedAt <= submittedAt || approvedAt > now)
                    throw new InvalidOperationException("Test 15 approval timestamps must follow submission timestamps and remain in the past.");
                var credited = creditedAccounts[progressSequence % creditedAccounts.Count];
                var submission = new Submission(
                    Guid.NewGuid(), eventId, team.Id, tile.Id, requirement.Id, drop?.Id,
                    credited.Participant.Id, credited.Character.Id, credited.Character.Name, captainAccount.Id, drop?.CreditedWeight ?? 1, submittedAt,
                    evidenceNote, null);
                StoredEvidence stored;
                if (progressStart is not null || seedEvidenceFiles.Length == 0)
                {
                    stored = await StoreSeedImageAsync(
                        eventId, submission.Id, $"progress-{team.Slug}-{tile.RowIndex}-{tile.ColumnIndex}-{sequence}.png",
                        (byte)(25 + sequence), cancellationToken);
                }
                else
                {
                    var sourcePath = seedEvidenceFiles[sequence % seedEvidenceFiles.Length];
                    var extension = Path.GetExtension(sourcePath);
                    await using var source = new FileStream(
                        sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
                    stored = await evidenceStorage.StoreAsync(
                        eventId, submission.Id,
                        $"progress-{team.Slug}-{tile.RowIndex}-{tile.ColumnIndex}-{sequence}{extension}",
                        source, cancellationToken);
                }
                db.Submissions.Add(submission);
                db.EvidenceAssets.Add(SeedAsset(
                    submission.Id, captainAccount.Id, stored,
                    EvidenceAssetRole.OriginalEvidence, submittedAt));
                db.ReviewActions.Add(SeedAction(
                    submission.Id, ReviewActionType.Submitted, captainAccount.Id,
                    submittedAt, "Seeded Test 15 progress evidence"));
                ApproveSeeded(submission, adminId, approvedAmount, approvedAt);
                remaining -= approvedAmount;
            }
        }

        for (var teamIndex = 0; teamIndex < teams.Count; teamIndex++)
        {
            var team = teams[teamIndex];
            var captainMembership = db.TeamMemberships.Local.Single(value =>
                value.TeamId == team.Id && value.Role == TeamMembershipRole.Captain);
            var captainParticipant = db.EventParticipants.Local.Single(value =>
                value.Id == captainMembership.EventParticipantId);
            var captainAccount = SeededCaptainAccount(team.Id, captainParticipant.Id);
            var memberParticipantIds = db.TeamMemberships.Local
                .Where(value => value.TeamId == team.Id && value.LeftAt is null)
                .Select(value => value.EventParticipantId)
                .ToHashSet();
            var teamParticipants = db.EventParticipants.Local
                .Where(value => memberParticipantIds.Contains(value.Id))
                .OrderBy(PrimaryName)
                .ToList();
            var creditedAccounts = teamParticipants
                .SelectMany(participant => db.EventParticipantCharacters.Local
                    .Where(assignment => assignment.EventParticipantId == participant.Id &&
                                        assignment.EventRole == EventCharacterRole.Playing && assignment.ReleasedAt == null)
                    .OrderBy(assignment => assignment.RegistrationOrder)
                    .Select(assignment => (Participant: participant,
                        Character: (Id: assignment.OsrsCharacterId,
                            seedCharacters.Values.Single(character => character.Id == assignment.OsrsCharacterId).DisplayName))))
                .ToList();
            if (!rotateAllPlayingAccounts)
                creditedAccounts = creditedAccounts
                    .GroupBy(value => value.Participant.Id)
                    .OrderBy(group => PrimaryName(group.First().Participant), StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToList();
            var completedTileCount = completedTileCounts?[teamIndex]
                ?? (team.Name == "Såeh cs?" ? tiles.Count : Math.Max(1, 5 - teamIndex));

            foreach (var tile in tiles.Take(completedTileCount))
            {
                foreach (var requirement in db.BoardRequirementSnapshots.Local
                             .Where(value => value.BoardTileId == tile.Id)
                             .OrderBy(value => value.Position))
                {
                    await ApproveRequirement(
                        team, creditedAccounts, captainAccount, tile,
                        requirement, requirement.TargetContribution);
                }
            }

            if (completedTileCount == tiles.Count) continue;

            var partialTile = tiles.Skip(completedTileCount).First(tile =>
            {
                var requirements = db.BoardRequirementSnapshots.Local
                    .Where(value => value.BoardTileId == tile.Id)
                    .ToList();
                return requirements.Count > 1 || requirements[0].TargetContribution > 1;
            });
            var partialRequirement = db.BoardRequirementSnapshots.Local
                .Where(value => value.BoardTileId == partialTile.Id)
                .OrderBy(value => value.Position)
                .First();
            var partialAmount = partialRequirement.TargetContribution > 1
                ? Math.Max(1, partialRequirement.TargetContribution / 2)
                : 1;
            await ApproveRequirement(
                team, creditedAccounts, captainAccount, partialTile,
                partialRequirement, partialAmount);
        }
    }

    private void AddActivitySecondRegularAccount(EventParticipant participant, DateTimeOffset now)
    {
        const string name = "Dev Activity Secondary";
        var normalized = Normalize(name);
        if (!seedCharacters.TryGetValue(normalized, out var character))
        {
            character = new OsrsCharacter(Guid.NewGuid(), name, normalized, now);
            db.OsrsCharacters.Add(character);
            seedCharacters.Add(normalized, character);
        }
        db.EventParticipantCharacters.Add(new EventParticipantCharacter(
            Guid.NewGuid(), participant.EventId, participant.Id, character.Id, 1, now,
            null, null, EventCharacterRole.Playing, 0, EhbSource.Manual, null));
    }

    private void AddCompleteActivityCache(BingoEvent bingoEvent, DateTimeOffset now)
    {
        var assignments = db.EventParticipantCharacters.Local
            .Where(value => value.EventId == bingoEvent.Id && value.EventRole == EventCharacterRole.Playing && value.ReleasedAt == null &&
                            db.EventParticipants.Local.Any(participant => participant.Id == value.EventParticipantId && participant.SignupStatus == SignupStatus.Confirmed))
            .OrderBy(value => value.Id.ToString("N"), StringComparer.Ordinal)
            .ToList();
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', assignments
            .Select(value => $"{value.Id:N}:{value.EventParticipantId:N}:{value.OsrsCharacterId:N}"))))).ToLowerInvariant();
        var fetchedAt = now.AddHours(-3);
        var synchronization = new EventCompetitionSynchronization(
            Guid.NewGuid(), bingoEvent.Id, 1, 1515, "TEST 15 local cached competition",
            bingoEvent.EventStartsAt, bingoEvent.EventEndsAt, fingerprint, fetchedAt);
        synchronization.MarkSuccess(fetchedAt, fetchedAt, true, "[]", null);
        db.EventCompetitionSynchronizations.Add(synchronization);
        var characters = seedCharacters.Values.ToDictionary(value => value.Id);
        db.EventCompetitionCharacterActivities.AddRange(assignments.Select((assignment, index) =>
        {
            var name = characters[assignment.OsrsCharacterId].DisplayName;
            var gained = name switch
            {
                "Dev Player 001" => 12m,
                "Dev Activity Secondary" => 8m,
                "Dev Player 002" => 20m,
                _ => 2m + index % 3
            };
            return new EventCompetitionCharacterActivity(
                Guid.NewGuid(), bingoEvent.Id, 1, 1515, assignment.OsrsCharacterId,
                gained, fetchedAt, fetchedAt, fingerprint);
        }));
    }

    private async Task AddDklReviewStatesAsync(Guid eventId, Guid adminId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var board = db.Boards.Local.Single(value => value.EventId == eventId);
        var team = db.Teams.Local.OrderBy(value => value.DraftPosition).First(value => value.EventId == eventId);
        var captainMembership = db.TeamMemberships.Local.First(value => value.TeamId == team.Id && value.Role == TeamMembershipRole.Captain);
        var participant = db.EventParticipants.Local.Single(value => value.Id == captainMembership.EventParticipantId);
        var captain = SeededCaptainAccount(team.Id, participant.Id);
        var tile = db.BoardTiles.Local.Where(value => value.BoardId == board.Id).OrderBy(value => value.RowIndex).ThenBy(value => value.ColumnIndex).Skip(5).First();
        var requirement = db.BoardRequirementSnapshots.Local.First(value => value.BoardTileId == tile.Id);
        var drop = requirement.ManualObjective ? null : db.BoardRequirementDropSnapshots.Local.First(value => value.RequirementId == requirement.Id);

        async Task<Submission> AddStateAsync(SubmissionStatus status, string note, byte color)
        {
            var submittedAt = now.AddMinutes(-20 - color);
            var credited = PrimaryCharacterSnapshot(participant);
            var submission = new Submission(Guid.NewGuid(), eventId, team.Id, tile.Id, requirement.Id, drop?.Id, participant.Id, credited.Id, credited.Name, captain.Id, drop?.CreditedWeight ?? 1, submittedAt, note, null);
            var stored = await StoreSeedImageAsync(eventId, submission.Id, $"review-{status}.png", color, cancellationToken);
            db.Submissions.Add(submission);
            db.EvidenceAssets.Add(SeedAsset(submission.Id, captain.Id, stored, EvidenceAssetRole.OriginalEvidence, submittedAt));
            db.ReviewActions.Add(SeedAction(submission.Id, ReviewActionType.Submitted, captain.Id, submittedAt, note));
            if (status == SubmissionStatus.Rejected)
            {
                submission.Reject("Seeded rejection for linked-resubmission testing.", now.AddMinutes(-5));
                db.ReviewActions.Add(SeedAction(submission.Id, ReviewActionType.Reject, adminId, now.AddMinutes(-5), "Seeded rejection for linked-resubmission testing."));
            }
            else if (status == SubmissionStatus.Withdrawn)
            {
                submission.Withdraw(now.AddMinutes(-5));
                db.ReviewActions.Add(SeedAction(submission.Id, ReviewActionType.Withdraw, captain.Id, now.AddMinutes(-5), "Seeded captain withdrawal."));
            }
            return submission;
        }

        await AddStateAsync(SubmissionStatus.Pending, "Pending evidence for Admin review testing.", 21);
        var rejected = await AddStateAsync(SubmissionStatus.Rejected, "Rejected evidence for linked-resubmission testing.", 22);
        await AddStateAsync(SubmissionStatus.Withdrawn, "Withdrawn evidence for retained-ledger testing.", 23);
        var resubmissionSubmittedAt = now.AddMinutes(-2);
        var creditedCharacter = PrimaryCharacterSnapshot(participant);
        var resubmission = new Submission(Guid.NewGuid(), eventId, team.Id, tile.Id, requirement.Id, drop?.Id, participant.Id,
            creditedCharacter.Id, creditedCharacter.Name, captain.Id, drop?.CreditedWeight ?? 1, resubmissionSubmittedAt,
            "Seeded linked resubmission.", null, rejected.Id);
        var resubmissionStored = await StoreSeedImageAsync(eventId, resubmission.Id, "review-resubmission.png", 24, cancellationToken);
        db.Submissions.Add(resubmission);
        db.EvidenceAssets.Add(SeedAsset(resubmission.Id, captain.Id, resubmissionStored, EvidenceAssetRole.OriginalEvidence, resubmissionSubmittedAt));
        db.ReviewActions.Add(SeedAction(resubmission.Id, ReviewActionType.Resubmit, captain.Id, resubmissionSubmittedAt, "Seeded linked resubmission."));
    }

    private async Task AddHistoryRejectedStateAsync(Guid eventId, EventParticipant participant, Guid submittedByAccountId, Guid adminId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var board = db.Boards.Local.Single(value => value.EventId == eventId);
        var activeTeamIds = db.TeamMemberships.Local
            .Where(membership => membership.EventParticipantId == participant.Id && membership.LeftAt == null)
            .Select(membership => membership.TeamId)
            .ToHashSet();
        var team = db.Teams.Local
            .Where(value => value.EventId == eventId && value.Active && activeTeamIds.Contains(value.Id))
            .OrderBy(value => value.DraftPosition)
            .First();
        var tile = db.BoardTiles.Local.Where(value => value.BoardId == board.Id).OrderBy(value => value.RowIndex).ThenBy(value => value.ColumnIndex).First();
        var requirement = db.BoardRequirementSnapshots.Local.First(value => value.BoardTileId == tile.Id);
        var drop = requirement.ManualObjective ? null : db.BoardRequirementDropSnapshots.Local.First(value => value.RequirementId == requirement.Id);
        var credited = PrimaryCharacterSnapshot(participant);
        var submittedAt = now.AddHours(-2);
        var submission = new Submission(Guid.NewGuid(), eventId, team.Id, tile.Id, requirement.Id, drop?.Id, participant.Id, credited.Id, credited.Name, submittedByAccountId, drop?.CreditedWeight ?? 1, submittedAt, "Seeded archived evidence history.", null);
        var stored = await StoreSeedImageAsync(eventId, submission.Id, "archived-rejected.png", 23, cancellationToken);
        db.Submissions.Add(submission);
        db.EvidenceAssets.Add(SeedAsset(submission.Id, submittedByAccountId, stored, EvidenceAssetRole.OriginalEvidence, submittedAt));
        db.ReviewActions.Add(SeedAction(submission.Id, ReviewActionType.Submitted, submittedByAccountId, submittedAt, "Seeded archived evidence history."));
        submission.Reject("Seeded rejected history for post-cutoff read-only testing.", now.AddHours(-1));
        db.ReviewActions.Add(SeedAction(submission.Id, ReviewActionType.Reject, adminId, now.AddHours(-1), "Seeded rejected history for post-cutoff read-only testing."));
    }

    private Board AddBoard(BingoEvent bingoEvent, BoardBlueprint blueprint, bool publish, DateTimeOffset now)
    {
        var eventId = bingoEvent.Id;
        var board = new Board(Guid.NewGuid(), eventId, blueprint.Name, blueprint.Rows, blueprint.Columns);
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
                        Guid.NewGuid(), templateRequirement.Id, drop.Id, drop.Maximum, drop.Weight));
                    db.BoardRequirementDropSnapshots.Add(new BoardRequirementDropSnapshot(
                        Guid.NewGuid(), snapshot.Id, drop.Id, drop.Boss, drop.Item,
                        drop.DisplayRate, drop.Probability, drop.Maximum, drop.Ehb, drop.Weight));
                }
            }
        }
        board.SetTotalEhb(total);
        if (publish) PublishSeedBoard(bingoEvent, board, now);
        return board;
    }

    // Development fixtures must exercise the same immutable public-board contract as
    // production publication. This copies the already-created local board snapshots;
    // it never derives values from a live public request.
    private void ApproveSeedBoard(BingoEvent bingoEvent, Board board, DateTimeOffset now)
    {
        // A brand-new board and its active approval pointer form a reciprocal FK pair.
        // Persist the private board tree first (still inside ResetAndSeedAsync's one
        // transaction), then add the approval tree and its pointer in the next batch.
        db.SaveChanges();
        var approval = new BoardApprovalSnapshot(
            Guid.NewGuid(), board.Id, 1, now, bingoEvent.CreatedByAccountId, null,
            board.Name, board.Rows, board.Columns, board.TotalEhbEstimate,
            board.CalculationVersion, board.Version, BoardState.Validated);
        db.BoardApprovalSnapshots.Add(approval);

        foreach (var tile in db.BoardTiles.Local.Where(value => value.BoardId == board.Id)
                     .OrderBy(value => value.RowIndex).ThenBy(value => value.ColumnIndex).ToList())
        {
            var approvalTile = new BoardApprovalTileSnapshot(
                Guid.NewGuid(), approval.Id, tile.Id, tile.TileTemplateId,
                tile.RowIndex, tile.ColumnIndex, tile.NameSnapshot,
                tile.DescriptionSnapshot, tile.EvidenceInstructionsSnapshot,
                tile.EstimatedEhbSnapshot, null);
            db.BoardApprovalTileSnapshots.Add(approvalTile);

            foreach (var requirement in db.BoardRequirementSnapshots.Local
                         .Where(value => value.BoardTileId == tile.Id)
                         .OrderBy(value => value.Position).ToList())
            {
                var approvalRequirement = new BoardApprovalRequirementSnapshot(
                    Guid.NewGuid(), approvalTile.Id, requirement.Id, requirement.Position,
                    requirement.TargetContribution, requirement.DuplicatesAllowed,
                    requirement.AllowHigherWeightings, requirement.CreditedWeight,
                    requirement.Description, requirement.ManualObjective);
                db.BoardApprovalRequirementSnapshots.Add(approvalRequirement);

                foreach (var boss in db.BoardRequirementBossSnapshots.Local
                             .Where(value => value.RequirementId == requirement.Id).ToList())
                    db.BoardApprovalRequirementBossSnapshots.Add(new BoardApprovalRequirementBossSnapshot(
                        Guid.NewGuid(), approvalRequirement.Id, boss.BossActivityId,
                        boss.BossName, boss.EfficientRate, 1));

                foreach (var drop in db.BoardRequirementDropSnapshots.Local
                             .Where(value => value.RequirementId == requirement.Id).ToList())
                {
                    var approvalDrop = new BoardApprovalRequirementDropSnapshot(
                        Guid.NewGuid(), approvalRequirement.Id, drop.SourceDropId, drop.BossName,
                        drop.ItemName, drop.DisplayRate, drop.NumericProbability,
                        drop.MaximumContribution, drop.EhbPerContribution, drop.CreditedWeight, 1);
                    db.BoardApprovalRequirementDropSnapshots.Add(approvalDrop);
                }
            }
        }

        board.Approve(approval.Id);
    }

    private void PublishSeedBoard(BingoEvent bingoEvent, Board board, DateTimeOffset now)
    {
        ApproveSeedBoard(bingoEvent, board, now);
        board.Publish(now);
        bingoEvent.SetBoardPublication(true, now);
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

            var externalCaptain = CreateParticipant(
                bingoEvent.Id, "External Clan Captain", 1, SignupStatus.Confirmed, 100,
                now.AddDays(-7), SignupSource.AdminCreated);
            var externalMember = CreateParticipant(
                bingoEvent.Id, "External Clan Member", 1, SignupStatus.Confirmed, 101,
                now.AddDays(-7), SignupSource.AdminCreated);
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

    private void AddDraftPublication(BingoEvent bingoEvent, DateTimeOffset now)
    {
        var session = db.DraftSessions.Local.Single(value => value.EventId == bingoEvent.Id);
        var cycle = new DraftPublicationCycle(Guid.NewGuid(), session.Id, 1, now, bingoEvent.CreatedByAccountId);
        db.DraftPublicationCycles.Add(cycle);

        var picks = db.DraftPicks.Local
            .Where(value => value.DraftSessionId == session.Id && value.UndoneAt is null)
            .ToDictionary(value => value.Id);
        var teams = db.Teams.Local
            .Where(value => value.EventId == bingoEvent.Id && value.Active)
            .ToDictionary(value => value.Id);
        foreach (var membership in db.TeamMemberships.Local
                     .Where(value => value.LeftAt is null && teams.ContainsKey(value.TeamId))
                     .OrderBy(value => teams[value.TeamId].Name)
                     .ThenBy(value => value.JoinedAt))
        {
            var participant = db.EventParticipants.Local.Single(value => value.Id == membership.EventParticipantId);
            int? pickNumber = membership.AssignedByDraftPickId is { } pickId && picks.TryGetValue(pickId, out var pick)
                ? pick.PickNumber
                : null;
            db.DraftPublicationRosters.Add(new DraftPublicationRoster(
                Guid.NewGuid(), cycle.Id, membership.TeamId, participant.Id, membership.Role,
                pickNumber, PrimaryName(participant)));
        }
    }

    private string AddCaptainAccount(
        BingoEvent bingoEvent,
        Team team,
        EventParticipant participant,
        string digits,
        DateTimeOffset now)
    {
        var baseName = new string(PrimaryName(participant).Where(char.IsLetterOrDigit).ToArray());
        var username = $"{baseName}{digits}";
        var account = Account.CreateEmergency(Guid.NewGuid(), username, Normalize(username), now);
        account.SetPasswordHash(passwordHasher.HashPassword(account, CaptainPassword), mustChangePassword: false);
        account.Enable();
        db.Accounts.Add(account);
        var access = new AccountEventAccess(Guid.NewGuid(), account.Id, bingoEvent.Id, team.Id, participant.Id, bingoEvent.EventStartsAt, null, null);
        access.Enable();
        db.AccountEventAccesses.Add(access);
        return username;
    }

    private void AddDisabledEmergencyCoverage(BingoEvent bingoEvent, Team team, DateTimeOffset now)
    {
        var account = Account.CreateEmergency(Guid.NewGuid(), EvidenceDisabledEmergencyUsername, EvidenceDisabledEmergencyUsername.ToUpperInvariant(), now);
        account.SetPasswordHash(passwordHasher.HashPassword(account, CaptainPassword), mustChangePassword: false);
        account.Enable();
        db.Accounts.Add(account);
        db.AccountEventAccesses.Add(new AccountEventAccess(Guid.NewGuid(), account.Id, bingoEvent.Id, team.Id, null, bingoEvent.EventStartsAt, null, now.AddDays(-1)));
    }

    private EventParticipant CreateParticipant(
        Guid eventId,
        string primaryName,
        decimal ehb,
        SignupStatus status,
        long sequence,
        DateTimeOffset signedUpAt,
        SignupSource source,
        string? secondName = null,
        bool captainVolunteer = false)
    {
        var participant = new EventParticipant(
            Guid.NewGuid(), eventId, status, sequence, signedUpAt, source);
        participant.SetCaptainVolunteer(captainVolunteer);
        var form = seedForms[eventId];
        AddAssignment(participant, primaryName, EventCharacterRole.Playing, ehb, 0, signedUpAt, form.RegularAccountQuestionId);
        if (!string.IsNullOrWhiteSpace(secondName) && Normalize(secondName) != Normalize(primaryName))
            AddAssignment(participant, secondName, EventCharacterRole.Informational, null, 1, signedUpAt, form.AltAccountQuestionId);
        db.SignupAnswers.AddRange(
            new SignupAnswer(Guid.NewGuid(), participant.Id, form.TextQuestionId, "Seeded note", $"Seeded answer for {primaryName}"),
            new SignupAnswer(Guid.NewGuid(), participant.Id, form.NumberQuestionId, "Seeded number", ehb.ToString(CultureInfo.InvariantCulture)),
            new SignupAnswer(Guid.NewGuid(), participant.Id, form.YesNoQuestionId, "Seeded yes/no", captainVolunteer ? "true" : "false"),
            new SignupAnswer(Guid.NewGuid(), participant.Id, form.ChoiceQuestionId, "Seeded choice", "North"));
        form.Form.RecordAcceptedResponse(signedUpAt);
        return participant;
    }

    private void AddSignupFoundation(BingoEvent bingoEvent, DateTimeOffset now)
    {
        var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
        db.SignupForms.Add(form);
        var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var captain = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer);
        var text = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "seeded_note", "Seeded note", SignupQuestionType.Text, false, 2, null);
        var number = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "seeded_number", "Seeded number", SignupQuestionType.Number, false, 3, null);
        var yesNo = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "seeded_yes_no", "Seeded yes/no", SignupQuestionType.YesNo, false, 4, null);
        var choice = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "seeded_choice", "Seeded choice", SignupQuestionType.SingleChoice, false, 5, "North\nSouth");
        var alt = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "seeded_alt_account", "Alt account", SignupQuestionType.Account, false, 6, null, accountAnswerRole: EventCharacterRole.Informational);
        db.SignupQuestions.AddRange(regular, captain, text, number, yesNo, choice, alt);
        seedForms.Add(bingoEvent.Id, new SeedSignupForm(form, regular.Id, alt.Id, text.Id, number.Id, yesNo.Id, choice.Id));
    }

    private void AddAssignment(
        EventParticipant participant,
        string name,
        EventCharacterRole role,
        decimal? ehb,
        int order,
        DateTimeOffset now,
        Guid signupQuestionId)
    {
        var normalized = Normalize(name);
        if (!seedCharacters.TryGetValue(normalized, out var character))
        {
            character = new OsrsCharacter(Guid.NewGuid(), name, normalized, now);
            db.OsrsCharacters.Add(character);
            seedCharacters.Add(normalized, character);
        }
        db.EventParticipantCharacters.Add(new EventParticipantCharacter(
            Guid.NewGuid(), participant.EventId, participant.Id, character.Id, order, now, null, signupQuestionId,
            role, ehb, role == EventCharacterRole.Playing ? EhbSource.Manual : null, null));
        db.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, signupQuestionId, role == EventCharacterRole.Playing ? "Account" : "Alt account", string.Empty, character.Id));
    }

    private string PrimaryName(EventParticipant participant)
    {
        var assignment = db.EventParticipantCharacters.Local
            .Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null && x.EventRole == EventCharacterRole.Playing)
            .OrderBy(x => x.RegistrationOrder)
            .First();
        return seedCharacters.Values.Single(x => x.Id == assignment.OsrsCharacterId).DisplayName;
    }

    private (Guid Id, string Name) PrimaryCharacterSnapshot(EventParticipant participant)
    {
        var assignment = db.EventParticipantCharacters.Local
            .Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null && x.EventRole == EventCharacterRole.Playing)
            .OrderBy(x => x.RegistrationOrder)
            .First();
        var character = seedCharacters.Values.Single(x => x.Id == assignment.OsrsCharacterId);
        return (character.Id, character.DisplayName);
    }

    private Account SeededCaptainAccount(Guid teamId, Guid participantId)
    {
        var access = db.AccountEventAccesses.Local.Single(value => value.TeamId == teamId && value.ParticipantId == participantId);
        return db.Accounts.Local.Single(value => value.Id == access.AccountId);
    }

    private static string CaptainDigits(BingoEvent bingoEvent, int accountNumber)
    {
        var scenarioDigits = new string(bingoEvent.Slug.Where(char.IsDigit).Take(2).ToArray());
        return $"{scenarioDigits.PadLeft(2, '0')}{accountNumber:00}";
    }

    private sealed record SeedSignupForm(SignupForm Form, Guid RegularAccountQuestionId, Guid AltAccountQuestionId, Guid TextQuestionId, Guid NumberQuestionId, Guid YesNoQuestionId, Guid ChoiceQuestionId);

    private async Task AddReviewCasesAsync(Guid eventId, Guid adminId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var board = db.Boards.Local.Single(value => value.EventId == eventId);
        var tiles = db.BoardTiles.Local.Where(value => value.BoardId == board.Id).ToDictionary(value => value.NameSnapshot);
        var team = db.Teams.Local.Single(value => value.EventId == eventId && value.Name == "Seeded Ravens");
        var membership = db.TeamMemberships.Local.First(value => value.TeamId == team.Id && value.Role == TeamMembershipRole.Captain);
        var player = db.EventParticipants.Local.Single(value => value.Id == membership.EventParticipantId);
        var captain = SeededCaptainAccount(team.Id, player.Id);

        BoardRequirementSnapshot Requirement(string tileName, int position = 1) =>
            db.BoardRequirementSnapshots.Local.Single(value => value.BoardTileId == tiles[tileName].Id && value.Position == position);
        BoardRequirementDropSnapshot Drop(BoardRequirementSnapshot requirement) =>
            db.BoardRequirementDropSnapshots.Local.First(value => value.RequirementId == requirement.Id);

        async Task<(Submission Submission, EvidenceAsset Asset)> Create(
            string tileName, int claimed, string note, byte color,
            BoardRequirementSnapshot? selectedRequirement = null,
            BoardRequirementDropSnapshot? selectedDrop = null,
            byte? sharedColor = null)
        {
            var tile = tiles[tileName];
            var requirement = selectedRequirement ?? Requirement(tileName);
            var drop = requirement.ManualObjective ? null : selectedDrop ?? Drop(requirement);
            var submittedAt = now.AddMinutes(-120 + db.Submissions.Local.Count * 3);
            var creditedCharacter = PrimaryCharacterSnapshot(player);
            var submission = new Submission(
                Guid.NewGuid(), eventId, team.Id, tile.Id, requirement.Id, drop?.Id,
                player.Id, creditedCharacter.Id, creditedCharacter.Name, captain.Id, claimed, submittedAt, note, null);
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

        var rejected = await Create("Araxxor pet or uniques", 1, "Rejected-history fixture.", 115);
        rejected.Submission.Reject("The screenshot does not show the required drop message.", now.AddMinutes(-12));
        db.ReviewActions.Add(SeedAction(rejected.Submission.Id, ReviewActionType.Reject, adminId, now.AddMinutes(-12), "The screenshot does not show the required drop message."));

        var withdrawn = await Create("Alchemical Hydra", 1, "Withdrawn captain-mistake fixture.", 135);
        withdrawn.Submission.Withdraw(now.AddMinutes(-10));
        db.ReviewActions.Add(SeedAction(withdrawn.Submission.Id, ReviewActionType.Withdraw, captain.Id, now.AddMinutes(-10), "Seeded captain withdrawal"));

        var approved = await Create("Nex", 1, "Approved visible evidence fixture.", 155);
        ApproveSeeded(approved.Submission, adminId, 1, now.AddMinutes(-8));

        var theatreRequirement = Requirement("Theatre of Blood megarares");
        var theatreDrop = Drop(theatreRequirement);
        var weightedFive = await Create("Theatre of Blood megarares", 5, "Approved weight 5; reverse this to test rebalancing.", 195, selectedRequirement: theatreRequirement, selectedDrop: theatreDrop);
        ApproveSeeded(weightedFive.Submission, adminId, 5, now.AddMinutes(-6));
        var cappedOne = await Create("Theatre of Blood megarares", 5, "Claimed 5 but capped to 1 while the requirement is full.", 215, selectedRequirement: theatreRequirement, selectedDrop: theatreDrop);
        ApproveSeeded(cappedOne.Submission, adminId, 1, now.AddMinutes(-5));

        // Finish the remainder of row one so Milestone 7 has a visible completed-line fixture.
        foreach (var position in new[] { 1, 2 })
        {
            var chestRequirement = Requirement("Barrows / Moons", position);
            var chestDrop = Drop(chestRequirement);
            for (var count = 0; count < 5; count++)
            {
                var chest = await Create(
                    "Barrows / Moons", 1, "Approved public-board line fixture.",
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

        // Add enough ranked teams to exercise two full rows in the flagship public overview.
        // Progress remains evidence-backed so the cards use the same calculation path as real events.
        var extraParticipants = db.EventParticipants.Local
            .Where(value => value.EventId == eventId)
            .OrderBy(value => value.SignupSequence)
            .Skip(6)
            .Take(3)
            .ToList();
        var extraTeams = new[]
        {
            new Team(Guid.NewGuid(), eventId, "Azure Owls", "azure-owls", TeamFormationType.Drafted, null, true),
            new Team(Guid.NewGuid(), eventId, "Ember Foxes", "ember-foxes", TeamFormationType.Drafted, null, true),
            new Team(Guid.NewGuid(), eventId, "Iron Jackals", "iron-jackals", TeamFormationType.Drafted, null, true)
        };
        var bingoEvent = db.Events.Local.Single(value => value.Id == eventId);
        for (var index = 0; index < extraTeams.Length; index++)
        {
            var extraTeam = extraTeams[index];
            var extraPlayer = extraParticipants[index];
            extraTeam.Finalize(now.AddHours(-1));
            db.Teams.Add(extraTeam);
            db.TeamMemberships.Add(new TeamMembership(
                Guid.NewGuid(), extraTeam.Id, extraPlayer.Id, TeamMembershipRole.Captain,
                now.AddHours(-1), null, "Public overview layout fixture"));
            AddCaptainAccount(bingoEvent, extraTeam, extraPlayer, CaptainDigits(bingoEvent, index + 4), now);
        }

        var seedColor = 90;
        async Task ApproveProgressAsync(Team progressTeam, EventParticipant progressPlayer, string tileName, int? amount = null)
        {
            var progressCaptain = SeededCaptainAccount(progressTeam.Id, progressPlayer.Id);
            var progressTile = tiles[tileName];
            foreach (var progressRequirement in db.BoardRequirementSnapshots.Local
                         .Where(value => value.BoardTileId == progressTile.Id)
                         .OrderBy(value => value.Position))
            {
                var approvedAmount = amount ?? progressRequirement.TargetContribution;
                var progressDrop = progressRequirement.ManualObjective
                    ? null
                    : db.BoardRequirementDropSnapshots.Local.First(value => value.RequirementId == progressRequirement.Id);
                var submittedAt = now.AddMinutes(-45 + seedColor % 20);
                var creditedCharacter = PrimaryCharacterSnapshot(progressPlayer);
                var progressSubmission = new Submission(
                    Guid.NewGuid(), eventId, progressTeam.Id, progressTile.Id, progressRequirement.Id, progressDrop?.Id,
                    progressPlayer.Id, creditedCharacter.Id, creditedCharacter.Name, progressCaptain.Id, approvedAmount, submittedAt,
                    "Approved public-overview ranking fixture.", null);
                var progressStored = await StoreSeedImageAsync(
                    eventId, progressSubmission.Id, $"ranking-{progressTeam.Slug}-{progressTile.RowIndex}-{progressTile.ColumnIndex}.png",
                    (byte)seedColor, cancellationToken);
                db.Submissions.Add(progressSubmission);
                db.EvidenceAssets.Add(SeedAsset(
                    progressSubmission.Id, progressCaptain.Id, progressStored,
                    EvidenceAssetRole.OriginalEvidence, submittedAt));
                db.ReviewActions.Add(SeedAction(
                    progressSubmission.Id, ReviewActionType.Submitted, progressCaptain.Id,
                    submittedAt, "Seeded public-overview ranking evidence"));
                ApproveSeeded(progressSubmission, adminId, approvedAmount, now.AddMinutes(-2));
                seedColor += 17;
            }
        }

        await ApproveProgressAsync(extraTeams[0], extraParticipants[0], "Alchemical Hydra");
        await ApproveProgressAsync(extraTeams[0], extraParticipants[0], "Zulrah unique table");
        await ApproveProgressAsync(extraTeams[0], extraParticipants[0], "Araxxor pet or uniques");
        await ApproveProgressAsync(extraTeams[1], extraParticipants[1], "Alchemical Hydra");
        await ApproveProgressAsync(extraTeams[1], extraParticipants[1], "Zulrah unique table");
        await ApproveProgressAsync(extraTeams[2], extraParticipants[2], "Alchemical Hydra");

        var wolves = db.Teams.Local.Single(value => value.EventId == eventId && value.Name == "Seeded Wolves");
        var wolvesMembership = db.TeamMemberships.Local.First(value =>
            value.TeamId == wolves.Id && value.Role == TeamMembershipRole.Captain);
        var wolvesPlayer = db.EventParticipants.Local.Single(value => value.Id == wolvesMembership.EventParticipantId);
        await ApproveProgressAsync(wolves, wolvesPlayer, "Nex", 1);
    }

    private async Task AddCompletedBoardAsync(Guid eventId, Guid adminId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var board = db.Boards.Local.Single(value => value.EventId == eventId);
        var team = db.Teams.Local.Single(value => value.EventId == eventId && value.Name == "Seeded Ravens");
        var membership = db.TeamMemberships.Local.First(value => value.TeamId == team.Id && value.Role == TeamMembershipRole.Captain);
        var player = db.EventParticipants.Local.Single(value => value.Id == membership.EventParticipantId);
        var captain = SeededCaptainAccount(team.Id, player.Id);
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
                    var creditedCharacter = PrimaryCharacterSnapshot(player);
                    var submission = new Submission(
                        Guid.NewGuid(), eventId, team.Id, tile.Id, requirement.Id, drop?.Id,
                        player.Id, creditedCharacter.Id, creditedCharacter.Name, captain.Id, 1, submittedAt,
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
            new SeedTile("Barrows / Moons", null,
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
        return await BuildBlueprintAsync("Canonical edge-case board", 3, 4, specifications, cancellationToken);
    }

    private async Task<BoardBlueprint> BuildDklBlueprintAsync(CancellationToken cancellationToken)
    {
        var specifications = new[]
        {
            new SeedTile("Nex", null,
            [
                new(["Nex"], 3, true, false, null, "Collect 3 Nex uniques; the pet does not count",
                    IncludedItems: ["Ancient hilt", "Nihil horn", "Torva full helm (damaged)", "Torva platebody (damaged)", "Torva platelegs (damaged)", "Zaryte vambraces"])
            ]),
            new SeedTile("Royal Titans", null,
            [
                new(["The Royal Titans"], 3, true, false, null, "Collect 3 fire staff crowns", IncludedItems: ["Fire element staff crown"]),
                new(["The Royal Titans"], 3, true, false, null, "Collect 3 ice staff crowns", IncludedItems: ["Ice element staff crown"])
            ]),
            new SeedTile("God Wars", null,
            [
                new(["General Graardor", "Kree'Arra", "K'ril Tsutsaroth", "Commander Zilyana"], 10, false, false, null,
                    "Collect 10 different eligible God Wars drops; pets count as ordinary distinct drops and no joker is used.",
                    IncludedItems:
                    [
                        "Bandos boots", "Bandos chestplate", "Bandos hilt", "Bandos tassets", "Pet general graardor",
                        "Armadyl chainskirt", "Armadyl chestplate", "Armadyl helmet", "Armadyl hilt", "Pet kree'arra",
                        "Staff of the dead", "Steam battlestaff", "Zamorak hilt", "Zamorakian spear", "Pet k'ril tsutsaroth",
                        "Armadyl crossbow", "Saradomin hilt", "Saradomin sword", "Saradomin's light", "Pet zilyana"
                    ])
            ]),
            new SeedTile("Wilderness Boss", null,
            [
                new(["Artio", "Callisto"], 1, true, false, null, "Collect a Voidwaker hilt", IncludedItems: ["Voidwaker hilt"]),
                new(["Calvar'ion", "Vet'ion"], 1, true, false, null, "Collect a Voidwaker blade", IncludedItems: ["Voidwaker blade"]),
                new(["Spindel", "Venenatis"], 1, true, false, null, "Collect a Voidwaker gem", IncludedItems: ["Voidwaker gem"])
            ]),
            new SeedTile("Duke / Whisperer", null,
            [
                new(["Duke Sucellus", "The Whisperer"], 2, true, false, null,
                    "Collect any 2 Eye of the duke or Siren's staff drops; duplicates are allowed",
                    IncludedItems: ["Eye of the duke", "Siren's staff"])
            ]),
            new SeedTile("Araxxor", 25m,
            [
                new(["Araxxor"], 1, true, false, null,
                    "Collect Nid using the destroy option, or Jar of venom.",
                    IncludedItems: ["Nid (Destroy)", "Jar of venom"])
            ]),
            new SeedTile("Phosani's Nightmare", null,
            [
                new(["Phosani's Nightmare"], 3, true, false, null, "Collect 3 eligible uniques; the pet does not count",
                    IncludedItems:
                    [
                        "Eldritch orb", "Harmonised orb", "Volatile orb", "Inquisitor's great helm", "Inquisitor's hauberk",
                        "Inquisitor's plateskirt", "Inquisitor's mace", "Nightmare staff"
                    ])
            ]),
            new SeedTile("Yama", null,
            [
                new(["Yama"], 4, true, false, null, "Collect 4 Oathplate armour pieces or Soulflame horns",
                    IncludedItems: ["Oathplate chest", "Oathplate helm", "Oathplate legs", "Soulflame horn"])
            ]),
            new SeedTile("The Hueycoatl", null,
            [
                new(["The Hueycoatl"], 2, true, false, null, "Collect 2 Dragon hunter wands", IncludedItems: ["Dragon hunter wand"])
            ]),
            new SeedTile("Vorkath", null,
            [
                new(["Vorkath"], 1, true, false, null, "Collect a Dragonbone necklace, either visage, or Vorki",
                    IncludedItems: ["Dragonbone necklace", "Draconic visage", "Skeletal visage", "Vorki"])
            ]),
            new SeedTile("Sarachnis", null,
            [
                new(["Sarachnis"], 3, true, false, null, "Collect 3 Sarachnis cudgels", IncludedItems: ["Sarachnis cudgel"])
            ]),
            new SeedTile("Grotesque Guardians", null,
            [
                new(["Grotesque Guardians"], 2, true, false, null, "Collect 2 Granite hammers", IncludedItems: ["Granite hammer"])
            ]),
            new SeedTile("Theatre of Blood", null,
            [
                new(["Theatre of Blood"], 6, true, true, null, "Collect 6 purples; Scythe of vitur counts for 2",
                    IncludedItems:
                    [
                        "Avernic defender hilt", "Ghrazi rapier", "Justiciar chestguard", "Justiciar faceguard", "Justiciar legguards",
                        "Sanguinesti staff (uncharged)", "Scythe of vitur (uncharged)"
                    ],
                    WeightTwoItems: ["Scythe of vitur (uncharged)"])
            ]),
            new SeedTile("Superior Slayer", 21,
            [
                new([], 4, true, false, null, "Collect Imbued heart, Eternal gem, Mist battlestaff, or Dust battlestaff", Manual: true,
                    IncludedItems: ["Imbued heart", "Eternal gem", "Mist battlestaff", "Dust battlestaff"])
            ]),
            new SeedTile("Corp", null,
            [
                new(["Corporeal Beast"], 3, true, false, null, "Collect 3 Spirit shields", IncludedItems: ["Spirit shield"])
            ]),
            new SeedTile("Maggot King", null,
            [
                new(["Maggot King"], 5, true, false, null, "Collect 5 eligible Maggot King drops; Maggot marquess is excluded",
                    IncludedItems: ["Crimson kisten", "Elder venator fang"])
            ]),
            new SeedTile("Alchemical Hydra", null,
            [
                new(["Alchemical Hydra"], 1, true, false, null, "Collect Hydra's claw, Ikkle hydra, or Jar of chemicals",
                    IncludedItems: ["Hydra's claw", "Ikkle hydra", "Jar of chemicals"])
            ]),
            new SeedTile("Zulrah", null,
            [
                new(["Zulrah"], 5, true, false, null, "Collect 5 eligible uniques; the pet and mutagens do not count",
                    IncludedItems: ["Magic fang", "Serpentine visage", "Tanzanite fang", "Uncut onyx"])
            ]),
            new SeedTile("Tombs of Amascut", null,
            [
                new(["Tombs of Amascut (Expert Mode)"], 6, true, true, null,
                    "Complete level-300 Expert ToA and collect 6 purples; Tumeken's shadow counts for 2",
                    IncludedItems:
                    [
                        "Elidinis' ward", "Lightbearer", "Masori body", "Masori chaps", "Masori mask", "Osmumten's fang",
                        "Tumeken's shadow (uncharged)"
                    ],
                    WeightTwoItems: ["Tumeken's shadow (uncharged)"])
            ]),
            new SeedTile("Doom", null,
            [
                new(["Doom of Mokhaiotl"], 2, true, false, null,
                    "Collect 2 eligible Doom uniques.",
                    IncludedItems: ["Avernic treads", "Eye of ayak (uncharged)", "Mokhaiotl cloth"])
            ]),
            new SeedTile("Fortis Colosseum", null,
            [
                new(["Sol Heredit"], 6, true, false, null, "Defeat Sol Heredit and collect 6 Sunfire armour pieces",
                    IncludedItems: ["Sunfire fanatic chausses", "Sunfire fanatic cuirass", "Sunfire fanatic helm"])
            ]),
            new SeedTile("Leviathan / Vardorvis", null,
            [
                new(["The Leviathan", "Vardorvis"], 3, true, false, null, "Collect 3 Chromium ingots", IncludedItems: ["Chromium ingot"])
            ]),
            new SeedTile("Barrows / Moons", null,
            [
                new(["Barrows Chests"], 5, true, false, null, "Collect 5 Barrows equipment pieces"),
                new(["Lunar Chests"], 5, true, false, null, "Collect 5 Moons equipment pieces")
            ]),
            new SeedTile("Cerberus", null,
            [
                new(["Cerberus"], 2, true, false, null, "Collect 2 Primordial crystals", IncludedItems: ["Primordial crystal"])
            ]),
            new SeedTile("Chambers of Xeric", null,
            [
                new(["Chambers of Xeric"], 6, true, true, null, "Collect 6 purples; megarares count for 2",
                    IncludedItems:
                    [
                        "Ancestral hat", "Ancestral robe bottom", "Ancestral robe top", "Arcane prayer scroll", "Dexterous prayer scroll",
                        "Dinh's bulwark", "Dragon claws", "Dragon hunter crossbow", "Elder maul", "Kodai insignia", "Twisted bow", "Twisted buckler"
                    ],
                    WeightTwoItems: ["Elder maul", "Kodai insignia", "Twisted bow"])
            ])
        };
        return await BuildBlueprintAsync("DKL comparison board", 5, 5, specifications, cancellationToken);
    }

    private async Task<BoardBlueprint> BuildBlueprintAsync(
        string name,
        int rows,
        int columns,
        IReadOnlyList<SeedTile> specifications,
        CancellationToken cancellationToken)
    {
        var bossNames = specifications.SelectMany(specification => specification.Requirements).SelectMany(requirement => requirement.Bosses).Distinct().ToList();
        var bosses = await db.BossActivities.AsNoTracking().Where(boss => bossNames.Contains(boss.Name)).ToListAsync(cancellationToken);
        var bossIds = bosses.Select(boss => boss.Id).ToList();
        var dropRows = await (from drop in db.SourceDrops.AsNoTracking()
                              join item in db.CatalogueItems.AsNoTracking() on drop.ItemId equals item.Id
                              join boss in db.BossActivities.AsNoTracking() on drop.BossActivityId equals boss.Id
                              where bossIds.Contains(boss.Id) && drop.Active && item.Active
                              select new { drop, item, boss }).ToListAsync(cancellationToken);
        var tiles = new List<TileBlueprint>();
        for (var index = 0; index < specifications.Count; index++)
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
                    (requirement.IncludedItems is not null
                        ? requirement.IncludedItems.Contains(row.item.Name, StringComparer.OrdinalIgnoreCase)
                        : requirement.ItemNameContains is null || row.item.Name.Contains(requirement.ItemNameContains, StringComparison.OrdinalIgnoreCase))).ToList();
                if (requirement.Manual && requirement.IncludedItems is { Length: > 0 })
                {
                    var manualDrops = requirement.IncludedItems
                        .Select(itemName => new DropBlueprint(Guid.NewGuid(), "Historical item pool", itemName, "Historical item pool", null, null, null))
                        .ToList();
                    requirements.Add(new RequirementBlueprint(
                        requirementIndex + 1, requirement.Target, requirement.Duplicates, requirement.HigherWeights,
                        requirement.Description, true, [], manualDrops));
                    estimates.Add(null);
                    continue;
                }
                if (!requirement.Manual && selectedDrops.Count == 0)
                {
                    throw new InvalidOperationException($"The retained catalogue has no eligible drops for seeded tile '{specification.Name}'.");
                }
                var selectedItemNames = selectedDrops.Select(row => row.item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var missingItems = requirement.IncludedItems?.Where(item => !selectedItemNames.Contains(item)).ToList() ?? [];
                if (missingItems.Count > 0)
                {
                    throw new InvalidOperationException($"The retained catalogue is missing seeded drops for '{specification.Name}': {string.Join(", ", missingItems)}.");
                }
                var selectedDropRates = selectedDrops.Select(row => new { row, Probability = row.drop.NumericProbability }).ToList();
                requirements.Add(new RequirementBlueprint(
                    requirementIndex + 1, requirement.Target, requirement.Duplicates, requirement.HigherWeights,
                    requirement.Description, requirement.Manual,
                    selectedBosses.Select(boss => new BossBlueprint(boss.Id, boss.Name, boss.EfficientCompletionsPerHour)).ToList(),
                    selectedDropRates.Select(value => new DropBlueprint(
                        value.row.drop.Id, value.row.boss.Name, value.row.item.Name, value.row.drop.DisplayRate,
                        value.Probability, requirement.Duplicates ? null : 1, value.row.drop.DefaultEhbEstimate,
                        requirement.WeightTwoItems?.Contains(value.row.item.Name, StringComparer.OrdinalIgnoreCase) == true ? 2 : 1)).ToList()));
                estimates.Add(requirement.Manual
                    ? null
                    : EhbCalculator.CalculateDropRequirement(
                        requirement.Target,
                        selectedDropRates.Select(value => new EligibleDropRate(
                            value.row.boss.EfficientCompletionsPerHour, value.Probability, value.row.drop.ItemId,
                            value.row.boss.Id,
                            requirement.WeightTwoItems?.Contains(value.row.item.Name, StringComparer.OrdinalIgnoreCase) == true ? 2 : 1,
                            value.row.drop.RollsPerCompletion, value.row.drop.RollGroup)),
                        requirement.Duplicates));
            }
            var description = string.Join("; ", specification.Requirements.Select(requirement => requirement.Description));
            var ehb = EhbCalculator.SumRequirements(estimates, specification.ManualEhb);
            tiles.Add(new TileBlueprint(
                index / columns, index % columns, specification.Name, description,
                "Submit one screenshot showing the player name and game message.",
                Math.Max(1, ehb), requirements));
        }
        return new BoardBlueprint(name, rows, columns, tiles);
    }

    private Task<int> ClearWorkflowDataAsync(CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                team_focus_markers, event_participant_character_swaps,
                official_placements, event_finalizations, final_review_resolutions, team_completion_corrections,
                submission_contributions, review_actions, evidence_assets, submissions, evidence_codes,
                draft_publication_rosters, draft_publication_cycles, team_membership_role_transitions,
                team_legacy_image_references, team_image_assets,
                draft_picks, team_memberships, draft_sessions, teams,
                board_approval_requirement_drop_snapshots,
                board_approval_requirement_boss_snapshots, board_approval_requirement_snapshots,
                board_approval_tile_snapshots, board_approval_snapshots,
                board_requirement_drop_snapshots, board_requirement_boss_snapshots, board_requirement_snapshots,
                board_tile_image_assets, board_tiles, template_requirement_drops, template_requirement_bosses, tile_template_requirements,
                tile_templates, boards, signup_answers, event_participant_characters, signup_questions, signup_forms, event_participants,
                scheduled_signup_opening_attempts, scheduled_event_start_attempts, event_state_transitions, event_banner_cleanups, events, audit_entries, personal_notifications,
                account_event_accesses, password_credential_tokens, account_discord_identity_transitions,
                waiting_list_promotion_follow_ups,
                event_competition_character_activity, event_competition_synchronizations
            RESTART IDENTITY;
            DELETE FROM accounts WHERE account_type = 'EmergencyCaptain';
            """,
            cancellationToken);

    private static BoardBlueprint ExpandBlueprint(
        BoardBlueprint source,
        string name,
        int rows,
        int columns)
    {
        var tileCount = rows * columns;
        var tiles = Enumerable.Range(0, tileCount)
            .Select(index =>
            {
                var sourceTile = source.Tiles[index % source.Tiles.Count];
                var repetition = index / source.Tiles.Count;
                return sourceTile with
                {
                    Row = index / columns,
                    Column = index % columns,
                    Name = repetition == 0
                        ? sourceTile.Name
                        : $"{sourceTile.Name} — Layout {repetition + 1}"
                };
            })
            .ToList();
        return new BoardBlueprint(name, rows, columns, tiles);
    }

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
        ScenarioStage.Archived => "official placements are archived and available for unfinalization-history testing",
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
        Archived = 11,
        CompletedFinalReview = 12
    }

    private enum DraftSeedState { Setup, Running, Finalized }
    private sealed record SeedTile(string Name, decimal? ManualEhb, SeedRequirement[] Requirements);
    private sealed record SeedRequirement(
        string[] Bosses,
        int Target,
        bool Duplicates,
        bool HigherWeights,
        string? ItemNameContains,
        string Description,
        bool Manual = false,
        string[]? IncludedItems = null,
        string[]? WeightTwoItems = null);
    private sealed record BoardBlueprint(string Name, int Rows, int Columns, IReadOnlyList<TileBlueprint> Tiles);
    private sealed record TileBlueprint(int Row, int Column, string Name, string Description, string EvidenceInstructions, decimal Ehb, IReadOnlyList<RequirementBlueprint> Requirements);
    private sealed record RequirementBlueprint(int Position, int Target, bool Duplicates, bool HigherWeights, string Description, bool Manual, IReadOnlyList<BossBlueprint> Bosses, IReadOnlyList<DropBlueprint> Drops);
    private sealed record BossBlueprint(Guid Id, string Name, decimal? Rate);
    private sealed record DropBlueprint(Guid Id, string Boss, string Item, string DisplayRate, decimal? Probability, int? Maximum, decimal? Ehb, int Weight = 1);
}

public sealed record SeedResult(
    string AdminUsername,
    string SecondaryAdminUsername,
    string SecondaryAdminPassword,
    string BoardBlueprint,
    IReadOnlyList<SeededScenario> Scenarios,
    string CaptainPassword,
    string ReplacementUsername,
    string ReplacementPassword);

public sealed record SeededScenario(
    Guid EventId,
    string EventName,
    EventState EventState,
    BoardState? BoardState,
    IReadOnlyList<string> CaptainUsernames);
