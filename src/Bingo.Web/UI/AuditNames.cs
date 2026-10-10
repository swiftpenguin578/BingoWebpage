using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Domain.Auditing;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.UI;

public sealed record AuditParticipantName(Guid? AccountId, string? Character, Guid EventId);
public sealed record AuditMembershipName(Guid ParticipantId, Guid TeamId);
public sealed record AuditDropName(string? Boss, string? Item);
public sealed record AuditSubmissionName(Guid ParticipantId, Guid TileId, string CharacterName);

/// <summary>
/// Brief 147 item 1: current names for the records an audit page refers to, resolved at display
/// time in one batch per page (a fixed number of queries, never one per entry). Stored audit data
/// is never changed; a record that no longer exists is simply absent and the presenter falls back
/// to the stored name or label, else the id.
/// </summary>
public sealed class AuditNames
{
    public static AuditNames Empty { get; } = new();

    public Dictionary<Guid, string> Accounts { get; } = [];
    public Dictionary<Guid, string> Characters { get; } = [];
    public Dictionary<Guid, AuditParticipantName> Participants { get; } = [];
    public Dictionary<Guid, AuditMembershipName> Memberships { get; } = [];
    public Dictionary<Guid, string> Teams { get; } = [];
    public Dictionary<Guid, Guid> TeamEvents { get; } = [];
    public Dictionary<Guid, Guid> DraftEvents { get; } = [];
    public Dictionary<Guid, string> Events { get; } = [];
    public Dictionary<Guid, string> Boards { get; } = [];
    public Dictionary<Guid, string> Tiles { get; } = [];
    public Dictionary<Guid, string> Questions { get; } = [];
    public Dictionary<Guid, string> Bosses { get; } = [];
    public Dictionary<Guid, string> Items { get; } = [];
    public Dictionary<Guid, AuditDropName> Drops { get; } = [];
    public Dictionary<Guid, AuditSubmissionName> Submissions { get; } = [];

    /// <summary>A6: the event of an entry stored without one, derived for display from its team, membership or draft.</summary>
    public Guid? EventFor(AuditEntry entry)
    {
        if (entry.EventId is { } stored) return stored;
        if (!Guid.TryParse(entry.TargetId, out var id)) return null;
        return entry.TargetType switch
        {
            "team" => TeamEvents.TryGetValue(id, out var teamEvent) ? teamEvent : null,
            "membership" or "draft_publication" => Memberships.TryGetValue(id, out var membership) && TeamEvents.TryGetValue(membership.TeamId, out var membershipEvent) ? membershipEvent
                : TeamEvents.TryGetValue(id, out var publicationTeamEvent) ? publicationTeamEvent : null,
            "draft" => DraftEvents.TryGetValue(id, out var draftEvent) ? draftEvent : null,
            _ => null
        };
    }
}

public static class AuditNameResolver
{
    private static readonly Regex GuidText = new(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.CultureInvariant);

