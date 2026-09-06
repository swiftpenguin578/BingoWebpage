using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bingo.Application.Boards;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.HistoricalImport;

public sealed class HistoricalEventImporter(ApplicationDbContext db, TimeProvider time)
{
    public const string EventSlug = "det-store-danske-sommerbingo-2026";
    public const string Disclosure = "Historical record — evidence image not retained; player attribution and timing reconstructed from event EHB.";
    public const long CompetitionId = 145197;
    private const decimal ApprovedMaggotKingEhb = 31.1487m;
    private const string ImportAction = "historical_import.applied";
    private const int ImportLock = 7303010;
    private const string SourceEventId = "dkl-sommerbingo-2026";
    private const string SourceUrl = "https://wiseoldman.net/competitions/145197";
    private const string ReviewedManifestSha256 = "e5297b20fc5e4a842b6a1e5ab378128cbe1c2bad16033fc875c54607c0d49438";
    private static readonly (string Name, string Slug)[] ApprovedTeams =
    [
        ("The Agency", "the-agency"),
        ("Xen0%_d_rops", "xen0-d-rops"),
        ("Touch Kids, not grass", "touch-kids-not-grass"),
        ("Morytania Monkeys", "morytania-monkeys"),
        ("Zalamalikum", "zalamalikum"),
        ("Såeh cs?", "saeh-cs")
    ];
    private static readonly string[] ApprovedTileNames =
    [
        "Nex", "Royal Titans", "God Wars", "Wilderness Boss", "Duke / Whisperer", "Araxxor",
        "Phosani's Nightmare", "Yama", "The Hueycoatl", "Vorkath", "Sarachnis", "Grotesque Guardians",
        "Theatre of Blood", "Superior Slayer", "Corp", "Maggot King", "Alchemical Hydra", "Zulrah",
        "Tombs of Amascut", "Doom", "Fortis Colosseum", "Leviathan / Vardorvis", "Barrows / Moons",
        "Cerberus", "Chambers of Xeric"
    ];
    private static readonly DateTimeOffset EventStartsAt = new(2026, 7, 14, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EventEndsAt = new(2026, 7, 19, 16, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public async Task<HistoricalImportResult> RunAsync(HistoricalImportOptions options, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var manifestBytes = await ReadFileAsync(options.ManifestPath, "public manifest", errors, cancellationToken);
        var inputPath = options.InputPath ?? Environment.GetEnvironmentVariable("HISTORICAL_IMPORT_INPUT");
        var inputBytes = await ReadFileAsync(inputPath, "private operator input", errors, cancellationToken);
        if (errors.Count > 0) return HistoricalImportResult.Failure(errors);

        HistoricalManifest? manifest;
        HistoricalInput? input;
        try
        {
            manifest = JsonSerializer.Deserialize<HistoricalManifest>(manifestBytes!, JsonOptions);
            input = JsonSerializer.Deserialize<HistoricalInput>(inputBytes!, JsonOptions);
        }
        catch (JsonException exception)
        {
            return HistoricalImportResult.Failure([$"The historical import JSON is invalid: {exception.Message}"]);
        }

        if (manifest is null || input is null)
            return HistoricalImportResult.Failure(["The public manifest and private operator input are both required JSON objects."]);

        var manifestHash = Hash(manifestBytes!);
        var inputHash = Hash(inputBytes!);
        var importHash = Hash(Encoding.UTF8.GetBytes($"{manifestHash}:{inputHash}"));
        var resolved = await PreflightAsync(manifest, input, manifestHash, importHash, errors, cancellationToken);
        if (options.Apply)
        {
            if (string.IsNullOrWhiteSpace(options.ActorUsername)) errors.Add("Apply requires --historical-import-actor <active-SuperAdmin-login>.");
            if (!string.Equals(options.Confirmation, manifest.Event?.Name, StringComparison.Ordinal))
                errors.Add($"Apply requires --confirm-historical-import with the exact event title '{manifest.Event?.Name}'.");
        }
        if (errors.Count > 0) return HistoricalImportResult.Failure(errors);
        if (!options.Apply)
            return new(true, false, false, [], $"Historical import preflight passed: 6 teams, 90 participants, 93 WOM accounts, 25 tiles, 150 counters. Hash {importHash}.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({ImportLock})", cancellationToken);
        var actor = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(account =>
            account.LoginName == options.ActorUsername && account.AccountType == AccountType.WebsiteAccount &&
            account.Active && account.DisabledAt == null && account.GlobalRole == GlobalRole.SuperAdmin, cancellationToken);
        if (actor is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return HistoricalImportResult.Failure([$"No active SuperAdmin matches --historical-import-actor '{options.ActorUsername}'."]);
        }

        errors.Clear();
        resolved = await PreflightAsync(manifest, input, manifestHash, importHash, errors, cancellationToken);
        if (errors.Count > 0) return HistoricalImportResult.Failure(errors);

        var prior = await FindImportAuditAsync(manifest.Event!.SourceEventId!, cancellationToken);
        if (prior is not null)
        {
            if (prior.Contains(importHash, StringComparison.Ordinal))
            {
                await transaction.CommitAsync(cancellationToken);
                return new(true, false, true, [], $"Historical import already applied; exact hash {importHash} is a no-op.");
            }
            return HistoricalImportResult.Failure(["A historical import for this source event already exists with a different hash; divergent imports are fail-closed."]);
        }

        try
        {
            await ApplyAsync(manifest, input, resolved!, actor, manifestHash, inputHash, importHash, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return HistoricalImportResult.Failure([$"Historical import transaction rolled back: {exception.GetBaseException().Message}"]);
        }
        return new(true, true, false, [], $"Historical import applied: {manifest.Event.Name} ({EventSlug}). Hash {importHash}.");
    }

    private static async Task<byte[]?> ReadFileAsync(string? path, string label, List<string> errors, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            errors.Add($"The {label} path is missing. Supply --historical-import-input <external-file> or HISTORICAL_IMPORT_INPUT.");
            return null;
        }
        if (!File.Exists(path))
        {
            errors.Add($"The {label} file '{path}' does not exist. Correct the operator input and retry preflight.");
            return null;
        }
        try { return await File.ReadAllBytesAsync(path, cancellationToken); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            errors.Add($"The {label} file '{path}' could not be read: {exception.Message}");
            return null;
        }
    }

    private async Task<ResolvedImport?> PreflightAsync(
        HistoricalManifest manifest, HistoricalInput input, string manifestHash, string importHash,
        List<string> errors, CancellationToken cancellationToken)
    {
        ValidateManifest(manifest, errors);
        ValidatePrivateInput(manifest, input, errors);
        if (errors.Count > 0) return null;
        if (!string.Equals(manifestHash, ReviewedManifestSha256, StringComparison.Ordinal))
            errors.Add("The public historical manifest SHA-256 does not match the reviewed bytes; restore src/Bingo.Web/data/historical-import/det-store-danske-sommerbingo-2026.json before retrying preflight.");
        if (errors.Count > 0) return null;

        var sourceNames = input.Accounts!.SelectMany(value => value.Accounts!).Select(value => value.Username!).ToList();
        var uniqueSourceNames = sourceNames.GroupBy(Normalize, StringComparer.Ordinal).Where(value => value.Count() == 1)
            .ToDictionary(value => value.Key, value => value.Single(), StringComparer.Ordinal);
        var existingCharacters = await db.OsrsCharacters.AsNoTracking()
            .Where(value => uniqueSourceNames.Keys.Contains(value.NormalizedName)).ToListAsync(cancellationToken);
        foreach (var character in existingCharacters)
            if (!string.Equals(character.DisplayName, uniqueSourceNames[character.NormalizedName], StringComparison.Ordinal))
                errors.Add($"OSRS character spelling conflict for normalized name '{character.NormalizedName}': existing '{character.DisplayName}', input '{uniqueSourceNames[character.NormalizedName]}'.");

        var existingAudit = await FindImportAuditAsync(manifest.Event!.SourceEventId!, cancellationToken);
        if (existingAudit is not null && !existingAudit.Contains(importHash, StringComparison.Ordinal))
            errors.Add("A prior import for this source event has a different manifest/input hash; it cannot be merged or overwritten.");

        var existingEvent = await db.Events.AsNoTracking().SingleOrDefaultAsync(value => value.Slug == EventSlug, cancellationToken);
        if (existingEvent is not null && existingAudit is null)
            errors.Add($"An event already uses slug '{EventSlug}' without an exact historical-import audit record; remove the conflict through the normal operator process before retrying.");
        if (existingAudit is not null && existingEvent is null)
            errors.Add("A historical-import audit exists without the archived event; the import is partial and must be repaired through the normal operator process before retrying.");
        if (existingAudit is not null && existingEvent is not null)
            await ValidateExistingImportAsync(existingEvent, manifest, errors, cancellationToken);
        if (await db.Events.AsNoTracking().AnyAsync(value => value.Name == manifest.Event.Name && value.Slug != EventSlug, cancellationToken))
            errors.Add($"Another event already uses the exact historical title '{manifest.Event.Name}' with a different slug.");
        var existingEventId = existingEvent?.Id;
        if (await db.EventCompetitionSynchronizations.AsNoTracking().AnyAsync(value => value.CompetitionId == CompetitionId && (existingEventId == null || value.EventId != existingEventId), cancellationToken))
            errors.Add($"Wise Old Man competition {CompetitionId} is already linked to another event.");

        var bosses = await db.BossActivities.AsNoTracking().ToListAsync(cancellationToken);
        var items = await db.CatalogueItems.AsNoTracking().ToListAsync(cancellationToken);
        var drops = await db.SourceDrops.AsNoTracking().Where(value => value.Active).ToListAsync(cancellationToken);
        var resolvedTiles = new List<ResolvedTile>();
        foreach (var tile in manifest.Tiles ?? [])
        {
            var requirements = new List<ResolvedRequirement>();
            foreach (var requirement in tile.Requirements ?? [])
            {
                var selectedBosses = (requirement.Bosses ?? []).Select(name =>
                    bosses.Where(value => value.Active).SingleOrDefault(value => string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase))).ToList();
                if (selectedBosses.Any(value => value is null))
                {
                    foreach (var name in requirement.Bosses ?? [])
                        if (!bosses.Any(value => value.Active && string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase)))
                            errors.Add($"Tile '{tile.Name}', requirement '{requirement.Description}': active catalogue boss '{name}' is missing or ambiguous.");
                    continue;
                }
                var resolvedBosses = selectedBosses.Cast<Bingo.Domain.Catalogue.BossActivity>().ToList();

                var selectedDrops = new List<ResolvedDrop>();
                foreach (var itemName in requirement.Items ?? [])
                {
                    if (string.Equals(itemName, "Maggot marquess", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"Tile '{tile.Name}', requirement '{requirement.Description}': Maggot marquess is explicitly excluded.");
                        continue;
                    }
                    if (requirement.Manual)
                        continue;
                    var catalogueItem = items.SingleOrDefault(value => value.Active && string.Equals(value.Name, itemName, StringComparison.OrdinalIgnoreCase));
                    if (catalogueItem is null)
                    {
                        errors.Add($"Tile '{tile.Name}', requirement '{requirement.Description}': active catalogue item '{itemName}' is missing or ambiguous.");
                        continue;
                    }
                    var itemDrops = drops.Where(drop => drop.ItemId == catalogueItem.Id && selectedBosses.Any(boss => boss!.Id == drop.BossActivityId)).ToList();
                    if (itemDrops.Count == 0)
                        errors.Add($"Tile '{tile.Name}', requirement '{requirement.Description}': catalogue item '{itemName}' has no active source drop for the named boss set.");
                    selectedDrops.AddRange(itemDrops.Select(drop => ToResolvedDrop(drop, selectedBosses.Single(boss => boss!.Id == drop.BossActivityId)!, catalogueItem)));
                }
                if (!requirement.Manual && (requirement.Items?.Count ?? 0) == 0)
                {
                    var broadDrops = drops.Where(drop => selectedBosses.Any(boss => boss!.Id == drop.BossActivityId)).ToList();
                    if (broadDrops.Count == 0) errors.Add($"Tile '{tile.Name}', requirement '{requirement.Description}': no active catalogue drops exist for the named boss set.");
                    selectedDrops.AddRange(broadDrops.Select(drop =>
                    {
                        var boss = selectedBosses.Single(value => value!.Id == drop.BossActivityId)!;
                        var item = items.SingleOrDefault(value => value.Id == drop.ItemId);
                        return item is null ? null : ToResolvedDrop(drop, boss, item);
                    }).Where(value => value is not null).Select(value => value!));
                }
                selectedDrops = selectedDrops.GroupBy(value => (value.SourceDropId, value.ItemName)).Select(group => group.First()).ToList();
                foreach (var drop in selectedDrops)
                    if (requirement.Weights?.GetValueOrDefault(drop.ItemName, 1) is < 1 or > 2)
                        errors.Add($"Tile '{tile.Name}', requirement '{requirement.Description}': item weight for '{drop.ItemName}' must be 1 or 2.");
                var estimate = requirement.Manual
                    ? (decimal?)null
                    : EhbCalculator.CalculateDropRequirement(requirement.Target, selectedDrops.Select(drop => new EligibleDropRate(
                        drop.EfficientCompletionsPerHour, drop.NumericProbability, requirement.Duplicates ? null : drop.SourceDropId,
                        drop.BossId, requirement.Weights?.GetValueOrDefault(drop.ItemName, 1) ?? 1, drop.RollsPerCompletion, drop.RollGroup)));
                if (!requirement.Manual && estimate is null)
                    errors.Add($"Tile '{tile.Name}', requirement '{requirement.Description}': catalogue rates cannot produce a deterministic EHB estimate.");
                requirements.Add(new ResolvedRequirement(requirement, resolvedBosses, selectedDrops, estimate));
            }
            var tileEhb = EhbCalculator.SumRequirements(requirements.Select(value => value.Estimate), tile.Ehb);
            var explicitlyNonAssertedEhb = tile.Ehb is null && requirements.Count > 0 && requirements.All(value =>
                value.Estimate is null && value.Manifest.Manual);
            if (tileEhb <= 0 && !explicitlyNonAssertedEhb) errors.Add($"Tile '{tile.Name}': the approved EHB estimate is zero; provide valid catalogue rates or the explicit manual value.");
            if (tile.Name == "Maggot King" && (tile.Ehb is not null || decimal.Round(tileEhb, 4) != ApprovedMaggotKingEhb))
                errors.Add($"Tile 'Maggot King' must resolve from the active catalogue to exactly {ApprovedMaggotKingEhb.ToString("0.0000", CultureInfo.InvariantCulture)} EHB; correct the catalogue-backed rates before retrying.");
            resolvedTiles.Add(new ResolvedTile(tile, requirements, tileEhb));
        }
        if (errors.Count > 0) return null;
        return new ResolvedImport(resolvedTiles, existingEvent?.Id, importHash);
    }

