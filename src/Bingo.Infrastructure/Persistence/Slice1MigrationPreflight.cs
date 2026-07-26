using System.Data.Common;
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
}