    /// <summary>
    /// Every identifier in the page's entries (target ids and ids inside the stored JSON or text) is
    /// looked up in a fixed set of queries, independent of the number of entries.
    /// </summary>
    public static async Task<AuditNames> ResolveAsync(ApplicationDbContext db, IEnumerable<AuditEntry> entries, CancellationToken ct)
    {
        var names = new AuditNames();
        var ids = new HashSet<Guid>();
        foreach (var entry in entries)
        {
            if (Guid.TryParse(entry.TargetId, out var target)) ids.Add(target);
            if (entry.EventId is { } eventId) ids.Add(eventId);
            foreach (var raw in new[] { entry.Details, entry.BeforeState, entry.AfterState })
                if (raw is not null)
                    foreach (Match match in GuidText.Matches(raw))
                        if (Guid.TryParse(match.Value, out var found)) ids.Add(found);
        }
        if (ids.Count == 0) return names;
        var all = ids.ToArray();

        foreach (var row in await db.Accounts.AsNoTracking().Where(x => all.Contains(x.Id)).Select(x => new { x.Id, Name = x.PublicUsername ?? x.LoginName }).ToListAsync(ct))
            names.Accounts[row.Id] = row.Name;
        foreach (var row in await db.OsrsCharacters.AsNoTracking().Where(x => all.Contains(x.Id)).Select(x => new { x.Id, x.DisplayName }).ToListAsync(ct))
            names.Characters[row.Id] = row.DisplayName;
        foreach (var row in await db.Events.AsNoTracking().Where(x => all.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct))
            names.Events[row.Id] = row.Name;
        foreach (var row in await db.Boards.AsNoTracking().Where(x => all.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct))
            names.Boards[row.Id] = row.Name;
        foreach (var row in await db.BoardTiles.AsNoTracking().Where(x => all.Contains(x.Id)).Select(x => new { x.Id, x.NameSnapshot }).ToListAsync(ct))
            names.Tiles[row.Id] = row.NameSnapshot;
        foreach (var row in await db.SignupQuestions.AsNoTracking().Where(x => all.Contains(x.Id)).Select(x => new { x.Id, x.Label }).ToListAsync(ct))
            names.Questions[row.Id] = row.Label;
        foreach (var row in await db.BossActivities.AsNoTracking().Where(x => all.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct))
            names.Bosses[row.Id] = row.Name;
        foreach (var row in await db.CatalogueItems.AsNoTracking().Where(x => all.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct))
            names.Items[row.Id] = row.Name;
        foreach (var row in await (from drop in db.SourceDrops.AsNoTracking()
                                   where all.Contains(drop.Id)
                                   join boss in db.BossActivities.AsNoTracking() on drop.BossActivityId equals boss.Id into bosses
                                   from boss in bosses.DefaultIfEmpty()
                                   join item in db.CatalogueItems.AsNoTracking() on drop.ItemId equals item.Id into items
                                   from item in items.DefaultIfEmpty()
                                   select new { drop.Id, Boss = boss == null ? null : boss.Name, Item = item == null ? null : item.Name }).ToListAsync(ct))
            names.Drops[row.Id] = new(row.Boss, row.Item);
        foreach (var row in await db.DraftSessions.AsNoTracking().Where(x => all.Contains(x.Id)).Select(x => new { x.Id, x.EventId }).ToListAsync(ct))
            names.DraftEvents[row.Id] = row.EventId;
        foreach (var row in await db.Submissions.AsNoTracking().Where(x => all.Contains(x.Id)).Select(x => new { x.Id, x.CreditedParticipantId, x.BoardTileId, x.CreditedCharacterName }).ToListAsync(ct))
            names.Submissions[row.Id] = new(row.CreditedParticipantId, row.BoardTileId, row.CreditedCharacterName);
        foreach (var row in await db.TeamMemberships.AsNoTracking().Where(x => all.Contains(x.Id)).Select(x => new { x.Id, x.EventParticipantId, x.TeamId }).ToListAsync(ct))
            names.Memberships[row.Id] = new(row.EventParticipantId, row.TeamId);

        // Second wave: participants and teams referenced directly or through memberships and submissions.
        var participantIds = all.Concat(names.Memberships.Values.Select(x => x.ParticipantId)).Concat(names.Submissions.Values.Select(x => x.ParticipantId)).Distinct().ToArray();
        var teamIds = all.Concat(names.Memberships.Values.Select(x => x.TeamId)).Distinct().ToArray();
        foreach (var row in await db.Teams.AsNoTracking().Where(x => teamIds.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.EventId }).ToListAsync(ct))
        {
            names.Teams[row.Id] = row.Name;
            names.TeamEvents[row.Id] = row.EventId;
        }
        var participants = await db.EventParticipants.AsNoTracking().Where(x => participantIds.Contains(x.Id)).Select(x => new { x.Id, x.AccountId, x.EventId }).ToListAsync(ct);
        if (participants.Count > 0)
        {
            var found = participants.Select(x => x.Id).ToArray();
            // The participant's character: the first active playing account, else the most recently
            // released one (a withdrawn participant keeps a readable name).
            var characters = await (from assignment in db.EventParticipantCharacters.AsNoTracking()
                                    where found.Contains(assignment.EventParticipantId) && assignment.EventRole == EventCharacterRole.Playing
                                    join character in db.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                                    select new { assignment.EventParticipantId, assignment.RegistrationOrder, assignment.ReleasedAt, character.DisplayName }).ToListAsync(ct);
            var byParticipant = characters.GroupBy(x => x.EventParticipantId).ToDictionary(group => group.Key, group =>
                group.Where(x => x.ReleasedAt is null).OrderBy(x => x.RegistrationOrder).Select(x => x.DisplayName).FirstOrDefault()
                ?? group.OrderByDescending(x => x.ReleasedAt).ThenBy(x => x.RegistrationOrder).Select(x => x.DisplayName).FirstOrDefault());
            foreach (var row in participants)
                names.Participants[row.Id] = new(row.AccountId, byParticipant.GetValueOrDefault(row.Id), row.EventId);
            var owners = participants.Where(x => x.AccountId is not null).Select(x => x.AccountId!.Value).Where(id => !names.Accounts.ContainsKey(id)).Distinct().ToArray();
            if (owners.Length > 0)
                foreach (var row in await db.Accounts.AsNoTracking().Where(x => owners.Contains(x.Id)).Select(x => new { x.Id, Name = x.PublicUsername ?? x.LoginName }).ToListAsync(ct))
                    names.Accounts[row.Id] = row.Name;
        }
        return names;
    }

    internal static IEnumerable<Guid> GuidsIn(string? raw)
    {
        if (raw is null) yield break;
        foreach (Match match in GuidText.Matches(raw))
            if (Guid.TryParse(match.Value, out var id)) yield return id;
    }

    internal static JsonElement? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try { using var document = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 16 }); return document.RootElement.Clone(); }
        catch (JsonException) { return null; }
    }
}