    private async Task ApplyAsync(HistoricalManifest manifest, HistoricalInput input, ResolvedImport resolved,
        Account actor, string manifestHash, string inputHash, string importHash, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().ToUniversalTime();
        var eventId = StableGuid($"event:{manifest.Event!.SourceEventId}");
        var item = BingoEvent.CreateArchivedHistorical(eventId, manifest.Event.Name!, EventSlug,
            Disclosure, "Europe/Copenhagen", EventStartsAt.AddDays(-7), EventStartsAt.AddDays(-2),
            EventStartsAt, EventEndsAt, actor.Id, now, Disclosure, 6, 15, 5, 5);
        db.Events.Add(item);
        var teams = new Dictionary<string, Team>(StringComparer.Ordinal);
        foreach (var sourceTeam in manifest.Teams!)
        {
            var team = new Team(StableGuid($"{eventId:N}:team:{sourceTeam.Slug}"), eventId, sourceTeam.Name!, sourceTeam.Slug!, TeamFormationType.Preformed, null, false, EventStartsAt);
            team.Finalize(EventEndsAt);
            teams.Add(sourceTeam.Slug!, team);
            db.Teams.Add(team);
        }

        var participantRows = new Dictionary<string, EventParticipant>(StringComparer.Ordinal);
        var primaryCharacters = new Dictionary<string, OsrsCharacter>(StringComparer.Ordinal);
        var allAssignments = new List<EventParticipantCharacter>();
        foreach (var sourceParticipant in input.Accounts!.OrderBy(value => value.TeamSlug, StringComparer.Ordinal).ThenBy(value => value.ParticipantKey, StringComparer.Ordinal))
        {
            var participantId = StableGuid($"{eventId:N}:participant:{sourceParticipant.ParticipantKey}");
            var participant = new EventParticipant(participantId, eventId, SignupStatus.Confirmed,
                participantRows.Count + 1, EventStartsAt.AddMinutes(participantRows.Count), SignupSource.CsvImport);
            participantRows.Add(sourceParticipant.ParticipantKey!, participant);
            db.EventParticipants.Add(participant);
            var team = teams[sourceParticipant.TeamSlug!];
            db.TeamMemberships.Add(new TeamMembership(StableGuid($"{team.Id:N}:membership:{participantId:N}"), team.Id, participant.Id,
                TeamMembershipRole.Participant, EventStartsAt, null, "Frozen historical import"));
            var sourceAccounts = sourceParticipant.Accounts!;
            for (var accountIndex = 0; accountIndex < sourceAccounts.Count; accountIndex++)
            {
                var sourceAccount = sourceAccounts[accountIndex];
                var normalized = Normalize(sourceAccount.Username!);
                var character = await db.OsrsCharacters.SingleOrDefaultAsync(value => value.NormalizedName == normalized, cancellationToken);
                if (character is null)
                {
                    character = new OsrsCharacter(StableGuid($"character:{normalized}"), sourceAccount.Username!, normalized, now);
                    db.OsrsCharacters.Add(character);
                }
                if (accountIndex == 0) primaryCharacters[sourceParticipant.ParticipantKey!] = character;
                var assignment = new EventParticipantCharacter(
                    StableGuid($"{participant.Id:N}:assignment:{normalized}"), eventId, participant.Id, character.Id, accountIndex,
                    EventStartsAt.AddMinutes(participantRows.Count + accountIndex), actor.Id, null, EventCharacterRole.Playing,
                    sourceAccount.StartEhb, EhbSource.Import, null);
                allAssignments.Add(assignment);
                db.EventParticipantCharacters.Add(assignment);
            }
        }

        var draft = new DraftSession(StableGuid($"{eventId:N}:draft"), eventId, 15);
        draft.Start(EventStartsAt);
        draft.Finalize(EventEndsAt);
        db.DraftSessions.Add(draft);
        var cycle = new DraftPublicationCycle(StableGuid($"{eventId:N}:publication-cycle"), draft.Id, 1, EventEndsAt, actor.Id);
        db.DraftPublicationCycles.Add(cycle);
        foreach (var sourceParticipant in input.Accounts!)
        {
            var participant = participantRows[sourceParticipant.ParticipantKey!];
            var team = teams[sourceParticipant.TeamSlug!];
            db.DraftPublicationRosters.Add(new DraftPublicationRoster(
                StableGuid($"{cycle.Id:N}:roster:{participant.Id:N}"), cycle.Id, team.Id, participant.Id,
                TeamMembershipRole.Participant, null, sourceParticipant.Accounts![0].Username!));
        }

        var board = new Board(StableGuid($"{eventId:N}:board"), eventId, "Det Store Danske Sommerbingo 2026", 5, 5);
        db.Boards.Add(board);
        var tileRows = new List<(HistoricalTile Manifest, BoardTile Tile, IReadOnlyList<(HistoricalRequirement Manifest, BoardRequirementSnapshot Snapshot, ResolvedRequirement Resolved)> Requirements)>();
        for (var tileIndex = 0; tileIndex < resolved.Tiles.Count; tileIndex++)
        {
            var resolvedTile = resolved.Tiles[tileIndex];
            var imageUrl = resolvedTile.Requirements.SelectMany(value => value.Bosses).Select(value => value.ImageUrl).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                ?? resolvedTile.Requirements.SelectMany(value => value.Drops).Select(value => value.ImageUrl).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            var template = new TileTemplate(StableGuid($"{board.Id:N}:template:{tileIndex}"), resolvedTile.Manifest.Name!,
                string.Join("; ", resolvedTile.Manifest.Requirements!.Select(value => value.Description)),
                resolvedTile.Manifest.Requirements!.Any(value => value.Manual) ? ObjectiveType.Manual : ObjectiveType.DropRequirements,
                Disclosure, resolvedTile.Manifest.Ehb, imageUrl);
            var tile = new BoardTile(StableGuid($"{board.Id:N}:tile:{tileIndex}"), board.Id, template.Id, tileIndex / 5, tileIndex % 5,
                resolvedTile.Manifest.Name!, string.Join("; ", resolvedTile.Manifest.Requirements!.Select(value => value.Description)), Disclosure,
                resolvedTile.Ehb, imageUrl);
            db.TileTemplates.Add(template);
            db.BoardTiles.Add(tile);
            var requirementRows = new List<(HistoricalRequirement, BoardRequirementSnapshot, ResolvedRequirement)>();
            for (var requirementIndex = 0; requirementIndex < resolvedTile.Requirements.Count; requirementIndex++)
            {
                var resolvedRequirement = resolvedTile.Requirements[requirementIndex];
                var requirement = resolvedRequirement.Manifest;
                var snapshot = new BoardRequirementSnapshot(StableGuid($"{tile.Id:N}:requirement:{requirementIndex}"), tile.Id,
                    requirementIndex + 1, requirement.Target, requirement.Duplicates, requirement.HigherWeights, requirement.Description!, requirement.Manual);
                var templateRequirement = new TileTemplateRequirement(StableGuid($"{template.Id:N}:requirement:{requirementIndex}"), template.Id,
                    requirementIndex + 1, requirement.Target, requirement.Duplicates, requirement.HigherWeights, requirement.Description!, requirement.Manual);
                db.BoardRequirementSnapshots.Add(snapshot);
                db.TileTemplateRequirements.Add(templateRequirement);
                foreach (var boss in resolvedRequirement.Bosses)
                {
                    db.BoardRequirementBossSnapshots.Add(new BoardRequirementBossSnapshot(StableGuid($"{snapshot.Id:N}:boss:{boss.Id:N}"), snapshot.Id, boss.Id, boss.Name, boss.EfficientCompletionsPerHour));
                    db.TemplateRequirementBosses.Add(new TemplateRequirementBoss(StableGuid($"{templateRequirement.Id:N}:boss:{boss.Id:N}"), templateRequirement.Id, boss.Id));
                }
                foreach (var drop in resolvedRequirement.Drops)
                {
                    int? maximum = requirement.Duplicates ? null : 1;
                    db.BoardRequirementDropSnapshots.Add(new BoardRequirementDropSnapshot(StableGuid($"{snapshot.Id:N}:drop:{drop.SourceDropId:N}:{drop.ItemName}"), snapshot.Id,
                        drop.SourceDropId, drop.ItemId, drop.BossName, drop.ItemName, drop.DisplayRate, drop.NumericProbability, maximum, drop.DefaultEhb, requirement.Weights?.GetValueOrDefault(drop.ItemName, 1) ?? 1));
                    db.TemplateRequirementDrops.Add(new TemplateRequirementDrop(StableGuid($"{templateRequirement.Id:N}:drop:{drop.SourceDropId:N}:{drop.ItemName}"), templateRequirement.Id,
                        drop.SourceDropId, maximum, requirement.Weights?.GetValueOrDefault(drop.ItemName, 1) ?? 1));
                }
                requirementRows.Add((requirement, snapshot, resolvedRequirement));
            }
            tileRows.Add((resolvedTile.Manifest, tile, requirementRows));
        }
        board.SetTotalEhb(tileRows.Sum(value => value.Tile.EstimatedEhbSnapshot));
        await db.SaveChangesAsync(cancellationToken);
        AddApprovalSnapshot(item, board, tileRows, actor.Id, EventEndsAt);

        var assignmentFingerprint = Fingerprint(allAssignments);
        var sync = new EventCompetitionSynchronization(StableGuid($"{eventId:N}:competition"), eventId, 1, CompetitionId,
            manifest.Event.CompetitionTitle!, EventStartsAt, EventEndsAt, assignmentFingerprint, EventEndsAt);
        sync.MarkHistoricalSuccess(input.Accounts!.SelectMany(value => value.Accounts!).Select(value => value.FetchedAt!.Value).Max(),
            input.Accounts!.SelectMany(value => value.Accounts!).Where(value => value.UpstreamUpdatedAt is not null).Select(value => value.UpstreamUpdatedAt!.Value).DefaultIfEmpty(EventEndsAt).Max());
        db.EventCompetitionSynchronizations.Add(sync);
        var allSourceAccounts = input.Accounts!.SelectMany(value => value.Accounts!.Select(account => (Participant: value, Account: account))).ToList();
        foreach (var source in allSourceAccounts)
        {
            var character = primaryCharacters.Values.FirstOrDefault(value => string.Equals(value.NormalizedName, Normalize(source.Account.Username!), StringComparison.Ordinal));
            character ??= await db.OsrsCharacters.SingleAsync(value => value.NormalizedName == Normalize(source.Account.Username!), cancellationToken);
            db.EventCompetitionCharacterActivities.Add(new EventCompetitionCharacterActivity(
                StableGuid($"{sync.Id:N}:activity:{character.Id:N}"), eventId, sync.Generation, CompetitionId, character.Id,
                source.Account.GainedEhb!.Value, source.Account.FetchedAt!.Value, source.Account.UpstreamUpdatedAt,
                assignmentFingerprint, source.Account.StartEhb, source.Account.EndEhb));
        }

        var totalUnits = manifest.Teams!.Sum(value => value.Counters!.Sum());
        var sequence = 0;
        foreach (var sourceTeam in manifest.Teams!)
        {
            var team = teams[sourceTeam.Slug!];
            var participants = input.Accounts!.Where(value => value.TeamSlug == sourceTeam.Slug).OrderBy(value => value.ParticipantKey, StringComparer.Ordinal).ToList();
            var weightedParticipants = participants.Select(value => new WeightedParticipant(value, Math.Max(1m, value.Accounts!.Sum(account => account.StartEhb!.Value)))).ToList();
            var totalWeight = weightedParticipants.Sum(value => value.Weight);
            for (var tileIndex = 0; tileIndex < sourceTeam.Counters!.Count; tileIndex++)
            {
                var remaining = sourceTeam.Counters[tileIndex];
                foreach (var row in tileRows[tileIndex].Requirements.OrderBy(value => value.Item2.Position))
                {
                    var amount = Math.Min(remaining, row.Item2.TargetContribution);
                    for (var unit = 0; unit < amount; unit++)
                    {
                        var selected = ChooseWeighted(weightedParticipants, totalWeight, sequence);
                        var participant = participantRows[selected.Input.ParticipantKey!];
                        var character = primaryCharacters[selected.Input.ParticipantKey!];
                        var submittedAt = EventStartsAt.AddTicks((long)(TimeSpan.FromTicks((EventEndsAt - EventStartsAt).Ticks).Ticks * (decimal)sequence / Math.Max(1, totalUnits)));
                        var approvedAt = submittedAt.AddSeconds(1);
                        var submissionId = StableGuid($"{eventId:N}:contribution:{sequence}");
                        var submission = new Submission(submissionId, eventId, team.Id, tileRows[tileIndex].Tile.Id, row.Item2.Id, null,
                            participant.Id, character.Id, character.DisplayName, actor.Id, 1, submittedAt, Disclosure, null);
                        submission.Approve(1, approvedAt);
                        db.Submissions.Add(submission);
                        db.SubmissionContributions.Add(new SubmissionContribution(StableGuid($"{submissionId:N}:unit"), submissionId, team.Id, row.Item2.Id, null, participant.Id, 1, approvedAt));
                        db.ReviewActions.Add(new ReviewAction(StableGuid($"{submissionId:N}:submitted"), submissionId, ReviewActionType.Submitted, actor.Id, submittedAt, Disclosure, null, null));
                        db.ReviewActions.Add(new ReviewAction(StableGuid($"{submissionId:N}:approved"), submissionId, ReviewActionType.Approve, actor.Id, approvedAt, Disclosure, null, null));
                        sequence++;
                    }
                    remaining -= amount;
                    if (remaining == 0) break;
                }
                if (remaining != 0)
                    throw new InvalidOperationException($"Historical counter for team '{sourceTeam.Name}' and tile '{tileRows[tileIndex].Manifest.Name}' exceeded its summed requirement target.");
            }
        }

        var reviewCycleId = StableGuid($"{eventId:N}:import-cycle");
        db.EventStateTransitions.Add(new EventStateTransition(reviewCycleId, eventId, EventState.Draft, EventState.Archived, actor.Id, EventEndsAt, "Frozen historical import; no live lifecycle transition."));
        var finalizationId = StableGuid($"{eventId:N}:finalization");
        db.EventFinalizations.Add(new EventFinalizationSnapshot(finalizationId, eventId, 1, EventEndsAt, actor.Id, reviewCycleId,
            "[]", JsonSerializer.Serialize(new { sourceEventId = manifest.Event.SourceEventId, manifestHash, inputHash, importHash }),
            JsonSerializer.Serialize(new { counters = manifest.Teams.Select(value => value.Counters), placements = manifest.Teams.Select(value => new { value.Slug, value.Placement }) })));
        foreach (var sourceTeam in manifest.Teams)
        {
            var team = teams[sourceTeam.Slug!];
            var completeTiles = sourceTeam.Counters!.Select((value, index) => value >= tileRows[index].Requirements.Sum(row => row.Item2.TargetContribution)).ToArray();
            var completedLines = Enumerable.Range(0, 5).Count(index => completeTiles.Skip(index * 5).Take(5).All(value => value))
                + Enumerable.Range(0, 5).Count(column => Enumerable.Range(0, 5).All(row => completeTiles[row * 5 + column]));
            db.OfficialPlacements.Add(new OfficialPlacementSnapshot(StableGuid($"{finalizationId:N}:placement:{team.Id:N}"), finalizationId, eventId, team.Id,
                team.Name, sourceTeam.Placement, completeTiles.All(value => value), completeTiles.All(value => value) ? EventEndsAt : null,
                completedLines, completeTiles.Count(value => value), 0));
        }
        db.AuditEntries.Add(new AuditEntry(StableGuid($"{eventId:N}:audit"), now, actor.Id, actor.LoginName, ImportAction, "event", eventId.ToString("D"),
            JsonSerializer.Serialize(new { manifestHash, inputHash, importHash, sourceEventId = manifest.Event.SourceEventId, counts = new { teams = 6, participants = 90, accounts = 93, tiles = 25, counters = 150 } }), eventId));
    }

