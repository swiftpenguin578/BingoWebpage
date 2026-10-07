using Bingo.Application.Evidence;
using Bingo.Application.Events;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Events;
using Bingo.Domain.Catalogue;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Bingo.Web.TestData;

/// <summary>Synthetic review data for a fresh, isolated Development database.</summary>
public sealed class UiReviewScenarioSeeder(
    ApplicationDbContext db, IWebHostEnvironment environment,
    IPasswordHasher<Account> passwords, IEvidenceStorage storage, TimeProvider time,
    IEventReadinessEvaluator signupReadiness, IEventLifecycleService lifecycle)
{
    public const string Password = "ReviewOnly!1234";
    private static readonly string[] TileNames = ["Fire cape", "Quest milestone", "Clue collection", "Achievement diary"];
    private readonly Dictionary<string, Account> accounts = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, OsrsCharacter> characters = [];

    public async Task<UiReviewScenarios> SeedAsync(string profile, CancellationToken ct = default)
    {
        if (!environment.IsDevelopment()) throw new InvalidOperationException("UI review scenarios require Development.");
        if (profile is not ("live" or "final-review")) throw new ArgumentException("Use live or final-review.", nameof(profile));
        if (await db.Events.AnyAsync(ct) || await db.Accounts.AnyAsync(ct))
            throw new InvalidOperationException("UI review seeding requires an empty scenario database; use the owned refresh command.");
        if (!await db.SourceDrops.AnyAsync(ct)) throw new InvalidOperationException("Apply the catalogue snapshot first.");
        // PostgreSQL stores microseconds. Keep every derived fixture instant exact at that precision.
        var current = time.GetUtcNow();
        var now = new DateTimeOffset(current.Ticks - current.Ticks % 10, TimeSpan.Zero);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        AddAccount("ReviewOwner", GlobalRole.SuperAdmin, now);
        AddAccount("ReviewAdmin", GlobalRole.Admin, now);
        foreach (var name in new[] { "ReviewCaptain", "ReviewCoCaptain", "ReviewParticipant", "ReviewWebsite", "ReviewDisabled", "ReviewFormer", "ReviewSecondCaptain", "ReviewSecondCoCaptain", "ReviewSecondMember" })
            AddAccount(name, GlobalRole.User, now);
        accounts["ReviewDisabled"].Disable(now.AddDays(-1), accounts["ReviewOwner"].Id, "Synthetic disabled-account review.");
        await db.SaveChangesAsync(ct);
        var events = new List<BingoEvent>();
        var privateSetup = await AddEventAsync("Private setup", "ur-draft", EventState.Draft, now, -2, ct);
        events.Add(privateSetup);

        events.Add(await AddEventAsync("Signups open", "ur-signups-open", EventState.SignupOpen, now, 15, ct));
        events.Add(await AddEventAsync("Signups closed — finalized affiliated rosters", "ur-signups-closed", EventState.SignupClosed, now, 16, ct, roster: true));
        events.Add(await AddEventAsync("Unknown timezone", "ur-unknown-timezone", EventState.SignupClosed, now, 17, ct, timezone: "Review/Unknown"));
        for (var index = 1; index <= 22; index++)
        {
            var name = index switch
            {
                10 => "alpha", 11 => "Alpha", 12 => "Ægir", 13 => "Ørn", 14 => "År",
                15 => "No capacity limit", 16 => "No dates configured", _ => $"Upcoming setup {index:00}"
            };
            events.Add(await AddEventAsync(name, $"ur-upcoming-{index:00}", EventState.Draft, now, 20 + index, ct,
                noCapacity: index == 15, noDates: index == 16));
        }
        var failedOpening = await AddEventAsync("Signup opening failed", "ur-opening-failed", EventState.Draft, now, 80, ct, openingFailed: true);
        events.Add(failedOpening);


        // Reach and retain historical states before creating the sole visible current event.
        var archived = await AddEventAsync("Archived — affiliated roster history", "ur-archived", EventState.Archived, now, -14, ct, roster: true, sharedFirst: true);
        events.Add(archived);
        var unavailableHistory = await AddEventAsync("Archived — WOM end could not update", "ur-wom-unavailable", EventState.Archived, now, -35, ct, roster: true);
        events.Add(unavailableHistory);
        events.Add(await AddImportedHistoryAsync(now, ct));
        foreach (var (name, slug, state) in new[] {
            ("Hidden final review", "ur-hidden-review", EventState.AwaitingFinalReview),
            ("Hidden legacy Finalized", "ur-hidden-finalized", EventState.Finalized),
            ("Hidden Archived", "ur-hidden-archived", EventState.Archived) })
        {
            var hidden = await AddEventAsync(name, slug, state, now, -21, ct, roster: true);
            var beforeHide = new { hidden.State, hidden.Version, hidden.HiddenAt, hidden.HiddenByAccountId, hidden.HiddenReason };
            hidden.Hide(accounts["ReviewOwner"].Id, now.AddHours(-2), hidden.Name, "Synthetic hidden-history review.");
            // Quarantine records its after snapshot after persistence has advanced Version.
            await db.SaveChangesAsync(ct);
            Audit(hidden, "event.hidden", now.AddHours(-2), beforeHide,
                new { hidden.State, hidden.Version, hidden.HiddenAt, hidden.HiddenByAccountId, hidden.HiddenReason }, hidden.HiddenReason);
            events.Add(hidden);
        }
        var cancelled = await AddEventAsync("Cancelled with signup history", "ur-cancelled", EventState.SignupOpen, now, 18, ct);
        AddConfirmedSignups(cancelled, now.AddDays(-2));
        cancelled.Cancel(accounts["ReviewOwner"].Id, now.AddDays(-1), "Synthetic event called off after signups opened.", protectedHistoryExists: true);
        History(cancelled, EventState.SignupOpen, "event.cancelled", now.AddDays(-1), new { state = cancelled.State }, cancelled.CancellationReason);
        events.Add(cancelled);
        var discarded = await AddEventAsync("Discarded private setup — excluded", "ur-discarded", EventState.Draft, now, 19, ct);
        discarded.Discard(accounts["ReviewOwner"].Id, now, protectedHistoryExists: false);
        History(discarded, EventState.Draft, "event.discarded", now, new { state = discarded.State }, "Empty event setup discarded");
        events.Add(discarded);
        var active = await AddEventAsync(profile == "live" ? "Live — published board correction" : "Final review — published board correction",
            "ur-current", profile == "live" ? EventState.Live : EventState.AwaitingFinalReview, now, -1, ct, roster: true);
        events.Add(active);
        var board = db.Boards.Local.Single(value => value.EventId == active.Id);
        active.EnsurePublishedBoardCorrectionAllowed();
        board.BeginPublishedCorrection();
        var tile = db.BoardTiles.Local.First(value => value.BoardId == board.Id);
        tile.UpdateContent(tile.NameSnapshot, "Exceptional correction working copy; public approval stays unchanged.", tile.EvidenceInstructionsSnapshot, tile.EstimatedEhbSnapshot);
        board.MarkChanged();
        const string correctionReason = "Synthetic exceptional correction review.";
        BoardAudit(board, "board.published_correction_started", now.AddMinutes(-10), correctionReason,
            new { activeApprovalSnapshotId = board.ActiveApprovalSnapshotId },
            new { activeApprovalSnapshotId = board.ActiveApprovalSnapshotId, workingCopy = true, reason = correctionReason });
        var blocked = await AddBlockedReviewAsync(active, now, ct);
        AddEndOutcome(active, EventCompetitionEndUpdateStatus.Pending, now, 91001);
        AddEndOutcome(archived, EventCompetitionEndUpdateStatus.Rejected, now, 91002);
        AddEndOutcome(unavailableHistory, EventCompetitionEndUpdateStatus.CouldNotUpdate, now, 91003);
        await db.SaveChangesAsync(ct);
        await AddScheduledAttentionAsync(privateSetup, failedOpening, now.AddHours(-1), ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new UiReviewScenarios(profile, now, active.Id, discarded.Id, blocked,
            events.Select(value => new UiReviewEvent(value.Id, value.Name, value.Slug, value.State, value.IsHidden)).ToArray(),
            accounts.Values.Select(value => new UiReviewAccount(value.LoginName, value.GlobalRole!.Value, value.DisabledAt is not null, value.Id)).ToArray());
    }

    private async Task AddScheduledAttentionAsync(BingoEvent postponed, BingoEvent failed, DateTimeOffset at, CancellationToken ct)
    {
        // Use the production evaluators for the actual synthetic field state.
        // Persist only the rows written by their failed scheduled-execution paths.
        var opening = (await signupReadiness.GetSignupReadinessAsync(failed.Id, SignupOpeningMode.ScheduledExecution, at, ct))!;
        var overlap = await EventSignupLifecycleService.CurrentEventBoundaryConflictAsync(db, failed, ct);
        var openingBlockers = opening.Blockers.Concat(overlap is null ? [] : [overlap]).ToArray();
        var openingCodes = openingBlockers.Select(value => value.Code).ToArray();
        var descriptions = openingBlockers.Select(value => value.Description).ToArray();
        failed.ConfigureScheduledSignupOpening(false, []);
        db.ScheduledSignupOpeningAttempts.Add(new ScheduledSignupOpeningAttempt(Guid.NewGuid(), failed.Id,
            failed.SignupOpensAt!.Value, at, false, openingCodes, descriptions));
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), at, null, "System", "event.signup_opening_failed", "event",
            failed.Id.ToString(), JsonSerializer.Serialize(new { scheduledFor = failed.SignupOpensAt.Value, blockerCodes = openingCodes }), failed.Id));
        NotifyAdmins(failed, "Scheduled signup opening failed", descriptions, at);
        var start = (await lifecycle.GetStartReadinessAsync(postponed.Id, ct))!;
        var startCodes = start.Blockers.Select(value => value.Code).ToArray();
        db.ScheduledEventStartAttempts.Add(new ScheduledEventStartAttempt(Guid.NewGuid(), postponed.Id,
            postponed.EventStartsAt!.Value, at, false, startCodes));
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), at, null, "System", "event.start_postponed", "event",
            postponed.Id.ToString(), JsonSerializer.Serialize(new { scheduledFor = postponed.EventStartsAt.Value, blockerCodes = startCodes }), postponed.Id));
        NotifyAdmins(postponed, "Automatic start postponed", start.Blockers.Select(value => value.Description), at);
    }

    private void NotifyAdmins(BingoEvent item, string title, IEnumerable<string> descriptions, DateTimeOffset at)
    {
        foreach (var recipient in accounts.Values.Where(value => value.Active && value.AccountType == AccountType.WebsiteAccount && value.GlobalRole is GlobalRole.Admin or GlobalRole.SuperAdmin))
            db.PersonalNotifications.Add(new PersonalNotification(Guid.NewGuid(), recipient.Id, title,
                $"{item.Name}: {string.Join(" ", descriptions)}", $"/Admin/Events/Manage/{item.Id}", at, item.Id));
    }

    private void AddAccount(string username, GlobalRole role, DateTimeOffset now)
    {
        var account = Account.CreateWebsite(Guid.NewGuid(), username, username.ToUpperInvariant(), now.AddDays(-90));
        account.SetGlobalRole(role);
        account.SetPassword(passwords.HashPassword(account, Password), false, now, incrementVersion: false);
        var name = username.Replace("Review", "Ur ", StringComparison.Ordinal);
        var character = new OsrsCharacter(Guid.NewGuid(), name, name.ToUpperInvariant(), now.AddDays(-90));
        db.OsrsCharacters.Add(character);
        account.CompleteOnboarding(character.Id, now.AddDays(-89));
        db.Accounts.Add(account);
        db.AccountOsrsCharacters.Add(new AccountOsrsCharacter(Guid.NewGuid(), account.Id, character.Id, account.Id, true, 0, "Synthetic review character", 25, now.AddDays(-89)));
        accounts.Add(username, account);
        characters.Add(account.Id, character);
    }

    private async Task<BingoEvent> AddEventAsync(string name, string slug, EventState state, DateTimeOffset now, int startDays,
        CancellationToken ct, bool roster = false, string timezone = "Europe/Copenhagen", bool sharedFirst = false,
        bool noCapacity = false, bool noDates = false, bool openingFailed = false)
    {
        var owner = accounts["ReviewOwner"];
        var start = now.AddDays(startDays);
        var end = startDays < -1 ? start.AddDays(5) : start.AddDays(7);
        if (state == EventState.AwaitingFinalReview && startDays == -1) end = now.AddHours(-1);
        var item = new BingoEvent(Guid.NewGuid(), name, slug, null, timezone,
            noDates ? null : openingFailed ? now.AddHours(-2) : start.AddDays(-7),
            noDates ? null : state == EventState.SignupOpen ? now.AddDays(7) : start.AddDays(-2),
            noDates ? null : start, noDates ? null : end, noDates ? null : end.AddMinutes(30),
            noCapacity ? null : 20, owner.Id, now.AddDays(-90));
        item.ConfigurePlanning("Use synthetic evidence only.", "Optional synthetic buy-in", "Local review only", 2, 3, 2, 2);
        item.ConfigureSignup(true, false, null);
        if (openingFailed) item.ConfigureScheduledSignupOpening(true, []);
        db.Events.Add(item);
        var form = new SignupForm(Guid.NewGuid(), item.Id, now.AddDays(-89));
        var question = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Playing account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        db.SignupForms.Add(form);
        db.SignupQuestions.Add(question);
        // Signup setup review uses the same standard fields as EventCreationService.
        db.SignupQuestions.AddRange(
            new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, true, 1, null, SignupSystemField.CaptainVolunteer),
            new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, SignupQuestion.CoCaptainKey, SignupQuestion.CoCaptainLabel, SignupQuestionType.Text, false, 2, null, SignupSystemField.CoCaptainName));
        var session = new DraftSession(Guid.NewGuid(), item.Id, 3);
        db.DraftSessions.Add(session);
        Audit(item, "event.created", item.CreatedAt, null,
            new { item.Name, item.Slug, Description = item.Description, item.Timezone, item.State }, "Created as a private draft.");
        if (state != EventState.Draft)
        {
            var openedAt = startDays < 0 ? start.AddDays(-7) : now.AddDays(-3);
            item.OpenSignups(openedAt);
            item.MarkFirstPublic(openedAt);
            History(item, EventState.Draft, "event.signup_opened", openedAt,
                new { state = item.State, item.ActualSignupOpenedAt, item.ActualSignupClosedAt });
            if (state != EventState.SignupOpen)
            {
                var closedAt = startDays < 0 ? start.AddDays(-2) : now.AddDays(-2);
                item.CloseSignups(closedAt);
                History(item, EventState.SignupOpen, "event.signup_closed", closedAt,
                    new { state = item.State, item.ActualSignupOpenedAt, item.ActualSignupClosedAt });
            }
        }
        if (roster) AddRosters(item, session, question.Id, now, start);
        await AddBoardAsync(item, now, state != EventState.Draft, ct);
        if (state is EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived)
        {
            item.StartEvent(start);
            await new EventItemPriceService(db, time).CaptureStartAsync(item,
                new PreparedEventItemPrices(CataloguePricing.LastCompletedHour(start), null), ct,
                new Bingo.Application.Events.LifecycleActor(owner.Id, owner.LoginName));
            History(item, EventState.SignupClosed, "event.started", start,
                new { state = item.State, item.ActualStartedAt, item.ActualEndedAt });
            if (state != EventState.Live)
            {
                item.EndEvent(end);
                var transition = History(item, EventState.Live, "event.ended", end,
                    new { state = item.State, item.ActualStartedAt, item.ActualEndedAt });
                if (state is EventState.Finalized or EventState.Archived)
                {
                    item.CloseSubmissionsIfDue(end.AddHours(1));
                    AddOfficialResults(item, transition.Id, end.AddDays(1), sharedFirst);
                    if (state == EventState.Finalized) item.FinalizeResults(end.AddDays(1));
                    else item.PublishOfficialResults(end.AddDays(1));
                    History(item, EventState.AwaitingFinalReview, "event.results_published", end.AddDays(1),
                        new { state = item.State }, "Official placements snapshotted and event archived");
                }
            }
        }
        await db.SaveChangesAsync(ct);
        return item;
    }

    private void AddRosters(BingoEvent item, DraftSession session, Guid questionId, DateTimeOffset now, DateTimeOffset start)
    {
        var at = start < now ? start.AddDays(-1) : now.AddDays(-1);
        var teams = new[] {
            new Team(Guid.NewGuid(), item.Id, "Amber Owls", "amber-owls", "Invented Amber Guild", false, at.AddHours(-12)),
            new Team(Guid.NewGuid(), item.Id, "Silver Foxes", "silver-foxes", "Invented Silver Guild", false, at.AddHours(-12)) };
        db.Teams.AddRange(teams);
        session.FinalizeDirect(at);
        item.SetDraftLocked(true, at);
        item.SetDraftRosterPublication(true);
        var cycle = new DraftPublicationCycle(Guid.NewGuid(), session.Id, 1, at, accounts["ReviewOwner"].Id, DraftPublicationMethod.DirectRoster);
        db.DraftPublicationCycles.Add(cycle);
        var names = new[] { "ReviewCaptain", "ReviewCoCaptain", "ReviewParticipant", "ReviewSecondCaptain", "ReviewSecondCoCaptain", "ReviewSecondMember", "ReviewFormer" };
        for (var index = 0; index < names.Length; index++)
        {
            var account = accounts[names[index]];
            var character = characters[account.Id];
            var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, index + 1, at.AddDays(-2), SignupSource.Website);
            participant.AssignOwner(account);
            participant.SetPaymentReceived(index != 2);
            db.EventParticipants.Add(participant);
            db.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, questionId, "Playing account", string.Empty, character.Id));
            db.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), item.Id, participant.Id, character.Id, 0, at.AddDays(-2), account.Id, questionId, EventCharacterRole.Playing, 25, EhbSource.Manual, null));
            var team = index < 3 || index == 6 ? teams[0] : teams[1];
            var role = index is 0 or 3 ? TeamMembershipRole.Captain : index is 1 or 4 ? TeamMembershipRole.CoCaptain : TeamMembershipRole.Participant;
            var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, role, at.AddHours(-6), null, "Synthetic direct roster.");
            membership.SetSource(TeamMembershipSource.PreformedManual);
            db.TeamMemberships.Add(membership);
            if (index == 6)
            {
                membership.Leave(at.AddHours(-1), "Synthetic former member before publication.");
                participant.Withdraw(at.AddHours(-1), "Synthetic former member.", account.Id);
                db.EventParticipantCharacters.Local.Single(value => value.EventParticipantId == participant.Id).Release(account.Id, at.AddHours(-1));
            }
            else db.DraftPublicationRosters.Add(new DraftPublicationRoster(Guid.NewGuid(), cycle.Id, team.Id, participant.Id, role, null, character.DisplayName));
        }
        foreach (var team in teams) { team.Finalize(at); team.LockMetadata(at); }
    }

    private async Task AddBoardAsync(BingoEvent item, DateTimeOffset now, bool publish, CancellationToken ct, bool frozenImport = false)
    {
        var board = new Board(Guid.NewGuid(), item.Id, "Synthetic community challenges", 2, 2);
        db.Boards.Add(board);
        foreach (var (name, index) in TileNames.Select((name, index) => (name, index)))
        {
            var description = $"Complete the synthetic {name.ToLowerInvariant()} objective.";
            var evidenceInstructions = frozenImport ? HistoricalImport.HistoricalEventImporter.Disclosure : "Upload an invented screenshot.";
            var template = new TileTemplate(Guid.NewGuid(), name, description, ObjectiveType.Manual, evidenceInstructions, 2);
            var tile = new BoardTile(Guid.NewGuid(), board.Id, template.Id, index / 2, index % 2, name, description, evidenceInstructions, 2);
            var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, frozenImport ? 1 : 0, 1, true, false, description, true);
            db.TileTemplates.Add(template); db.BoardTiles.Add(tile); db.BoardRequirementSnapshots.Add(requirement);
            db.TileTemplateRequirements.Add(new TileTemplateRequirement(Guid.NewGuid(), template.Id, frozenImport ? 1 : 0, 1, true, false, description, true));
        }
        board.SetTotalEhb(8);
        await db.SaveChangesAsync(ct);
        if (!publish) return;
        var approvedAt = frozenImport ? item.ActualEndedAt!.Value : (item.ActualSignupClosedAt ?? item.ActualSignupOpenedAt)!.Value.AddHours(1);
        var approval = new BoardApprovalSnapshot(Guid.NewGuid(), board.Id, 1, approvedAt, item.CreatedByAccountId, null, board.Name, board.Rows, board.Columns, board.TotalEhbEstimate, board.CalculationVersion, board.Version, BoardState.Validated);
        db.BoardApprovalSnapshots.Add(approval);
        foreach (var tile in db.BoardTiles.Local.Where(value => value.BoardId == board.Id))
        {
            var frozenTile = new BoardApprovalTileSnapshot(Guid.NewGuid(), approval.Id, tile.Id, tile.TileTemplateId, tile.RowIndex, tile.ColumnIndex, tile.NameSnapshot, tile.DescriptionSnapshot, frozenImport ? HistoricalImport.HistoricalEventImporter.Disclosure : tile.EvidenceInstructionsSnapshot, tile.EstimatedEhbSnapshot, null);
            db.BoardApprovalTileSnapshots.Add(frozenTile);
            var requirement = db.BoardRequirementSnapshots.Local.Single(value => value.BoardTileId == tile.Id);
            db.BoardApprovalRequirementSnapshots.Add(new BoardApprovalRequirementSnapshot(Guid.NewGuid(), frozenTile.Id, requirement.Id, requirement.Position, requirement.TargetContribution, requirement.DuplicatesAllowed, requirement.AllowHigherWeightings, requirement.CreditedWeight, requirement.Description, requirement.ManualObjective));
        }
        await db.SaveChangesAsync(ct);
        board.Approve(approval.Id);
        if (!frozenImport) BoardAudit(board, "board.approved", approvedAt, $"Approved snapshot {approval.Version}",
            new { state = "Draft", activeApprovalSnapshotId = (Guid?)null },
            new { state = "Validated", activeApprovalSnapshotId = approval.Id, approvalVersion = approval.Version });
        board.Publish(frozenImport ? approvedAt : approvedAt.AddHours(1));
        if (!frozenImport) BoardAudit(board, "board.published", approvedAt.AddHours(1), $"Published approval snapshot {board.ActiveApprovalSnapshotId}",
            new { state = "Validated", activeApprovalSnapshotId = board.ActiveApprovalSnapshotId },
            new { state = "Published", activeApprovalSnapshotId = board.ActiveApprovalSnapshotId });
        if (!frozenImport) item.SetBoardPublication(true, approvedAt.AddHours(1));
    }

    private void AddConfirmedSignups(BingoEvent item, DateTimeOffset at)
    {
        var question = db.SignupQuestions.Local.Single(value => value.EventId == item.Id && value.SystemField == SignupSystemField.PrimaryRegularAccount);
        var sequence = 0;
        foreach (var name in new[] { "ReviewCaptain", "ReviewCoCaptain", "ReviewParticipant" })
        {
            var account = accounts[name];
            var character = characters[account.Id];
            var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, ++sequence, at, SignupSource.Website);
            participant.AssignOwner(account);
            db.EventParticipants.Add(participant);
            db.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, question.Id, "Playing account", string.Empty, character.Id));
            db.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), item.Id, participant.Id, character.Id, 0, at,
                account.Id, question.Id, EventCharacterRole.Playing, 25, EhbSource.Manual, null));
        }
    }

    private async Task<BingoEvent> AddImportedHistoryAsync(DateTimeOffset now, CancellationToken ct)
    {
        // Separate import-shaped event; retain the accepted platform WOM-failure
        // history without attaching import metadata to its lifecycle snapshot.
        var owner = accounts["ReviewOwner"];
        var start = now.AddDays(-60);
        var end = start.AddDays(5);
        const string disclosure = HistoricalImport.HistoricalEventImporter.Disclosure;
        var item = BingoEvent.CreateArchivedHistorical(Guid.NewGuid(), "Imported — frozen synthetic history", "ur-imported",
            disclosure, "Europe/Copenhagen", start.AddDays(-7), start.AddDays(-2), start, end, owner.Id, now,
            disclosure, 2, 3, 2, 2);
        db.Events.Add(item);
        var teams = new[] {
            new Team(Guid.NewGuid(), item.Id, "Imported Amber", "imported-amber", TeamFormationType.Preformed, null, false, start),
            new Team(Guid.NewGuid(), item.Id, "Imported Silver", "imported-silver", TeamFormationType.Preformed, null, false, start) };
        foreach (var team in teams) { team.Finalize(end); db.Teams.Add(team); }
        var session = new DraftSession(Guid.NewGuid(), item.Id, 3);
        session.Start(start); session.Finalize(end); db.DraftSessions.Add(session);
        var publication = new DraftPublicationCycle(Guid.NewGuid(), session.Id, 1, end, owner.Id);
        db.DraftPublicationCycles.Add(publication);
        var names = new[] { "ReviewCaptain", "ReviewCoCaptain", "ReviewParticipant", "ReviewSecondCaptain", "ReviewSecondCoCaptain", "ReviewSecondMember" };
        var assignments = new List<EventParticipantCharacter>();
        var participants = new List<EventParticipant>();
        for (var index = 0; index < names.Length; index++)
        {
            var character = characters[accounts[names[index]].Id];
            var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, index + 1,
                start.AddMinutes(index), SignupSource.CsvImport);
            participants.Add(participant); db.EventParticipants.Add(participant);
            var team = teams[index / 3];
            db.TeamMemberships.Add(new TeamMembership(Guid.NewGuid(), team.Id, participant.Id,
                TeamMembershipRole.Participant, start, null, "Frozen historical import"));
            var assignment = new EventParticipantCharacter(Guid.NewGuid(), item.Id, participant.Id, character.Id, 0,
                start.AddMinutes(index + 1), owner.Id, null, EventCharacterRole.Playing, 25, EhbSource.Import, null);
            assignments.Add(assignment); db.EventParticipantCharacters.Add(assignment);
            db.DraftPublicationRosters.Add(new DraftPublicationRoster(Guid.NewGuid(), publication.Id, team.Id, participant.Id,
                TeamMembershipRole.Participant, null, character.DisplayName));
        }
        await AddBoardAsync(item, now, true, ct, frozenImport: true);
        var board = db.Boards.Local.Single(value => value.EventId == item.Id);
        var tile = db.BoardTiles.Local.First(value => value.BoardId == board.Id);
        var requirement = db.BoardRequirementSnapshots.Local.Single(value => value.BoardTileId == tile.Id);
        var primary = characters[accounts[names[0]].Id];
        var approvedAt = start.AddSeconds(1);
        var submission = new Submission(Guid.NewGuid(), item.Id, teams[0].Id, tile.Id, requirement.Id, null,
            participants[0].Id, primary.Id, primary.DisplayName, owner.Id, 1, start, disclosure, null);
        submission.Approve(1, approvedAt); db.Submissions.Add(submission);
        db.SubmissionContributions.Add(new SubmissionContribution(Guid.NewGuid(), submission.Id, teams[0].Id, requirement.Id, null, participants[0].Id, 1, approvedAt));
        db.ReviewActions.Add(new ReviewAction(Guid.NewGuid(), submission.Id, ReviewActionType.Submitted, owner.Id, submission.SubmittedAt, disclosure, null, null));
        db.ReviewActions.Add(new ReviewAction(Guid.NewGuid(), submission.Id, ReviewActionType.Approve, owner.Id, approvedAt, disclosure, null, null));
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('|', assignments.OrderBy(value => value.Id).Select(value => $"{value.Id:N}:{value.EventParticipantId:N}:{value.OsrsCharacterId:N}")))));
        var sync = new EventCompetitionSynchronization(Guid.NewGuid(), item.Id, 1, 91004, "Synthetic frozen import",
            start, end, fingerprint, end);
        sync.MarkHistoricalSuccess(end, end); db.EventCompetitionSynchronizations.Add(sync);
        foreach (var assignment in assignments)
            db.EventCompetitionCharacterActivities.Add(new EventCompetitionCharacterActivity(Guid.NewGuid(), item.Id, 1, 91004,
                assignment.OsrsCharacterId, 1, end, end, fingerprint, 25, 26));
        var counters = new[] { new[] { 1, 0, 0, 0 }, new[] { 0, 0, 0, 0 } };
        var manifest = JsonSerializer.Serialize(new { @event = new { sourceEventId = item.Slug, item.Name, start, end },
            teams = teams.Select((team, index) => new { team.Slug, team.Name, counters = counters[index], placement = index + 1 }) });
        var input = JsonSerializer.Serialize(new { accounts = assignments.Select((assignment, index) => new {
            participantKey = participants[index].Id, teamSlug = teams[index / 3].Slug,
            accounts = new[] { new { username = characters[accounts[names[index]].Id].DisplayName, startEhb = 25, endEhb = 26, gainedEhb = 1, fetchedAt = end, upstreamUpdatedAt = end } } }) });
        static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        var manifestHash = Hash(manifest);
        var inputHash = Hash(input);
        var importHash = Hash($"{manifestHash}:{inputHash}");
        var transition = new EventStateTransition(Guid.NewGuid(), item.Id, EventState.Draft, EventState.Archived, owner.Id, end,
            "Frozen historical import; no live lifecycle transition.");
        db.EventStateTransitions.Add(transition);
        var finalization = new EventFinalizationSnapshot(Guid.NewGuid(), item.Id, 1, end, owner.Id, transition.Id, "[]",
            JsonSerializer.Serialize(new { sourceEventId = item.Slug, manifestHash, inputHash, importHash }),
            JsonSerializer.Serialize(new { counters, placements = teams.Select((team, index) => new { team.Slug, Placement = index + 1 }) }));
        db.EventFinalizations.Add(finalization);
        foreach (var (team, index) in teams.Select((team, index) => (team, index)))
            db.OfficialPlacements.Add(new OfficialPlacementSnapshot(Guid.NewGuid(), finalization.Id, item.Id, team.Id, team.Name,
                index + 1, false, null, 0, index == 0 ? 1 : 0, 0));
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, owner.Id, owner.LoginName, "historical_import.applied", "event",
            item.Id.ToString("D"), JsonSerializer.Serialize(new { manifestHash, inputHash, importHash, sourceEventId = item.Slug,
                counts = new { teams = 2, participants = 6, accounts = 6, tiles = 4, counters = 8 } }), item.Id));
        await db.SaveChangesAsync(ct);
        return item;
    }

    private void AddOfficialResults(BingoEvent item, Guid cycle, DateTimeOffset at, bool sharedFirst)
    {
        var finalization = new EventFinalizationSnapshot(Guid.NewGuid(), item.Id, 1, at, accounts["ReviewOwner"].Id, cycle,
            calculationInputsJson: null);
        db.EventFinalizations.Add(finalization);
        var placement = 0;
        foreach (var team in db.Teams.Local.Where(value => value.EventId == item.Id).OrderBy(value => value.Name, StringComparer.Ordinal))
            db.OfficialPlacements.Add(new OfficialPlacementSnapshot(Guid.NewGuid(), finalization.Id, item.Id, team.Id, team.Name, sharedFirst ? 1 : ++placement, false, null, 0, 0, 0));
    }

    private async Task<Guid> AddBlockedReviewAsync(BingoEvent item, DateTimeOffset now, CancellationToken ct)
    {
        var participant = db.EventParticipants.Local.Single(value => value.EventId == item.Id && value.AccountId == accounts["ReviewParticipant"].Id);
        var membership = db.TeamMemberships.Local.Single(value => value.EventParticipantId == participant.Id);
        var board = db.Boards.Local.Single(value => value.EventId == item.Id);
        var tile = db.BoardTiles.Local.First(value => value.BoardId == board.Id);
        var requirement = db.BoardRequirementSnapshots.Local.Single(value => value.BoardTileId == tile.Id);
        var character = characters[accounts["ReviewParticipant"].Id];
        Guid laterId = Guid.Empty;
        for (var index = 0; index < 2; index++)
        {
            var at = item.ActualStartedAt!.Value.AddHours(1 + index);
            var submission = new Submission(Guid.NewGuid(), item.Id, membership.TeamId, tile.Id, requirement.Id, null, participant.Id, character.Id, character.DisplayName, accounts["ReviewCaptain"].Id, 1, at,
                index == 0 ? "Earlier upload reserves this objective's remaining room." : "Blocked approval: approve or reject the earlier upload first.", null);
            db.Submissions.Add(submission);
            using var image = new Image<Rgba32>(640, 360, new Rgba32((byte)(60 + index * 80), 90, 120));
            await using var stream = new MemoryStream();
            await image.SaveAsPngAsync(stream, ct); stream.Position = 0;
            var stored = await storage.StoreAsync(item.Id, submission.Id, $"synthetic-evidence-{index + 1}.png", stream, ct);
            db.EvidenceAssets.Add(new EvidenceAsset(Guid.NewGuid(), submission.Id, stored.StorageKey, stored.OriginalFilename, stored.MediaType, stored.ByteSize, stored.Width, stored.Height, stored.Checksum, at, accounts["ReviewCaptain"].Id, EvidenceAssetRole.OriginalEvidence));
            db.ReviewActions.Add(new ReviewAction(Guid.NewGuid(), submission.Id, ReviewActionType.Submitted, accounts["ReviewCaptain"].Id, at, submission.CaptainNote, null, null));
            laterId = submission.Id;
        }
        return laterId;
    }

    private void AddEndOutcome(BingoEvent item, EventCompetitionEndUpdateStatus status, DateTimeOffset now, long competitionId)
    {
        var sync = new EventCompetitionSynchronization(Guid.NewGuid(), item.Id, 1, competitionId, "Synthetic local WOM competition", item.EventStartsAt, item.EventEndsAt!.Value.AddDays(1), "synthetic-review", now, EventCompetitionProvenance.External);
        sync.RequestEndUpdate(item.EventEndsAt.Value, now);
        if (status == EventCompetitionEndUpdateStatus.Rejected) sync.RejectEndUpdate(item.EventEndsAt.Value, "SYNTHETIC_REJECTED");
        if (status == EventCompetitionEndUpdateStatus.CouldNotUpdate) sync.MarkEndCouldNotBeUpdated();
        db.EventCompetitionSynchronizations.Add(sync);
    }

    // Match EventSignupLifecycleService / EventLifecycleService / finalization and destructive
    // history rows exactly. These review operations are manual; EffectiveAt equals PerformedAt.
    private EventStateTransition History(BingoEvent item, EventState from, string action, DateTimeOffset at, object after, string? reason = null)
    {
        var transition = new EventStateTransition(Guid.NewGuid(), item.Id, from, item.State,
            accounts["ReviewOwner"].Id, at, reason, scheduled: false, effectiveAt: at);
        db.EventStateTransitions.Add(transition);
        Audit(item, action, at, new { state = from }, after, reason);
        return transition;
    }

    private void BoardAudit(Board board, string action, DateTimeOffset at, string details, object before, object after) =>
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), at, accounts["ReviewOwner"].Id, "ReviewOwner", action,
            "board", board.Id.ToString(), details, board.EventId, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after)));

    private void Audit(BingoEvent item, string action, DateTimeOffset at, object? before, object after, string? details = null) =>
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), at, accounts["ReviewOwner"].Id, "ReviewOwner", action,
            "event", item.Id.ToString(), details, item.Id, before is null ? null : JsonSerializer.Serialize(before), JsonSerializer.Serialize(after)));
}

public sealed record UiReviewEvent(Guid Id, string Name, string Slug, EventState State, bool Hidden);
public sealed record UiReviewAccount(string Username, GlobalRole Role, bool Disabled, Guid Id = default);
public sealed record UiReviewScenarios(string Profile, DateTimeOffset BuiltAt, Guid CurrentEventId, Guid DiscardedEventId, Guid BlockedSubmissionId, IReadOnlyList<UiReviewEvent> Events, IReadOnlyList<UiReviewAccount> Accounts);
