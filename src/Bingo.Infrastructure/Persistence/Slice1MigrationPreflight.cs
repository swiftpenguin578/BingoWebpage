using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Persistence;

/// <summary>Read-only retained-database gate. It deliberately refuses to choose an owner or repair data.</summary>
public sealed class Slice1MigrationPreflight(ApplicationDbContext db)
{
    public async Task<string> RunAsync(string? selectedOwner, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection(); await connection.OpenAsync(ct);
        try
        {
            var rows = new List<(string Id, string Username, string Role, string? Event, string? Team, string? Participant)>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id::text, username, role, event_id::text, team_id::text, captain_participant_id::text FROM accounts ORDER BY username";
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5)));
            }
            var duplicate = rows.GroupBy(x => x.Username.Trim().ToUpperInvariant()).FirstOrDefault(x => x.Count() > 1); if (duplicate is not null) throw new InvalidOperationException($"Normalized login collision: {duplicate.Key}.");
            foreach (var captain in rows.Where(x => x.Role == "Captain"))
            {
                if (captain.Event is null || captain.Team is null || captain.Participant is null) throw new InvalidOperationException($"Captain {captain.Username} has ambiguous scope.");
                await using var membership = connection.CreateCommand();
                membership.CommandText = "SELECT count(*) FROM team_memberships m JOIN teams t ON t.id = m.team_id JOIN event_participants p ON p.id = m.event_participant_id JOIN events e ON e.id = t.event_id WHERE m.team_id = @team::uuid AND m.event_participant_id = @participant::uuid AND m.left_at IS NULL AND m.role IN ('Captain', 'CoCaptain') AND t.event_id = @event::uuid AND p.event_id = @event::uuid AND e.id = @event::uuid";
                var team = membership.CreateParameter(); team.ParameterName = "team"; team.Value = captain.Team; membership.Parameters.Add(team);
                var participant = membership.CreateParameter(); participant.ParameterName = "participant"; participant.Value = captain.Participant; membership.Parameters.Add(participant);
                var eventId = membership.CreateParameter(); eventId.ParameterName = "event"; eventId.Value = captain.Event; membership.Parameters.Add(eventId);
                if (Convert.ToInt32(await membership.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) != 1) throw new InvalidOperationException($"Captain {captain.Username} has no authoritative active captain/co-captain membership.");
            }
            var admins = rows.Where(x => x.Role == "Admin").ToList(); var matches = admins.Where(x => x.Id == selectedOwner || string.Equals(x.Username, selectedOwner, StringComparison.Ordinal)).ToList(); if (string.IsNullOrWhiteSpace(selectedOwner) || matches.Count != 1) throw new InvalidOperationException("Supply an explicit existing Admin ID or username for initial Super Admin ownership."); var selected = matches[0];
            await using (var owner = connection.CreateCommand()) { owner.CommandText = "SELECT disabled_at IS NULL FROM accounts WHERE id = @id::uuid"; var parameter = owner.CreateParameter(); parameter.ParameterName = "id"; parameter.Value = selected.Id; owner.Parameters.Add(parameter); if (await owner.ExecuteScalarAsync(ct) is not bool enabled || !enabled) throw new InvalidOperationException("The selected retained owner must be enabled."); }
            return string.Join(Environment.NewLine, rows.Select(x => $"{x.Role}: {x.Id} {x.Username} event={x.Event ?? "-"} team={x.Team ?? "-"} participant={x.Participant ?? "-"}"));
        }
        finally { await connection.CloseAsync(); }
    }

    public async Task<string> RunImmutableItemPreflightAsync(CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection(); await connection.OpenAsync(ct);
        try { return (await InspectImmutableItemSnapshotsAsync(connection, ct)).Report; }
        finally { await connection.CloseAsync(); }
    }

    public static async Task StageImmutableItemMappingsAsync(DbConnection connection, string? mappingPath, string? expectedHash, CancellationToken ct)
    {
        var inspection = await InspectImmutableItemSnapshotsAsync(connection, ct);
        ImmutableItemMappingFile? mapping = null;
        if (inspection.FlaggedRows.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(mappingPath)) throw new InvalidOperationException($"Immutable catalogue item identity requires an adjudication mapping. Run --immutable-item-preflight, fill its template, then supply --immutable-item-mapping <path> and --immutable-item-mapping-sha256 <sha256> to --migrate. Database fingerprint: {inspection.DatabaseFingerprint}.");
            var bytes = await File.ReadAllBytesAsync(mappingPath, ct);
            var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(expectedHash) || !string.Equals(actualHash, expectedHash.Trim(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Immutable item mapping hash mismatch. Supplied={expectedHash ?? "-"}; actual={actualHash}.");
            mapping = JsonSerializer.Deserialize<ImmutableItemMappingFile>(bytes, JsonOptions) ?? throw new InvalidOperationException("The immutable item mapping file is empty or invalid JSON.");
            if (!string.Equals(mapping.DatabaseFingerprint, inspection.DatabaseFingerprint, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Immutable item mapping fingerprint mismatch. Supplied={mapping.DatabaseFingerprint}; database={inspection.DatabaseFingerprint}.");
            ValidateMappings(inspection.FlaggedRows, mapping.Rows);
            foreach (var row in mapping.Rows)
            {
                await using var item = connection.CreateCommand(); item.CommandText = "SELECT EXISTS (SELECT 1 FROM catalogue_items WHERE id = @id)"; var parameter = item.CreateParameter(); parameter.ParameterName = "id"; parameter.Value = row.ItemId!.Value; item.Parameters.Add(parameter);
                if (await item.ExecuteScalarAsync(ct) is not bool exists || !exists) throw new InvalidOperationException($"Immutable item mapping references unknown catalogue item {row.ItemId} for {row.SnapshotFamily}/{row.SnapshotId}.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(mappingPath) || !string.IsNullOrWhiteSpace(expectedHash))
        {
            throw new InvalidOperationException("An immutable item mapping was supplied, but this database has no flagged snapshot rows.");
        }

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TEMP TABLE IF NOT EXISTS bingo_item_snapshot_mapping (snapshot_family text NOT NULL, snapshot_id uuid NOT NULL, item_id uuid NOT NULL, PRIMARY KEY (snapshot_family, snapshot_id)); TRUNCATE bingo_item_snapshot_mapping;";
            await create.ExecuteNonQueryAsync(ct);
        }
        if (mapping is null) return;
        foreach (var row in mapping.Rows)
        {
            await using var insert = connection.CreateCommand(); insert.CommandText = "INSERT INTO bingo_item_snapshot_mapping (snapshot_family, snapshot_id, item_id) VALUES (@family, @snapshot, @item)";
            AddParameter(insert, "family", row.SnapshotFamily); AddParameter(insert, "snapshot", row.SnapshotId); AddParameter(insert, "item", row.ItemId!.Value); await insert.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<ImmutableItemSnapshotInspection> InspectImmutableItemSnapshotsAsync(DbConnection connection, CancellationToken ct)
    {
        var eventColumn = await HasColumnAsync(connection, "board_requirement_drop_snapshots", "item_id_snapshot", ct);
        var approvalColumn = await HasColumnAsync(connection, "board_approval_requirement_drop_snapshots", "item_id_snapshot", ct);
        if (eventColumn != approvalColumn) throw new InvalidOperationException("Immutable item snapshot columns are only partially present; restore the migration boundary before retrying.");
        var rows = new List<ImmutableItemSnapshotRow>();
        await using var command = connection.CreateCommand();
        var frozenEvent = eventColumn ? "snapshot.item_id_snapshot" : "NULL::uuid";
        var frozenApproval = approvalColumn ? "snapshot.item_id_snapshot" : "NULL::uuid";
        command.CommandText = $"""
            SELECT 'event', snapshot.id::text, event_item.id::text, event_item.name, requirement.id::text, snapshot.source_drop_id::text, snapshot.item_name,
                   {frozenEvent}, current_item.id::text, current_item.name,
                   COALESCE((SELECT string_agg(item.id::text, ',' ORDER BY item.id) FROM catalogue_items item WHERE item.name = snapshot.item_name), '')
            FROM board_requirement_drop_snapshots snapshot
            JOIN board_requirement_snapshots requirement ON requirement.id = snapshot.requirement_id
            JOIN board_tiles tile ON tile.id = requirement.board_tile_id
            JOIN boards board ON board.id = tile.board_id
            JOIN events event_item ON event_item.id = board.event_id
            LEFT JOIN source_drops current_drop ON current_drop.id = snapshot.source_drop_id
            LEFT JOIN catalogue_items current_item ON current_item.id = current_drop.item_id
            UNION ALL
            SELECT 'approval', snapshot.id::text, event_item.id::text, event_item.name, requirement.board_requirement_snapshot_id::text, snapshot.source_drop_id::text, snapshot.item_name,
                   {frozenApproval}, current_item.id::text, current_item.name,
                   COALESCE((SELECT string_agg(item.id::text, ',' ORDER BY item.id) FROM catalogue_items item WHERE item.name = snapshot.item_name), '')
            FROM board_approval_requirement_drop_snapshots snapshot
            JOIN board_approval_requirement_snapshots requirement ON requirement.id = snapshot.approval_requirement_snapshot_id
            JOIN board_approval_tile_snapshots tile ON tile.id = requirement.approval_tile_snapshot_id
            JOIN board_approval_snapshots approval ON approval.id = tile.approval_snapshot_id
            JOIN boards board ON board.id = approval.board_id
            JOIN events event_item ON event_item.id = board.event_id
            LEFT JOIN source_drops current_drop ON current_drop.id = snapshot.source_drop_id
            LEFT JOIN catalogue_items current_item ON current_item.id = current_drop.item_id
            ORDER BY 1, 2
            """;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var candidateIds = reader.IsDBNull(10) ? [] : reader.GetString(10).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList();
            var frozenId = reader.IsDBNull(7) ? (Guid?)null : Guid.Parse(reader.GetString(7));
            var currentId = reader.IsDBNull(8) ? (Guid?)null : Guid.Parse(reader.GetString(8));
            var currentName = reader.IsDBNull(9) ? null : reader.GetString(9);
            var needsMapping = eventColumn
                ? frozenId is null
                : currentId is null || !string.Equals(currentName, reader.GetString(6), StringComparison.Ordinal) || candidateIds.Count != 1 || candidateIds[0] != currentId;
            rows.Add(new ImmutableItemSnapshotRow(reader.GetString(0), Guid.Parse(reader.GetString(1)), Guid.Parse(reader.GetString(2)), reader.GetString(3), Guid.Parse(reader.GetString(4)), Guid.Parse(reader.GetString(5)), reader.GetString(6), frozenId, currentId, currentName, candidateIds, needsMapping));
        }
        var canonical = string.Join('\n', rows.Select(row => JsonSerializer.Serialize(new { row.SnapshotFamily, row.SnapshotId, row.EventId, row.EventName, row.RequirementId, row.SourceDropId, row.FrozenItemName, row.FrozenItemId, row.CurrentItemId, row.CurrentItemName, row.CandidateItemIds, row.NeedsMapping }, JsonOptions)));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        var flagged = rows.Where(row => row.NeedsMapping).ToList();
        var template = new ImmutableItemMappingFile
        {
            DatabaseFingerprint = fingerprint,
            Rows = flagged.Select(row => new ImmutableItemMappingRow
            {
                SnapshotFamily = row.SnapshotFamily, SnapshotId = row.SnapshotId, ItemId = null,
                EventId = row.EventId, EventName = row.EventName, RequirementId = row.RequirementId,
                SourceDropId = row.SourceDropId, FrozenItemName = row.FrozenItemName,
                CurrentItemId = row.CurrentItemId, CurrentItemName = row.CurrentItemName,
                CandidateItemIds = row.CandidateItemIds
            }).ToList()
        };
        var templateText = JsonSerializer.Serialize(template, JsonOptions);
        return new ImmutableItemSnapshotInspection(fingerprint, rows, flagged, templateText, eventColumn);
    }

    private static void ValidateMappings(IReadOnlyList<ImmutableItemSnapshotRow> flagged, IReadOnlyList<ImmutableItemMappingRow> supplied)
    {
        if (supplied.Any(row => row.ItemId is null || row.ItemId == Guid.Empty)) throw new InvalidOperationException("Every flagged immutable item snapshot row needs a catalogue item ID.");
        var expected = flagged.Select(row => (row.SnapshotFamily, row.SnapshotId)).ToHashSet();
        var actual = supplied.Select(row => (row.SnapshotFamily, row.SnapshotId)).ToList();
        if (actual.Count != actual.Distinct().Count() || actual.Count != expected.Count || actual.Any(key => !expected.Contains(key))) throw new InvalidOperationException("Immutable item mapping row IDs do not exactly match the flagged preflight rows.");
    }

    private static async Task<bool> HasColumnAsync(DbConnection connection, string table, string column, CancellationToken ct)
    {
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = @table AND column_name = @column)"; AddParameter(command, "table", table); AddParameter(command, "column", column); return await command.ExecuteScalarAsync(ct) is bool exists && exists;
    }

    private static void AddParameter(DbCommand command, string name, object value) { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter); }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    private sealed record ImmutableItemSnapshotInspection(string DatabaseFingerprint, IReadOnlyList<ImmutableItemSnapshotRow> Rows, IReadOnlyList<ImmutableItemSnapshotRow> FlaggedRows, string MappingTemplate, bool SnapshotColumnsPresent)
    {
        public string Report => $"Database fingerprint: {DatabaseFingerprint}{Environment.NewLine}Snapshot rows: {Rows.Count}; flagged: {FlaggedRows.Count}; snapshot columns present: {SnapshotColumnsPresent}.{Environment.NewLine}" +
            (FlaggedRows.Count == 0 ? "No immutable catalogue item mappings require adjudication." : string.Join(Environment.NewLine, FlaggedRows.Select(row => $"{row.SnapshotFamily} snapshot={row.SnapshotId} event={row.EventId}/\"{row.EventName}\" requirement={row.RequirementId} sourceDrop={row.SourceDropId} frozenItem=\"{row.FrozenItemName}\" currentMapping={row.CurrentItemId?.ToString() ?? "-"}/\"{row.CurrentItemName ?? "-"}\" candidates={string.Join(',', row.CandidateItemIds)}"))) +
            $"{Environment.NewLine}Mapping template (fill itemId, then hash the exact file bytes for --immutable-item-mapping-sha256):{Environment.NewLine}{MappingTemplate}";
    }

    private sealed record ImmutableItemSnapshotRow(string SnapshotFamily, Guid SnapshotId, Guid EventId, string EventName, Guid RequirementId, Guid SourceDropId, string FrozenItemName, Guid? FrozenItemId, Guid? CurrentItemId, string? CurrentItemName, IReadOnlyList<Guid> CandidateItemIds, bool NeedsMapping);

    public sealed class ImmutableItemMappingFile
    {
        public string DatabaseFingerprint { get; set; } = string.Empty;
        public IReadOnlyList<ImmutableItemMappingRow> Rows { get; set; } = [];
    }

    public sealed class ImmutableItemMappingRow
    {
        public string SnapshotFamily { get; set; } = string.Empty;
        public Guid SnapshotId { get; set; }
        public Guid? ItemId { get; set; }
        public Guid? EventId { get; set; }
        public string? EventName { get; set; }
        public Guid? RequirementId { get; set; }
        public Guid? SourceDropId { get; set; }
        public string? FrozenItemName { get; set; }
        public Guid? CurrentItemId { get; set; }
        public string? CurrentItemName { get; set; }
        public IReadOnlyList<Guid> CandidateItemIds { get; set; } = [];
    }
}