    private void AddApprovalSnapshot(BingoEvent bingoEvent, Board board,
        IReadOnlyList<(HistoricalTile Manifest, BoardTile Tile, IReadOnlyList<(HistoricalRequirement Manifest, BoardRequirementSnapshot Snapshot, ResolvedRequirement Resolved)> Requirements)> tiles,
        Guid actorId, DateTimeOffset at)
    {
        var approval = new BoardApprovalSnapshot(StableGuid($"{board.Id:N}:approval"), board.Id, 1, at, actorId, null,
            board.Name, board.Rows, board.Columns, board.TotalEhbEstimate, board.CalculationVersion, board.Version, BoardState.Validated);
        db.BoardApprovalSnapshots.Add(approval);
        foreach (var tileRow in tiles)
        {
            var approvalTile = new BoardApprovalTileSnapshot(StableGuid($"{approval.Id:N}:tile:{tileRow.Tile.Id:N}"), approval.Id, tileRow.Tile.Id,
                tileRow.Tile.TileTemplateId, tileRow.Tile.RowIndex, tileRow.Tile.ColumnIndex, tileRow.Tile.NameSnapshot,
                tileRow.Tile.DescriptionSnapshot, Disclosure, tileRow.Tile.EstimatedEhbSnapshot, null);
            db.BoardApprovalTileSnapshots.Add(approvalTile);
            foreach (var row in tileRow.Requirements)
            {
                var requirement = row.Snapshot;
                var approvalRequirement = new BoardApprovalRequirementSnapshot(StableGuid($"{approvalTile.Id:N}:requirement:{requirement.Position}"), approvalTile.Id,
                    requirement.Id, requirement.Position, requirement.TargetContribution, requirement.DuplicatesAllowed, requirement.AllowHigherWeightings,
                    requirement.CreditedWeight, requirement.Description, requirement.ManualObjective);
                db.BoardApprovalRequirementSnapshots.Add(approvalRequirement);
                foreach (var boss in row.Resolved.Bosses)
                    db.BoardApprovalRequirementBossSnapshots.Add(new BoardApprovalRequirementBossSnapshot(StableGuid($"{approvalRequirement.Id:N}:boss:{boss.Id:N}"), approvalRequirement.Id, boss.Id, boss.Name, boss.EfficientCompletionsPerHour, boss.Version));
                foreach (var drop in row.Resolved.Drops)
                    db.BoardApprovalRequirementDropSnapshots.Add(new BoardApprovalRequirementDropSnapshot(StableGuid($"{approvalRequirement.Id:N}:drop:{drop.SourceDropId:N}:{drop.ItemName}"), approvalRequirement.Id,
                        drop.SourceDropId, drop.ItemId, drop.BossName, drop.ItemName, drop.DisplayRate, drop.NumericProbability,
                        row.Manifest.Duplicates ? null : 1, drop.DefaultEhb, row.Manifest.Weights?.GetValueOrDefault(drop.ItemName, 1) ?? 1,
                        drop.CatalogueVersion, drop.ProbabilityScope, drop.ConditionalOnParent, drop.ParentProbability, drop.AssumedParticipants,
                        drop.RollsPerCompletion, drop.RollGroup, drop.RateCondition));
            }
        }
        board.Approve(approval.Id);
        board.Publish(at);
    }

