using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.UI;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Review;

// U8 (R-R15, R-R16): one bounded read of the selected event's submissions, shared by the queue and the
// workspace's Previous/Next. Per-row checks are projections of existing data; no new rule is introduced.
// One after-end boundary everywhere: ActualEndedAt ?? EventEndsAt (BR-3).
public static class ReviewList
{
    public sealed record Gap(DateTimeOffset StartedAt, DateTimeOffset ResumedAt);

    public sealed record Row(Guid Id, DateTimeOffset SubmittedAt, Guid TeamId, string Team, string Account, string Tile,
        string? DropItem, string? Boss, SubmissionStatus Status, bool NoScreenshot, bool AfterEnd, bool Paused, bool SameImage, bool LeftTeam)
    {
        public bool HasChecks => NoScreenshot || AfterEnd || Paused || SameImage || LeftTeam;
    }

    public static DateTimeOffset? EffectiveEnd(BingoEvent item) => item.ActualEndedAt ?? item.EventEndsAt;

    public static bool InGap(IEnumerable<Gap> gaps, DateTimeOffset at) => gaps.Any(gap => at >= gap.StartedAt && at < gap.ResumedAt);

    // Paused intervals: Awaiting final review followed by a return to Live (the event was ended early and resumed).
    public static async Task<IReadOnlyList<Gap>> PausedGapsAsync(ApplicationDbContext db, Guid eventId, CancellationToken ct)
    {
        var transitions = await db.EventStateTransitions.AsNoTracking().Where(x => x.EventId == eventId &&
            (x.ToState == EventState.AwaitingFinalReview || x.ToState == EventState.Live)).OrderBy(x => x.EffectiveAt).ThenBy(x => x.PerformedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var gaps = new List<Gap>();
        DateTimeOffset? started = null;
        foreach (var transition in transitions)
        {
            if (transition.ToState == EventState.AwaitingFinalReview) started = transition.EffectiveAt;
            else if (started is { } from)
            {
                if (transition.EffectiveAt > from) gaps.Add(new Gap(from, transition.EffectiveAt));
                started = null;
            }
        }
        return gaps;
    }

    // The credited participant's team departure: null while any membership of that team is current.
    public static DateTimeOffset? LeftAt(IEnumerable<DateTimeOffset?> memberships)
    {
        var list = memberships.ToList();
        return list.Count == 0 || list.Any(x => x == null) ? null : list.Max();
    }

    public static async Task<IReadOnlyList<Row>> RowsAsync(ApplicationDbContext db, BingoEvent item, CancellationToken ct)
    {
        var submissions = await (from s in db.Submissions.AsNoTracking()
                                 join team in db.Teams.AsNoTracking() on s.TeamId equals team.Id
                                 join tile in db.BoardTiles.AsNoTracking() on s.BoardTileId equals tile.Id
                                 where s.EventId == item.Id
                                 select new { s.Id, s.SubmittedAt, s.TeamId, Team = team.Name, s.CreditedCharacterName, s.CreditedParticipantId, Tile = tile.NameSnapshot, s.DropSnapshotId, s.Status })
            .ToListAsync(ct);
        var ids = submissions.Select(x => x.Id).ToList();
        var assets = await db.EvidenceAssets.AsNoTracking().Where(x => ids.Contains(x.SubmissionId) && x.Active)
            .Select(x => new { x.SubmissionId, x.Checksum }).ToListAsync(ct);
        var sharedChecksums = assets.GroupBy(x => x.Checksum).Where(g => g.Select(x => x.SubmissionId).Distinct().Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var withAsset = assets.Select(x => x.SubmissionId).ToHashSet();
        var sameImage = assets.Where(x => sharedChecksums.Contains(x.Checksum)).Select(x => x.SubmissionId).ToHashSet();
        var teamIds = submissions.Select(x => x.TeamId).Distinct().ToList();
        var memberships = await db.TeamMemberships.AsNoTracking().Where(x => teamIds.Contains(x.TeamId))
            .Select(x => new { x.TeamId, x.EventParticipantId, x.LeftAt }).ToListAsync(ct);
        var departures = memberships.GroupBy(x => (x.TeamId, x.EventParticipantId)).ToDictionary(g => g.Key, g => LeftAt(g.Select(x => x.LeftAt)));
        var dropIds = submissions.Where(x => x.DropSnapshotId != null).Select(x => x.DropSnapshotId!.Value).Distinct().ToList();
        var drops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => dropIds.Contains(x.Id))
            .Select(x => new { x.Id, x.ItemName, x.BossName }).ToDictionaryAsync(x => x.Id, ct);
        var gaps = await PausedGapsAsync(db, item.Id, ct);
        var end = EffectiveEnd(item);
        return submissions
            .Select(s =>
            {
                var drop = s.DropSnapshotId is { } dropId && drops.TryGetValue(dropId, out var found) ? found : null;
                return new Row(s.Id, s.SubmittedAt, s.TeamId, s.Team, s.CreditedCharacterName, s.Tile, drop?.ItemName, drop?.BossName, s.Status,
                    !withAsset.Contains(s.Id), end is { } e && s.SubmittedAt > e, InGap(gaps, s.SubmittedAt), sameImage.Contains(s.Id),
                    departures.GetValueOrDefault((s.TeamId, s.CreditedParticipantId)) is not null);
            })
            .OrderByDescending(x => x.Status == SubmissionStatus.Pending).ThenByDescending(x => x.SubmittedAt).ThenBy(x => x.Id)
            .ToList();
    }

    public static bool Matches(Row row, string search, SubmissionStatus? status) =>
        (status is null || row.Status == status) &&
        (search.Length == 0 || row.Team.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || row.Account.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || row.Tile.Contains(search, StringComparison.CurrentCultureIgnoreCase));

    public static IReadOnlyList<Row> Filter(IEnumerable<Row> rows, string search, SubmissionStatus? status) => rows.Where(row => Matches(row, search, status)).ToList();

    public static string QueueUrl(Guid eventId, string? search, SubmissionStatus? status) => Url("/Admin/Review", eventId, search, status);

    public static string DetailsUrl(Guid id, Guid eventId, string? search, SubmissionStatus? status) => Url($"/Admin/Review/Details/{id:D}", eventId, search, status);

    private static string Url(string path, Guid eventId, string? search, SubmissionStatus? status)
    {
        var query = new List<string> { "eventId=" + eventId.ToString("D") };
        if (!string.IsNullOrWhiteSpace(search)) query.Add("search=" + Uri.EscapeDataString(search.Trim()));
        if (status is { } value) query.Add("status=" + value);
        return path + "?" + string.Join('&', query);
    }
}

// U8-Q1: Review shows UTC first (the OSRS event plugin stamps UTC); the secondary time uses the event's
// own timezone, named by its city, never a fixed city.
public static class ReviewTime
{
    private static System.Globalization.CultureInfo Culture => System.Globalization.CultureInfo.CurrentCulture;
    public static string Utc(DateTimeOffset value) => value.UtcDateTime.ToString("d MMM, HH:mm", Culture) + " UTC";
    public static string UtcFull(DateTimeOffset value) => value.UtcDateTime.ToString("d MMM yyyy, HH:mm", Culture) + " UTC";
    public static string UtcTime(DateTimeOffset value) => value.UtcDateTime.ToString("HH:mm", Culture) + " UTC";
    public static string Local(DateTimeOffset value, string timezone) => DateTimePresentation.Format(value, "HH:mm", timezone, Culture);
    public static string LocalDay(DateTimeOffset value, string timezone) => DateTimePresentation.Format(value, "d MMM, HH:mm", timezone, Culture);
    public static string LocalFull(DateTimeOffset value, string timezone) => DateTimePresentation.Format(value, "d MMM yyyy, HH:mm", timezone, Culture);
    public static string City(string timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone)) return "UTC";
        var city = timezone[(timezone.LastIndexOf('/') + 1)..].Replace('_', ' ');
        return city.Length == 0 ? timezone : city;
    }
}

public static class ReviewStatusText
{
    // "Approved" differs only in case from an existing resource name, so it uses a scoped key (MSB3568).
    public static string Key(Bingo.Domain.Evidence.SubmissionStatus status) => status == Bingo.Domain.Evidence.SubmissionStatus.Approved ? "AdminDesign.Approved" : status.ToString();
}
