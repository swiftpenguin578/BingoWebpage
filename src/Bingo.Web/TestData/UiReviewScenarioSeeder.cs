using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
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

/// <summary>Synthetic review data for a fresh, isolated Development database.</summary>
public sealed class UiReviewScenarioSeeder(
    ApplicationDbContext db, IWebHostEnvironment environment,
    IPasswordHasher<Account> passwords, IEvidenceStorage storage, TimeProvider time)
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
        foreach (var name in new[] { "ReviewCaptain", "ReviewCoCaptain", "ReviewParticipant", "ReviewWebsite", "ReviewDisabled", "ReviewFormer", "ReviewSecondCaptain", "ReviewSecondMember" })
            AddAccount(name, GlobalRole.User, now);
        accounts["ReviewDisabled"].Disable(now.AddDays(-1), accounts["ReviewOwner"].Id, "Synthetic disabled-account review.");
        await db.SaveChangesAsync(ct);
        var events = new List<BingoEvent>();
        events.Add(await AddEventAsync("Private setup", "ur-draft", EventState.Draft, now, 14, ct));
        events.Add(await AddEventAsync("Signups open", "ur-signups-open", EventState.SignupOpen, now, 15, ct));
        events.Add(await AddEventAsync("Signups closed — finalized affiliated rosters", "ur-signups-closed", EventState.SignupClosed, now, 16, ct, roster: true));
        events.Add(await AddEventAsync("Unknown timezone", "ur-unknown-timezone", EventState.SignupClosed, now, 17, ct, timezone: "Review/Unknown"));
        for (var index = 1; index <= 9; index++)
            events.Add(await AddEventAsync($"Upcoming setup {index:00}", $"ur-upcoming-{index:00}", EventState.Draft, now, 20 + index, ct));

        // Reach and retain historical states before creating the sole visible current event.
        var archived = await AddEventAsync("Archived — affiliated roster history", "ur-archived", EventState.Archived, now, -14, ct, roster: true);
        events.Add(archived);
        foreach (var (name, slug, state) in new[] {
            ("Hidden final review", "ur-hidden-review", EventState.AwaitingFinalReview),
            ("Hidden legacy Finalized", "ur-hidden-finalized", EventState.Finalized),
            ("Hidden Archived", "ur-hidden-archived", EventState.Archived) })
        {
            var hidden = await AddEventAsync(name, slug, state, now, -21, ct, roster: true);
            hidden.Hide(accounts["ReviewOwner"].Id, now.AddHours(-2), hidden.Name, "Synthetic hidden-history review.");
            Audit(hidden, "event.hidden", now.AddHours(-2), state.ToString(), "Hidden");
            events.Add(hidden);
        }
        var cancelled = await AddEventAsync("Cancelled with signup history", "ur-cancelled", EventState.SignupOpen, now, 18, ct);
        cancelled.Cancel(accounts["ReviewOwner"].Id, now.AddDays(-1), "Synthetic event called off after signups opened.", protectedHistoryExists: true);
        Audit(cancelled, "event.cancelled", now.AddDays(-1), "SignupOpen", "Cancelled");
        events.Add(cancelled);
        var discarded = await AddEventAsync("Discarded private setup — excluded", "ur-discarded", EventState.Draft, now, 19, ct);
        discarded.Discard(accounts["ReviewOwner"].Id, now, protectedHistoryExists: false);
        Audit(discarded, "event.discarded", now, "Draft", "Discarded");
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
        Audit(active, "board.published_correction_started", now.AddMinutes(-10), "Published", "Correction in progress");
        var blocked = await AddBlockedReviewAsync(active, now, ct);
        AddEndOutcome(active, EventCompetitionEndUpdateStatus.Pending, now, 91001);
        AddEndOutcome(archived, EventCompetitionEndUpdateStatus.Rejected, now, 91002);
        AddEndOutcome(events.Single(value => value.Slug == "ur-hidden-archived"), EventCompetitionEndUpdateStatus.CouldNotUpdate, now, 91003);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new UiReviewScenarios(profile, now, active.Id, discarded.Id, blocked,
            events.Select(value => new UiReviewEvent(value.Id, value.Name, value.Slug, value.State, value.IsHidden)).ToArray(),
            accounts.Values.Select(value => new UiReviewAccount(value.LoginName, value.GlobalRole!.Value, value.DisabledAt is not null)).ToArray());
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
        CancellationToken ct, bool roster = false, string timezone = "Europe/Copenhagen")
    {
        var owner = accounts["ReviewOwner"];
        var start = now.AddDays(startDays);
        var end = startDays < -1 ? start.AddDays(5) : start.AddDays(7);
        if (state == EventState.AwaitingFinalReview && startDays == -1) end = now.AddHours(-1);
        var item = new BingoEvent(Guid.NewGuid(), name, slug, "Invented UI review scenario. Safe local data only.", timezone,
            start.AddDays(-7), state == EventState.SignupOpen ? now.AddDays(7) : start.AddDays(-2), start, end, end.AddMinutes(30), 20, owner.Id, now.AddDays(-90));
        item.ConfigurePlanning("Use synthetic evidence only.", "Optional synthetic buy-in", "Local review only", 2, 3, 2, 2);
        item.ConfigureSignup(true, false, null);
        db.Events.Add(item);
        var form = new SignupForm(Guid.NewGuid(), item.Id, now.AddDays(-89));
        var question = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Playing account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        db.SignupForms.Add(form);
        db.SignupQuestions.Add(question);
        var session = new DraftSession(Guid.NewGuid(), item.Id, 3);
        db.DraftSessions.Add(session);
        Audit(item, "event.created", item.CreatedAt, null, "Draft");
        if (state != EventState.Draft)
        {
            var openedAt = startDays < 0 ? start.AddDays(-7) : now.AddDays(-3);
            item.OpenSignups(openedAt);
            item.MarkFirstPublic(openedAt);
            Audit(item, "event.signups_opened", openedAt, "Draft", "SignupOpen");
            if (state != EventState.SignupOpen)
            {
                var closedAt = startDays < 0 ? start.AddDays(-2) : now.AddDays(-2);
                item.CloseSignups(closedAt);
                Audit(item, "event.signups_closed", closedAt, "SignupOpen", "SignupClosed");
            }
        }
        if (roster) AddRosters(item, session, question.Id, now, start);
        await AddBoardAsync(item, now, state != EventState.Draft, ct);
        if (state is EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived)
        {
            item.StartEvent(start);
            item.MarkItemPricesCaptured();
            Audit(item, "event.started", start, "SignupClosed", "Live");
            if (state != EventState.Live)
            {
                item.EndEvent(end);
                var transition = new EventStateTransition(Guid.NewGuid(), item.Id, EventState.Live, EventState.AwaitingFinalReview, owner.Id, end, "Synthetic scheduled end.");
                db.EventStateTransitions.Add(transition);
                Audit(item, "event.ended", end, "Live", "AwaitingFinalReview");
                if (state is EventState.Finalized or EventState.Archived)
                {
                    item.CloseSubmissionsIfDue(end.AddHours(1));
                    AddOfficialResults(item, transition.Id, end.AddDays(1));
                    if (state == EventState.Finalized) item.FinalizeResults(end.AddDays(1));
                    else item.PublishOfficialResults(end.AddDays(1));
                    Audit(item, "event.results_published", end.AddDays(1), "AwaitingFinalReview", state.ToString());
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
        var names = new[] { "ReviewCaptain", "ReviewCoCaptain", "ReviewParticipant", "ReviewSecondCaptain", "ReviewSecondMember", "ReviewWebsite", "ReviewFormer" };
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
            var role = index is 0 or 3 ? TeamMembershipRole.Captain : index == 1 ? TeamMembershipRole.CoCaptain : TeamMembershipRole.Participant;
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

    private async Task AddBoardAsync(BingoEvent item, DateTimeOffset now, bool publish, CancellationToken ct)
    {
        var board = new Board(Guid.NewGuid(), item.Id, "Synthetic community challenges", 2, 2);
        db.Boards.Add(board);
        foreach (var (name, index) in TileNames.Select((name, index) => (name, index)))
        {
            var description = $"Complete the synthetic {name.ToLowerInvariant()} objective.";
            var template = new TileTemplate(Guid.NewGuid(), name, description, ObjectiveType.Manual, "Upload an invented screenshot.", 2);
            var tile = new BoardTile(Guid.NewGuid(), board.Id, template.Id, index / 2, index % 2, name, description, "Upload an invented screenshot.", 2);
            var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 1, true, false, description, true);
            db.TileTemplates.Add(template); db.BoardTiles.Add(tile); db.BoardRequirementSnapshots.Add(requirement);
            db.TileTemplateRequirements.Add(new TileTemplateRequirement(Guid.NewGuid(), template.Id, 0, 1, true, false, description, true));
        }
        board.SetTotalEhb(8);
        await db.SaveChangesAsync(ct);
        if (!publish) return;
        var approvedAt = (item.ActualSignupClosedAt ?? item.ActualSignupOpenedAt)!.Value.AddHours(1);
        var approval = new BoardApprovalSnapshot(Guid.NewGuid(), board.Id, 1, approvedAt, item.CreatedByAccountId, null, board.Name, board.Rows, board.Columns, board.TotalEhbEstimate, board.CalculationVersion, board.Version, BoardState.Validated);
        db.BoardApprovalSnapshots.Add(approval);
        foreach (var tile in db.BoardTiles.Local.Where(value => value.BoardId == board.Id))
        {
            var frozenTile = new BoardApprovalTileSnapshot(Guid.NewGuid(), approval.Id, tile.Id, tile.TileTemplateId, tile.RowIndex, tile.ColumnIndex, tile.NameSnapshot, tile.DescriptionSnapshot, tile.EvidenceInstructionsSnapshot, tile.EstimatedEhbSnapshot, null);
            db.BoardApprovalTileSnapshots.Add(frozenTile);
            var requirement = db.BoardRequirementSnapshots.Local.Single(value => value.BoardTileId == tile.Id);
            db.BoardApprovalRequirementSnapshots.Add(new BoardApprovalRequirementSnapshot(Guid.NewGuid(), frozenTile.Id, requirement.Id, requirement.Position, requirement.TargetContribution, requirement.DuplicatesAllowed, requirement.AllowHigherWeightings, requirement.CreditedWeight, requirement.Description, requirement.ManualObjective));
        }
        await db.SaveChangesAsync(ct);
        board.Approve(approval.Id);
        board.Publish(approvedAt.AddHours(1));
        item.SetBoardPublication(true, approvedAt.AddHours(1));
    }

    private void AddOfficialResults(BingoEvent item, Guid cycle, DateTimeOffset at)
    {
        var finalization = new EventFinalizationSnapshot(Guid.NewGuid(), item.Id, 1, at, accounts["ReviewOwner"].Id, cycle);
        db.EventFinalizations.Add(finalization);
        var placement = 0;
        foreach (var team in db.Teams.Local.Where(value => value.EventId == item.Id).OrderBy(value => value.Name, StringComparer.Ordinal))
            db.OfficialPlacements.Add(new OfficialPlacementSnapshot(Guid.NewGuid(), finalization.Id, item.Id, team.Id, team.Name, ++placement, false, null, 0, 0, 0));
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

    private void Audit(BingoEvent item, string action, DateTimeOffset at, string? before, string after) =>
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), at, accounts["ReviewOwner"].Id, "ReviewOwner", action, "BingoEvent", item.Id.ToString(), "Invented UI review scenario.", item.Id, before, after));
}

public sealed record UiReviewEvent(Guid Id, string Name, string Slug, EventState State, bool Hidden);
public sealed record UiReviewAccount(string Username, GlobalRole Role, bool Disabled);
public sealed record UiReviewScenarios(string Profile, DateTimeOffset BuiltAt, Guid CurrentEventId, Guid DiscardedEventId, Guid BlockedSubmissionId, IReadOnlyList<UiReviewEvent> Events, IReadOnlyList<UiReviewAccount> Accounts);