    private async Task<string?> FindImportAuditAsync(string sourceEventId, CancellationToken cancellationToken) =>
        await db.AuditEntries.AsNoTracking().Where(value => value.Action == ImportAction && value.Details != null && value.Details.Contains(sourceEventId))
            .OrderByDescending(value => value.OccurredAt).Select(value => value.Details).FirstOrDefaultAsync(cancellationToken);

    private async Task ValidateExistingImportAsync(BingoEvent existingEvent, HistoricalManifest manifest, List<string> errors, CancellationToken cancellationToken)
    {
        var expectedEventId = StableGuid($"event:{manifest.Event!.SourceEventId}");
        if (existingEvent.Id != expectedEventId || existingEvent.Name != manifest.Event.Name || existingEvent.State != EventState.Archived ||
            existingEvent.EventStartsAt != EventStartsAt || existingEvent.EventEndsAt != EventEndsAt || existingEvent.ArchivedAt != EventEndsAt)
            errors.Add("The existing historical event is divergent from the approved archived identity; no-op is unsafe.");

        var teamCount = await db.Teams.AsNoTracking().CountAsync(value => value.EventId == existingEvent.Id, cancellationToken);
        var participantCount = await db.EventParticipants.AsNoTracking().CountAsync(value => value.EventId == existingEvent.Id, cancellationToken);
        var assignmentCount = await db.EventParticipantCharacters.AsNoTracking().CountAsync(value => value.EventId == existingEvent.Id, cancellationToken);
        var boardCount = await db.Boards.AsNoTracking().CountAsync(value => value.EventId == existingEvent.Id, cancellationToken);
        var tileCount = await db.BoardTiles.AsNoTracking().CountAsync(value => value.BoardId == db.Boards.Where(board => board.EventId == existingEvent.Id).Select(board => board.Id).FirstOrDefault(), cancellationToken);
        var syncCount = await db.EventCompetitionSynchronizations.AsNoTracking().CountAsync(value => value.EventId == existingEvent.Id, cancellationToken);
        var activityCount = await db.EventCompetitionCharacterActivities.AsNoTracking().CountAsync(value => value.EventId == existingEvent.Id, cancellationToken);
        var finalizationCount = await db.EventFinalizations.AsNoTracking().CountAsync(value => value.EventId == existingEvent.Id, cancellationToken);
        var placementCount = await db.OfficialPlacements.AsNoTracking().CountAsync(value => value.EventId == existingEvent.Id, cancellationToken);
        var rosterCount = await (from roster in db.DraftPublicationRosters.AsNoTracking()
                                 join cycle in db.DraftPublicationCycles.AsNoTracking() on roster.DraftPublicationCycleId equals cycle.Id
                                 join draft in db.DraftSessions.AsNoTracking() on cycle.DraftSessionId equals draft.Id
                                 where draft.EventId == existingEvent.Id && cycle.SupersededAt == null
                                 select roster.Id).CountAsync(cancellationToken);
        var submissionCount = await db.Submissions.AsNoTracking().CountAsync(value => value.EventId == existingEvent.Id, cancellationToken);
        var expectedSubmissionCount = manifest.Teams!.SelectMany(value => value.Counters!).Sum();
        var expectedCounts = new (string Name, int Actual, int Expected)[]
        {
            ("teams", teamCount, 6), ("participants", participantCount, 90), ("character assignments", assignmentCount, 93),
            ("boards", boardCount, 1), ("tiles", tileCount, 25), ("synchronizations", syncCount, 1),
            ("activity rows", activityCount, 93), ("finalizations", finalizationCount, 1), ("official placements", placementCount, 6),
            ("published roster rows", rosterCount, 90), ("reconstructed submissions", submissionCount, expectedSubmissionCount)
        };
        foreach (var count in expectedCounts)
            if (count.Actual != count.Expected)
                errors.Add($"Existing historical import is partial: expected {count.Expected} {count.Name}, found {count.Actual}; repair it before retrying.");
    }

