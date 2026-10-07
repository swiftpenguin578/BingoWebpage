using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

/// <summary>
/// Brief 87 item 0c: the Participants directory read. G3b-3: every participant is
/// listed, including manual-team members, and waiting positions count them.
/// B-Participants-5: search matches every event account RSN, the website username
/// (the Accounts page's username: PublicUsername, else LoginName) and the Discord
/// name. B-Participants-6: only status tabs, Paid/Unpaid, search and sort remain.
/// </summary>
public sealed class ParticipantsListReader(ApplicationDbContext db)
{
    public static readonly int[] PageSizes = [10, 25, 50, 100];
    public const int SearchLimit = 100;

    public async Task<ParticipantsListView> ReadAsync(BingoEvent bingoEvent, ParticipantsQuery query, CancellationToken ct)
    {
        var participants = await db.EventParticipants.AsNoTracking().Where(x => x.EventId == bingoEvent.Id)
            .OrderBy(x => x.SignupSequence).ToListAsync(ct);
        var ids = participants.Select(x => x.Id).ToList();
        var primaries = await db.AdminPrimaryCharacters().AsNoTracking().Where(x => x.EventId == bingoEvent.Id)
            .ToListAsync(ct);
        var primaryById = primaries.GroupBy(x => x.ParticipantId).ToDictionary(x => x.Key, x => x.First());
        var assignments = await (from assignment in db.EventParticipantCharacters.AsNoTracking()
                                 join character in db.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                                 where assignment.EventId == bingoEvent.Id
                                 select new { assignment.EventParticipantId, assignment.OsrsCharacterId, assignment.ReleasedAt, assignment.EventRole, assignment.RegistrationOrder, character.DisplayName }).ToListAsync(ct);
        var ownerIds = participants.Where(x => x.AccountId != null).Select(x => x.AccountId!.Value).Distinct().ToList();
        var owners = await db.Accounts.AsNoTracking().Where(x => ownerIds.Contains(x.Id))
            .Select(x => new { x.Id, Username = x.PublicUsername ?? x.LoginName, x.DiscordDisplayName }).ToDictionaryAsync(x => x.Id, ct);
        var teams = await (from membership in db.TeamMemberships.AsNoTracking()
                           join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
                           where team.EventId == bingoEvent.Id && membership.LeftAt == null
                           select new { membership.EventParticipantId, team.Name }).ToListAsync(ct);
        var teamByParticipant = teams.GroupBy(x => x.EventParticipantId).ToDictionary(x => x.Key, x => x.First().Name);
        var coCaptainQuestion = await db.SignupQuestions.AsNoTracking()
            .Where(x => x.EventId == bingoEvent.Id && x.Active && x.SystemField == SignupSystemField.CoCaptainName).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        var coCaptains = coCaptainQuestion is { } coCaptainId
            ? await db.SignupAnswers.AsNoTracking().Where(x => x.SignupQuestionId == coCaptainId && ids.Contains(x.EventParticipantId))
                .ToDictionaryAsync(x => x.EventParticipantId, x => x.Value, ct)
            : [];
        var waitingPositions = participants.Where(x => x.SignupStatus == SignupStatus.WaitingList)
            .OrderBy(x => x.WaitingListedAt ?? x.SignedUpAt).ThenBy(x => x.SignupSequence).ThenBy(x => x.Id)
            .Select((x, index) => (x.Id, Position: index + 1)).ToDictionary(x => x.Id, x => x.Position);

        var rows = participants.Select(participant =>
        {
            var withdrawn = participant.SignupStatus == SignupStatus.Withdrawn;
            // Current accounts, or for a withdrawn participant the set its withdrawal released.
            var own = assignments.Where(x => x.EventParticipantId == participant.Id &&
                    (withdrawn ? x.ReleasedAt != null && participant.WithdrawnAt != null && x.ReleasedAt >= participant.WithdrawnAt : x.ReleasedAt == null))
                .OrderBy(x => x.RegistrationOrder).ToList();
            var primary = primaryById.GetValueOrDefault(participant.Id);
            var owner = participant.AccountId is { } ownerId ? owners.GetValueOrDefault(ownerId) : null;
            var playingExtra = Math.Max(0, own.Count(x => x.EventRole == EventCharacterRole.Playing) - 1);
            var alt = own.FirstOrDefault(x => x.EventRole == EventCharacterRole.Informational)?.DisplayName;
            return new ParticipantsListRow(
                participant.Id, primary?.Name ?? own.FirstOrDefault()?.DisplayName ?? "—", primary?.Ehb,
                participant.SignupStatus, waitingPositions.TryGetValue(participant.Id, out var position) ? position : null,
                participant.PaymentStatus == PaymentStatus.Paid, !string.IsNullOrEmpty(participant.AdminNotes),
                participant.CaptainVolunteer, participant.CaptainVolunteer ? coCaptains.GetValueOrDefault(participant.Id) : null,
                teamByParticipant.GetValueOrDefault(participant.Id), participant.SignupSequence, participant.SignedUpAt,
                owner?.Username, owner?.DiscordDisplayName, playingExtra, alt, participant.ResponseVersion,
                own.Select(x => x.DisplayName).ToList());
        }).ToList();

        var counts = new ParticipantsCounts(
            rows.Count(x => x.Status == SignupStatus.Confirmed), rows.Count(x => x.Status == SignupStatus.WaitingList),
            rows.Count(x => x.Status == SignupStatus.Withdrawn), rows.Count,
            rows.Count(x => x.Status != SignupStatus.Withdrawn && !x.Paid), bingoEvent.ParticipantCap ?? 0);

        IEnumerable<ParticipantsListRow> filtered = query.Tab switch
        {
            "waiting" => rows.Where(x => x.Status == SignupStatus.WaitingList),
            "withdrawn" => rows.Where(x => x.Status == SignupStatus.Withdrawn),
            "all" => rows,
            _ => rows.Where(x => x.Status == SignupStatus.Confirmed)
        };
        if (query.Pay == "paid") filtered = filtered.Where(x => x.Paid);
        if (query.Pay == "unpaid") filtered = filtered.Where(x => !x.Paid);
        if (query.Search.Length > 0)
            filtered = filtered.Where(x => x.Accounts.Append(x.Username ?? string.Empty).Append(x.DiscordName ?? string.Empty)
                .Any(value => value.Contains(query.Search, StringComparison.OrdinalIgnoreCase)));

        var sort = query.EffectiveSort;
        var descending = query.Descending;
        var rank = new Dictionary<SignupStatus, int> { [SignupStatus.Confirmed] = 0, [SignupStatus.WaitingList] = 1, [SignupStatus.Withdrawn] = 2 };
        Comparison<ParticipantsListRow> compare = sort switch
        {
            "name" => (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
            "status" => (a, b) => rank.GetValueOrDefault(a.Status).CompareTo(rank.GetValueOrDefault(b.Status)) is var c && c != 0 ? c : (a.WaitingPosition ?? 0).CompareTo(b.WaitingPosition ?? 0),
            "ehb" => (a, b) => (a.Ehb ?? -1).CompareTo(b.Ehb ?? -1),
            "paid" => (a, b) => a.Paid.CompareTo(b.Paid),
            "captain" => (a, b) => b.Captain.CompareTo(a.Captain) is var c && c != 0 ? c : (string.IsNullOrEmpty(b.CoCaptain) ? 0 : 1).CompareTo(string.IsNullOrEmpty(a.CoCaptain) ? 0 : 1),
            "team" => (a, b) => string.Compare(a.Team ?? string.Empty, b.Team ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            _ => (_, _) => 0
        };
        var sorted = filtered.ToList();
        sorted.Sort((a, b) =>
        {
            // Rows with a team come before rows without one in either direction (reference).
            if (sort == "team" && (a.Team is null) != (b.Team is null)) return a.Team is null ? 1 : -1;
            var value = compare(a, b) * (descending ? -1 : 1);
            return value != 0 ? value : a.Sequence.CompareTo(b.Sequence);
        });

        var total = sorted.Count;
        var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)query.PerPage));
        var page = Math.Clamp(query.Page, 1, pageCount);
        var pageRows = sorted.Skip((page - 1) * query.PerPage).Take(query.PerPage).ToList();
        return new ParticipantsListView(counts, pageRows, total, page, pageCount, ParticipantRosterPolicy.For(bingoEvent, (key, _) => key).Editable);
    }

    /// <summary>B-Participants-5: Add search by username, Discord name or saved RSN; no "Recently joined" list.</summary>
    public async Task<IReadOnlyList<ParticipantOwnerOption>> SearchOwnersAsync(Guid eventId, string? search, CancellationToken ct)
    {
        search = search?.Trim();
        if (string.IsNullOrEmpty(search) || search.Length > SearchLimit) return [];
        var normalized = search.ToUpperInvariant();
        var pattern = "%" + search.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
        var rsnMatches = await (from link in db.AccountOsrsCharacters.AsNoTracking()
                                join character in db.OsrsCharacters.AsNoTracking() on link.OsrsCharacterId equals character.Id
                                where link.Active && EF.Functions.ILike(character.DisplayName, pattern)
                                select link.AccountId).Distinct().Take(50).ToListAsync(ct);
        var accounts = await db.Accounts.AsNoTracking()
            .Where(x => x.Active && x.AccountType == AccountType.WebsiteAccount &&
                ((x.NormalizedPublicUsername ?? x.NormalizedLoginName).Contains(normalized) ||
                 (x.DiscordDisplayName != null && EF.Functions.ILike(x.DiscordDisplayName, pattern)) ||
                 rsnMatches.Contains(x.Id)))
            .OrderBy(x => x.PublicUsername ?? x.LoginName).Take(8)
            .Select(x => new { x.Id, Username = x.PublicUsername ?? x.LoginName, x.DiscordDisplayName }).ToListAsync(ct);
        var accountIds = accounts.Select(x => x.Id).ToList();
        var saved = await (from link in db.AccountOsrsCharacters.AsNoTracking()
                           join character in db.OsrsCharacters.AsNoTracking() on link.OsrsCharacterId equals character.Id
                           where link.Active && accountIds.Contains(link.AccountId)
                           orderby link.Position
                           select new { link.AccountId, character.DisplayName }).ToListAsync(ct);
        var inEvent = await db.EventParticipants.AsNoTracking().Where(x => x.EventId == eventId && x.AccountId != null && accountIds.Contains(x.AccountId.Value))
            .ToDictionaryAsync(x => x.AccountId!.Value, x => x.SignupStatus, ct);
        return accounts.Select(account =>
        {
            var names = saved.Where(x => x.AccountId == account.Id).Select(x => x.DisplayName).ToList();
            var matched = account.Username.Contains(search, StringComparison.OrdinalIgnoreCase) ? null : names.FirstOrDefault(x => x.Contains(search, StringComparison.OrdinalIgnoreCase));
            var status = inEvent.TryGetValue(account.Id, out var value) ? value == SignupStatus.Withdrawn ? "withdrawn" : "active" : null;
            return new ParticipantOwnerOption(account.Id, account.Username, account.DiscordDisplayName, names, matched, status);
        }).ToList();
    }

    /// <summary>F04: the chosen website account's saved Playing accounts with their stored EHB.</summary>
    public async Task<IReadOnlyList<ParticipantOwnerAccount>> OwnerAccountsAsync(Guid eventId, Guid ownerId, CancellationToken ct)
    {
        var reserved = await db.EventParticipantCharacters.AsNoTracking().Where(x => x.EventId == eventId && x.ReleasedAt == null).Select(x => x.OsrsCharacterId).ToListAsync(ct);
        return await (from link in db.AccountOsrsCharacters.AsNoTracking()
                      join character in db.OsrsCharacters.AsNoTracking() on link.OsrsCharacterId equals character.Id
                      where link.AccountId == ownerId && link.Active
                      orderby link.Position
                      select new ParticipantOwnerAccount(character.Id, character.DisplayName, link.SavedEhb, reserved.Contains(character.Id))).ToListAsync(ct);
    }
}

