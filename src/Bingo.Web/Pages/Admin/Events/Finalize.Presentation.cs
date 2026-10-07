using System.Globalization;
using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Domain.Events;
using Bingo.Web.UI;

namespace Bingo.Web.Pages.Admin.Events;

public sealed partial class FinalizeModel
{
    private static readonly JsonSerializerOptions CurrentJsonOptions = new(JsonSerializerDefaults.Web);
    public string CurrentJson => JsonSerializer.Serialize(CurrentState(Readiness), CurrentJsonOptions);
    public FinalizationHistoryRow? ActiveResult => Readiness.History.FirstOrDefault(x => x.Active);
    public bool Official => Readiness.State is EventState.Finalized or EventState.Archived;
    public string Fmt(DateTimeOffset? value) => value is { } instant ? DateTimePresentation.Format(instant, "dd MMM yyyy, HH:mm", EventTimezone, CultureInfo.CurrentCulture) : Localize("Not set");
    public string Badge => Official && ActiveResult is { } active ? Localize("Official · version {0}", active.Version)
        : Readiness.State == EventState.AwaitingFinalReview ? Localize(Readiness.History.Count > 0 ? "Provisional · correction" : "Provisional")
        : Localize(Readiness.State switch { EventState.Draft => "Setup", EventState.SignupOpen => "Signups open", EventState.SignupClosed => "Signups closed", EventState.Live => "Live", EventState.Cancelled => "Cancelled", _ => "Final review" });
    public string BlockingReason => Readiness.BlockingCurrentEvent is not { } current ? string.Empty : current.State switch
    {
        EventState.Live => Localize("{0} is still live. End it first, then publish its results before reopening this event.", current.Name),
        EventState.Finalized => Localize("{0} is still the current event. Contact the Super Admin to archive it.", current.Name),
        _ => Localize("Publish the results of {0} first.", current.Name)
    };
    public string PublicationTitle => Readiness.State == EventState.Live ? Localize("Final review starts when the event ends")
        : Readiness.State != EventState.AwaitingFinalReview ? Localize("No results yet")
        : Readiness.Blockers.Any(x => x.Key != "submission-window") ? Localize("{0} things to resolve before publishing", Readiness.Blockers.Count(x => x.Key != "submission-window"))
        : Readiness.SubmissionWindowOpen ? Localize("Waiting for uploads to close") : Localize("Ready to publish");
    public IReadOnlyList<FinalReviewCheck> Checks
    {
        get
        {
            var used = new HashSet<string>();
            FinalReviewCheck Check(string key, string label, string description)
            {
                var blocker = Readiness.Blockers.FirstOrDefault(x => x.Key == key || x.Key.StartsWith(key + "-", StringComparison.Ordinal));
                if (blocker is null) return new(Localize(label), description, null, key == "submission-window" && Readiness.SubmissionWindowOpen ? "is-wait" : "is-done");
                used.Add(blocker.Key);
                return Blocker(blocker);
            }
            var result = new List<FinalReviewCheck>
            {
                Check("event-state", "Event is in final review", Localize("Ended {0}", Fmt(Readiness.EventEndsAt))),
                Readiness.SubmissionWindowOpen
                    ? Check("submission-window", "Uploads are still open", Localize("Uploads close {0}", Fmt(Readiness.SubmissionCutoff)))
                    : Check("submission-cutoff", "Uploads are closed", Localize("Closed {0}", Fmt(Readiness.SubmissionCutoff))),
                Check("pending-submissions", "No pending submissions", Localize("Every submission has a decision.")),
                Check(Readiness.Blockers.Any(x => x.Key == "calculated-placements") ? "calculated-placements" : "published-board", "Placements calculated", Localize("From the published board and finalized teams."))
            };
            foreach (var blocker in Readiness.Blockers.Where(x => !used.Contains(x.Key))) result.Add(Blocker(blocker));
            return result;
        }
    }
    private FinalReviewCheck Blocker(FinalReviewBlocker blocker)
    {
        var description = blocker.Key == "submission-window" ? Localize("Uploads close {0}", Fmt(Readiness.SubmissionCutoff))
            : blocker.Key.StartsWith("pending-submissions", StringComparison.Ordinal) ? Localize("Every submission must be approved or rejected in Review.") : Localize(blocker.Description);
        return new(Localize(blocker.Title), description, blocker.Link, blocker.Key == "submission-window" ? "is-wait" : "is-blocked");
    }
    public IReadOnlyList<FinalReviewDisplayRow> Rows => PresentRows(Readiness.Placements, Readiness.PlacementRule);
    public IReadOnlyList<FinalReviewDisplayRow> PresentRows(IReadOnlyList<ProvisionalPlacement> rows, PlacementRule rule)
    {
        var shared = rows.GroupBy(x => x.Placement).Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet();
        return rows.Select((row, index) =>
        {
            var neighbour = rows.Count < 2 ? null : rows[index == 0 ? 1 : index - 1];
            var key = neighbour is null || neighbour.Placement == row.Placement ? null : DecisiveInput(row, neighbour, rule);
            var why = neighbour is null ? Localize("The only ranked team")
                : neighbour.Placement == row.Placement ? Localize("Shares place {0} with {1}: equal on every input", row.Placement, neighbour.TeamName)
                : key switch
                {
                    "board" => Localize(row.BoardComplete ? index == 0 ? "First to complete the board" : "Completed the board after {0}" : "No full board", neighbour.TeamName),
                    "lines" => Localize(index == 0 ? "Most completed lines" : "Fewer lines than {0}", neighbour.TeamName),
                    "tiles" => Localize(index == 0 ? "Same lines as {0}; more tiles" : "Same lines; fewer tiles than {0}", neighbour.TeamName),
                    "ehb" => Localize(index == 0 ? "More credited EHB than {0}" : "Less credited EHB than {0}", neighbour.TeamName),
                    "score" => Localize(index == 0 ? "Reached the tied score before {0}" : "Reached the tied score after {0}", neighbour.TeamName),
                    _ => string.Empty
                };
            return new FinalReviewDisplayRow(row, (shared.Contains(row.Placement) ? "=" : "") + row.Placement.ToString(CultureInfo.InvariantCulture), why, key);
        }).ToList();
    }
    public static string? DecisiveInput(ProvisionalPlacement row, ProvisionalPlacement other, PlacementRule rule)
    {
        if (row.BoardComplete != other.BoardComplete || row.BoardComplete && row.CalculatedCompletedAt != other.CalculatedCompletedAt) return "board";
        if (row.CompletedLines != other.CompletedLines) return "lines";
        if (row.CompletedTiles != other.CompletedTiles) return "tiles";
        var ehbDiffers = rule == PlacementRule.CreditedEhbThenScoreTime
            ? decimal.Round(row.EhbTiebreak, 4, MidpointRounding.AwayFromZero) != decimal.Round(other.EhbTiebreak, 4, MidpointRounding.AwayFromZero)
            : row.EhbTiebreak != other.EhbTiebreak;
        var scoreDiffers = (row.BoardComplete ? row.CalculatedCompletedAt : row.CurrentScoreReachedAt) != (other.BoardComplete ? other.CalculatedCompletedAt : other.CurrentScoreReachedAt);
        return rule == PlacementRule.CreditedEhbThenScoreTime ? ehbDiffers ? "ehb" : scoreDiffers ? "score" : null : scoreDiffers ? "score" : ehbDiffers ? "ehb" : null;
    }
    public sealed record FinalReviewCheck(string Title, string Description, string? Link, string Class);
    public sealed record FinalReviewDisplayRow(ProvisionalPlacement Value, string Place, string Explanation, string? Decider);
}