    private static void ValidateManifest(HistoricalManifest manifest, List<string> errors)
    {
        var item = manifest.Event;
        if (manifest.SchemaVersion != 1) errors.Add("Public historical manifest schemaVersion must be 1.");
        if (item is null || item.Name != "Det Store Danske Sommerbingo 2026" || item.Slug != EventSlug || item.Timezone != "Europe/Copenhagen" ||
            item.CompetitionId != CompetitionId || item.CompetitionTitle != item.Name || item.StartsAt != EventStartsAt || item.EndsAt != EventEndsAt || item.ArchivedAt != EventEndsAt)
            errors.Add("Public manifest event identity, title, timezone, competition, or exact UTC timestamps do not match the approved historical contract.");
        if (item is null || item.SourceEventId != SourceEventId || item.SourceUrl != SourceUrl) errors.Add("Public manifest source event ID and source URL must match the approved source exactly.");
        if (manifest.Disclosure != Disclosure) errors.Add("The historical disclosure must match the approved text exactly; no additional disclosure is accepted.");
        if (manifest.Teams?.Count != 6) errors.Add("Public manifest must contain exactly six teams.");
        if (manifest.Tiles?.Count != 25) errors.Add("Public manifest must contain exactly 25 English tile definitions.");
        if (manifest.Teams?.SelectMany(value => value.Counters ?? []).Count() != 150) errors.Add("Public manifest must contain exactly 150 aggregate team/tile counters.");
        if (manifest.Teams?.Select(value => value.Slug).Distinct(StringComparer.Ordinal).Count() != 6 || manifest.Teams?.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() != 6)
            errors.Add("Public team names and slugs must be unique and stable.");
        if (manifest.Teams is { Count: 6 } teams)
        {
            for (var index = 0; index < teams.Count; index++)
            {
                var expected = ApprovedTeams[index];
                var actual = teams[index];
                if (actual.Name != expected.Name || actual.Slug != expected.Slug || actual.Placement != index + 1)
                    errors.Add($"Public team {index + 1} must be exactly '{expected.Name}' with slug '{expected.Slug}' and placement {index + 1}.");
                if (actual.Counters?.Count != 25)
                    errors.Add($"Public team '{actual.Name}' must contain exactly 25 aggregate tile counters.");
                if (actual.Counters?.Any(value => value < 0) == true)
                    errors.Add($"Public team '{actual.Name}' contains a negative aggregate tile counter.");
            }
        }
        if (manifest.Tiles is not null)
        {
            if (manifest.Tiles.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() != 25) errors.Add("Public tile spellings must be globally unique within the manifest.");
            if (manifest.Tiles.Count == ApprovedTileNames.Length && !manifest.Tiles.Select(value => value.Name).SequenceEqual(ApprovedTileNames, StringComparer.Ordinal))
                errors.Add("Public tile definitions must use the approved English names and order.");
            foreach (var tile in manifest.Tiles)
            {
                if (tile.Requirements is null or { Count: 0 }) errors.Add($"Tile '{tile.Name}' must have at least one requirement.");
                foreach (var requirement in tile.Requirements ?? [])
                {
                    if (requirement.Target < 1) errors.Add($"Tile '{tile.Name}', requirement '{requirement.Description}' must have a positive target.");
                    if (requirement.Items?.Any(value => string.Equals(value, "Maggot marquess", StringComparison.OrdinalIgnoreCase)) == true) errors.Add($"Tile '{tile.Name}' includes excluded Maggot marquess.");
                    if (requirement.Items?.Count != requirement.Items?.Distinct(StringComparer.OrdinalIgnoreCase).Count()) errors.Add($"Tile '{tile.Name}', requirement '{requirement.Description}' repeats an item name.");
                    if (requirement.Weights?.Any(value => requirement.Items?.Contains(value.Key, StringComparer.OrdinalIgnoreCase) != true || value.Value is < 1 or > 2) == true)
                        errors.Add($"Tile '{tile.Name}', requirement '{requirement.Description}' contains an invalid or out-of-pool item weight.");
                    if (requirement.Weights?.Any(value => value.Value == 2) == true && !requirement.HigherWeights)
                        errors.Add($"Tile '{tile.Name}', requirement '{requirement.Description}' has weight 2 items without higher-weighting enabled.");
                }
            }
        }
        if (manifest.Teams is { Count: 6 } teamsWithCounters && manifest.Tiles is { Count: 25 } tilesWithRequirements &&
            teamsWithCounters.All(value => value.Counters is { Count: 25 }))
        {
            for (var teamIndex = 0; teamIndex < teamsWithCounters.Count; teamIndex++)
                for (var tileIndex = 0; tileIndex < tilesWithRequirements.Count; tileIndex++)
                {
                    var counter = teamsWithCounters[teamIndex].Counters![tileIndex];
                    var target = tilesWithRequirements[tileIndex].Requirements?.Sum(value => (long)value.Target) ?? 0;
                    if (counter > target)
                        errors.Add($"Team '{teamsWithCounters[teamIndex].Name}' tile '{tilesWithRequirements[tileIndex].Name}' counter {counter} exceeds the summed requirement target {target}; correct the manifest before retrying.");
                }
        }
        var royal = manifest.Tiles?.SingleOrDefault(value => value.Name == "Royal Titans");
        if (royal is null || royal.Requirements?.Count != 2 || royal.Requirements[0].Target != 3 || royal.Requirements[1].Target != 3 ||
            !SetEquals(royal.Requirements[0].Items, ["Fire element staff crown"]) || !SetEquals(royal.Requirements[1].Items, ["Ice element staff crown"]))
            errors.Add("Royal Titans must have exactly two AND requirements of three Fire crowns and three Ice crowns.");
        var wilderness = manifest.Tiles?.SingleOrDefault(value => value.Name == "Wilderness Boss");
        if (wilderness is null || wilderness.Requirements?.Count != 3 || wilderness.Requirements.Any(value => value.Target != 1) ||
            !SetEquals(wilderness.Requirements[0].Items, ["Voidwaker hilt"]) || !SetEquals(wilderness.Requirements[1].Items, ["Voidwaker blade"]) || !SetEquals(wilderness.Requirements[2].Items, ["Voidwaker gem"]))
            errors.Add("Wilderness must have exactly three AND requirements of one hilt, one blade, and one gem.");
        var godWars = manifest.Tiles?.SingleOrDefault(value => value.Name == "God Wars")?.Requirements?.SingleOrDefault();
        if (godWars is null || godWars.Items?.Count != 20 || godWars.Duplicates || godWars.Items.Any(value => value.Contains("joker", StringComparison.OrdinalIgnoreCase))) errors.Add("God Wars must use all 20 distinct named candidates, including pets as ordinary drops and no joker.");
        var duke = manifest.Tiles?.SingleOrDefault(value => value.Name == "Duke / Whisperer")?.Requirements?.SingleOrDefault();
        if (duke is null || duke.Target != 2 || !duke.Duplicates || !SetEquals(duke.Items, ["Eye of the duke", "Siren's staff"])) errors.Add("Duke / Whisperer must allow any two eligible Eye/Staff drops including duplicates.");
        var araxxor = manifest.Tiles?.SingleOrDefault(value => value.Name == "Araxxor")?.Requirements?.SingleOrDefault();
        if (araxxor is null || !SetEquals(araxxor.Items, ["Nid (Destroy)", "Jar of venom"])) errors.Add("Araxxor must use only Nid (Destroy) and Jar of venom.");
        var slayer = manifest.Tiles?.SingleOrDefault(value => value.Name == "Superior Slayer");
        if (slayer is null || slayer.Ehb != 21 || slayer.Requirements?.Count != 1 || !slayer.Requirements[0].Manual || slayer.Requirements[0].Target != 3 || !SetEquals(slayer.Requirements[0].Items, ["Imbued heart", "Eternal gem", "Mist battlestaff", "Dust battlestaff"])) errors.Add("Superior Slayer must use its exact four-item manual pool and explicit 21 EHB.");
        var vorkath = manifest.Tiles?.SingleOrDefault(value => value.Name == "Vorkath")?.Requirements?.SingleOrDefault();
        if (vorkath is null || !vorkath.Items!.Contains("Draconic visage") || !vorkath.Items.Contains("Skeletal visage")) errors.Add("Vorkath must include both visages.");
        var maggot = manifest.Tiles?.SingleOrDefault(value => value.Name == "Maggot King")?.Requirements?.SingleOrDefault();
        if (maggot is null || maggot.Target != 5 || !maggot.Duplicates || maggot.Manual || !SetEquals(maggot.Bosses, ["Maggot King"]) || !SetEquals(maggot.Items, ["Elder venator fang", "Crimson kisten"]) ||
            maggot.Items!.Any(value => string.Equals(value, "Maggot marquess", StringComparison.OrdinalIgnoreCase)))
            errors.Add("Maggot King must use Elder venator fang OR Crimson kisten, repeated to 5, with Maggot marquess excluded.");
    }