public sealed record ParticipantsQuery(string Tab, string Pay, string Search, string Sort, bool Descending, int Page, int PerPage)
{
    public static readonly string[] Tabs = ["confirmed", "waiting", "withdrawn", "all"];
    public static readonly string[] Sorts = ["name", "status", "ehb", "paid", "captain", "team", "signup"];

    public static ParticipantsQuery From(string? tab, string? pay, string? search, string? sort, string? direction, string? page, string? perPage)
    {
        var cleanTab = Tabs.Contains(tab) ? tab! : "confirmed";
        var cleanSort = Sorts.Contains(sort) ? sort! : "default";
        var cleanSearch = (search ?? string.Empty).Trim();
        if (cleanSearch.Length > ParticipantsListReader.SearchLimit) cleanSearch = cleanSearch[..ParticipantsListReader.SearchLimit];
        var size = int.TryParse(perPage, out var parsedSize) && ParticipantsListReader.PageSizes.Contains(parsedSize) ? parsedSize : 25;
        var number = int.TryParse(page, out var parsedPage) && parsedPage > 0 ? parsedPage : 1;
        var descending = direction == "desc" || (direction != "asc" && cleanSort == "ehb");
        return new(cleanTab, pay is "paid" or "unpaid" ? pay : "any", cleanSearch, cleanSort, descending, number, size);
    }

    public string EffectiveSort => Sort != "default" ? Sort : Tab is "waiting" or "all" ? "status" : "signup";
    public bool Filtered => Search.Length > 0 || Pay != "any";
}

public sealed record ParticipantsCounts(int Confirmed, int Waiting, int Withdrawn, int All, int Unpaid, int Capacity);
public sealed record ParticipantsListRow(
    Guid Id, string Name, decimal? Ehb, SignupStatus Status, int? WaitingPosition, bool Paid, bool HasAdminNote,
    bool Captain, string? CoCaptain, string? Team, long Sequence, DateTimeOffset SignedUpAt,
    string? Username, string? DiscordName, int ExtraPlaying, string? Alt, int ResponseVersion, IReadOnlyList<string> Accounts);
public sealed record ParticipantsListView(ParticipantsCounts Counts, IReadOnlyList<ParticipantsListRow> Rows, int Total, int Page, int PageCount, bool Editable);
public sealed record ParticipantOwnerOption(Guid Id, string Username, string? DiscordName, IReadOnlyList<string> SavedAccounts, string? MatchedAccount, string? InEvent);
public sealed record ParticipantOwnerAccount(Guid CharacterId, string Name, decimal? SavedEhb, bool InEvent);