    private static void ValidatePrivateInput(HistoricalManifest manifest, HistoricalInput input, List<string> errors)
    {
        if (manifest.Event is null)
            return;
        if (input.SourceEventId != manifest.Event.SourceEventId || input.CompetitionId != CompetitionId || input.CompetitionTitle != manifest.Event.Name || input.StartsAt != EventStartsAt || input.EndsAt != EventEndsAt)
            errors.Add("Private input source event, competition, title, or exact UTC interval does not match the public manifest.");
        if (input.Accounts is not { } participants)
        {
            errors.Add("Private input must contain an accounts array with exactly 90 participant records.");
            return;
        }
        if (participants.Count != 90) errors.Add("Private input must contain exactly 90 participants.");
        if (participants.GroupBy(value => value.ParticipantKey?.Trim(), StringComparer.OrdinalIgnoreCase).Any(value => value.Count() != 1)) errors.Add("Private participant keys must be unique.");
        var knownTeams = manifest.Teams?.Select(value => value.Slug).Where(value => !string.IsNullOrWhiteSpace(value)).ToHashSet(StringComparer.Ordinal) ?? [];
        foreach (var participant in participants)
        {
            if (string.IsNullOrWhiteSpace(participant.ParticipantKey) || string.IsNullOrWhiteSpace(participant.DisplayName))
                errors.Add("Every private participant must have a participantKey and displayName.");
            if (!knownTeams.Contains(participant.TeamSlug ?? "")) errors.Add($"Participant '{participant.ParticipantKey}' references unknown team '{participant.TeamSlug}'.");
            var accountCount = participant.Accounts?.Count ?? 0;
            if (accountCount is < 1 or > 2) errors.Add($"Participant '{participant.ParticipantKey}' must have one primary account and at most one approved secondary account.");
            foreach (var account in participant.Accounts ?? [])
            {
                if (string.IsNullOrWhiteSpace(account.Username))
                    errors.Add($"Participant '{participant.ParticipantKey}' contains an account without a username.");
                if (account.StartEhb is null || account.EndEhb is null || account.GainedEhb is null || account.FetchedAt is null || account.UpstreamUpdatedAt is null || account.StartEhb < 0 || account.EndEhb < 0 || account.GainedEhb < 0 || account.EndEhb - account.StartEhb != account.GainedEhb)
                    errors.Add($"Participant '{participant.ParticipantKey}', account '{account.Username}': start/end/gained EHB and both synchronization timestamps must be complete and consistent.");
            }
        }
        if (participants.SelectMany(value => value.Accounts ?? []).Count() != 93) errors.Add("Private input must contain exactly 93 WOM accounts.");
        var all = participants.SelectMany(value => (value.Accounts ?? []).Where(account => !string.IsNullOrWhiteSpace(account.Username)).Select(account => (Participant: value, Account: account))).ToList();
        if (all.Select(value => Normalize(value.Account.Username!)).Distinct(StringComparer.Ordinal).Count() != all.Count) errors.Add("WOM account names must be normalized-unique; correct duplicate or ambiguous spellings.");
        if (!HasOrderedAssociation(all, "Ezzi", "Also Ezzi") || !HasOrderedAssociation(all, "wolles", "w olles")) errors.Add("Approved account associations must be Ezzi primary → Also Ezzi secondary and wolles primary → w olles secondary.");
        var cox = all.FirstOrDefault(value => string.Equals(value.Account.Username, "Coxophobia", StringComparison.OrdinalIgnoreCase));
        var coxIsSecondary = cox.Account is not null && cox.Participant.Accounts?.Count == 2 &&
                             string.Equals(cox.Participant.Accounts?.Skip(1).FirstOrDefault()?.Username, cox.Account.Username, StringComparison.OrdinalIgnoreCase);
        if (!coxIsSecondary || cox.Account?.GainedEhb != 0 || !string.Equals(cox.Participant.TeamSlug, "xen0-d-rops", StringComparison.Ordinal)) errors.Add("Coxophobia must be an unused zero-gain secondary account attached to an existing Xen participant.");
        if (participants.Count(value => value.Accounts?.Count == 2) != 3) errors.Add("The private mapping must contain exactly the three approved secondary associations and no others.");
        foreach (var team in manifest.Teams ?? [])
            if (participants.Count(value => value.TeamSlug == team.Slug) != 15) errors.Add($"Team '{team.Name}' must have exactly 15 participants.");
    }

    private static bool HasOrderedAssociation(IEnumerable<(HistoricalParticipant Participant, HistoricalAccount Account)> values, string primary, string secondary)
    {
        var primaryRow = values.FirstOrDefault(value => string.Equals(value.Account.Username, primary, StringComparison.OrdinalIgnoreCase));
        if (primaryRow.Account is null)
            return false;
        var accounts = primaryRow.Participant.Accounts;
        return accounts is { Count: 2 } &&
               string.Equals(accounts[0].Username, primary, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(accounts[1].Username, secondary, StringComparison.OrdinalIgnoreCase);
    }

    private static ResolvedDrop ToResolvedDrop(Bingo.Domain.Catalogue.SourceDrop drop, Bingo.Domain.Catalogue.BossActivity boss, Bingo.Domain.Catalogue.CatalogueItem item) =>
        new(drop.Id, item.Id, boss.Name, item.Name, drop.DisplayRate, drop.NumericProbability, drop.DefaultEhbEstimate,
            EfficientCompletionsPerHour: boss.EfficientCompletionsPerHour, RollsPerCompletion: drop.RollsPerCompletion,
            RollGroup: drop.RollGroup, RateCondition: drop.RateConditionNote, ProbabilityScope: drop.ProbabilityScope,
            ConditionalOnParent: drop.ConditionalOnParent, ParentProbability: drop.ParentProbability,
            AssumedParticipants: drop.AssumedParticipants, DataSource: drop.DataSource, ImageUrl: item.ImageUrl,
            CatalogueVersion: drop.Version, BossId: boss.Id);

    private static string Fingerprint(IEnumerable<EventParticipantCharacter> assignments) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', assignments.OrderBy(value => value.Id).Select(value => $"{value.Id:N}:{value.EventParticipantId:N}:{value.OsrsCharacterId:N}"))))).ToLowerInvariant();

    private static WeightedParticipant ChooseWeighted(IReadOnlyList<WeightedParticipant> values, decimal totalWeight, int sequence)
    {
        var needle = ((sequence + 0.5m) * totalWeight / 17m) % totalWeight;
        var cumulative = 0m;
        foreach (var value in values.OrderBy(value => value.Input.ParticipantKey, StringComparer.Ordinal))
        {
            cumulative += value.Weight;
            if (needle < cumulative) return value;
        }
        return values.OrderBy(value => value.Input.ParticipantKey, StringComparer.Ordinal).Last();
    }

    private static Guid StableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("historical-import:v1:" + value));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static bool SetEquals(List<string>? left, IReadOnlyCollection<string> right) => left is not null && left.Count == right.Count && left.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(right);

    private sealed record ResolvedImport(IReadOnlyList<ResolvedTile> Tiles, Guid? ExistingEventId, string ImportHash);
    private sealed record ResolvedTile(HistoricalTile Manifest, IReadOnlyList<ResolvedRequirement> Requirements, decimal Ehb);
    private sealed record ResolvedRequirement(HistoricalRequirement Manifest, IReadOnlyList<Bingo.Domain.Catalogue.BossActivity> Bosses, IReadOnlyList<ResolvedDrop> Drops, decimal? Estimate);
    private sealed record ResolvedDrop(Guid SourceDropId, Guid ItemId, string BossName, string ItemName, string DisplayRate, decimal? NumericProbability, decimal? DefaultEhb,
        decimal? EfficientCompletionsPerHour = null, int RollsPerCompletion = 1, string RollGroup = "default", string? RateCondition = null,
        DropProbabilityScope ProbabilityScope = DropProbabilityScope.Participant, bool ConditionalOnParent = false, decimal? ParentProbability = null,
        int AssumedParticipants = 1, string? DataSource = null, string? ImageUrl = null, long CatalogueVersion = 1, Guid BossId = default);
    private sealed record WeightedParticipant(HistoricalParticipant Input, decimal Weight);

    private sealed class HistoricalManifest { public int SchemaVersion { get; set; } public HistoricalEvent? Event { get; set; } public string? Disclosure { get; set; } public List<HistoricalTeam>? Teams { get; set; } public List<HistoricalTile>? Tiles { get; set; } }
    private sealed class HistoricalEvent { public string? Name { get; set; } public string? Slug { get; set; } public string? Timezone { get; set; } public DateTimeOffset StartsAt { get; set; } public DateTimeOffset EndsAt { get; set; } public DateTimeOffset ArchivedAt { get; set; } public long CompetitionId { get; set; } public string? CompetitionTitle { get; set; } public string? SourceEventId { get; set; } public string? SourceUrl { get; set; } }
    private sealed class HistoricalTeam { public string? Name { get; set; } public string? Slug { get; set; } public int Placement { get; set; } public List<int>? Counters { get; set; } }
    private sealed class HistoricalTile { public string? Name { get; set; } public decimal? Ehb { get; set; } public List<HistoricalRequirement>? Requirements { get; set; } }
    private sealed class HistoricalRequirement { public List<string>? Bosses { get; set; } public int Target { get; set; } public bool Duplicates { get; set; } public bool HigherWeights { get; set; } public bool Manual { get; set; } public string? Description { get; set; } public List<string>? Items { get; set; } public Dictionary<string, int>? Weights { get; set; } }
    private sealed class HistoricalInput { public string? SourceEventId { get; set; } public long CompetitionId { get; set; } public string? CompetitionTitle { get; set; } public DateTimeOffset StartsAt { get; set; } public DateTimeOffset EndsAt { get; set; } public List<HistoricalParticipant>? Accounts { get; set; } }
    private sealed class HistoricalParticipant { public string? ParticipantKey { get; set; } public string? DisplayName { get; set; } public string? TeamSlug { get; set; } public List<HistoricalAccount>? Accounts { get; set; } }
    private sealed class HistoricalAccount { public string? Username { get; set; } public decimal? StartEhb { get; set; } public decimal? EndEhb { get; set; } public decimal? GainedEhb { get; set; } public DateTimeOffset? FetchedAt { get; set; } public DateTimeOffset? UpstreamUpdatedAt { get; set; } }
}
