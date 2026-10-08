using System.Globalization;
using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Web.UI;

namespace Bingo.Web.Pages.Admin.Events;

public sealed partial class WiseOldManModel
{
    private static readonly JsonSerializerOptions WomJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ClientLabels =
    [
        "Check current state",
        "Update queued",
        "The operation is queued. Check the current state for its result.",
        "AdminDesign.None yet",
        "Not configured",
        "Active",
        "Queued",
        "Sending",
        "Unknown outcome",
        "Failed",
        "Conflict",
        "Deleted",
        "Cancelled",
        "Not connected",
        "Enter a valid competition ID.",
        "Enter a valid Wise Old Man management code.",
        "Action",
        "Fetch now",
        "Fetching…",
        "Management code",
        "Cancel",
        "Update unconfirmed",
        "The current state is unavailable. Check again before trying another action.",
        "Nothing was saved. Your entries are still here.",
        "Check the current state before trying another action.",
        "Next eligible time: {0}",
        "We couldn’t confirm whether new data was fetched.",
        "We couldn’t confirm whether this change was saved.",
        "Current stored connection: {0}. Last successful fetch on record: {1}. Operation status: {2}.",
        "The submitted code was cleared. Enter it again if needed.",
        "The current connection is shown.",
    ];
    public string LabelsJson => JsonSerializer.Serialize(ClientLabels.ToDictionary(key => key, key => text?[key].Value ?? key), WomJsonOptions);
    public string CurrentJson => JsonSerializer.Serialize(new { eventId = EventView!.Id, version = EventVersion.ToString(CultureInfo.InvariantCulture), state = EventView.State.ToString(), integration = CompetitionIntegration, management = CompetitionManagement, activity = Activity }, WomJsonOptions);
    public bool ReadOnly => EventView!.State is EventState.Cancelled or EventState.Finalized or EventState.Archived;
    public bool BeforeLive => EventView!.ActualStartedAt is null && EventView.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed;
    public string ReadOnlyText => Localize(EventView!.State switch { EventState.Cancelled => "This event was cancelled. Its Wise Old Man connection is read-only.", EventState.Finalized => "Results are official. Its Wise Old Man connection is read-only.", _ => "This event is archived. Its Wise Old Man connection is read-only." });
    public string Fmt(DateTimeOffset? value) => value is { } instant ? DateTimePresentation.Format(instant, "dd MMM yyyy, HH:mm", EventView!.Timezone, CultureInfo.CurrentCulture) : Localize("AdminDesign.None yet");
    public string Utc(DateTimeOffset? value) => value?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fffffff 'UTC'", CultureInfo.InvariantCulture) ?? Localize("Not set");
    public EventCompetitionEndUpdateStatus EndStatus => ReadOnly && CompetitionIntegration?.EndUpdateStatus is EventCompetitionEndUpdateStatus.Pending or EventCompetitionEndUpdateStatus.Rejected ? EventCompetitionEndUpdateStatus.CouldNotUpdate : CompetitionIntegration?.EndUpdateStatus ?? EventCompetitionEndUpdateStatus.NotRequired;
    public bool EndNeedsAttention => EndStatus is EventCompetitionEndUpdateStatus.Pending or EventCompetitionEndUpdateStatus.Rejected or EventCompetitionEndUpdateStatus.CouldNotUpdate;
    public string EndTitle => Localize(EndStatus switch { EventCompetitionEndUpdateStatus.Pending => "End update pending", EventCompetitionEndUpdateStatus.Rejected => "End update rejected", EventCompetitionEndUpdateStatus.CouldNotUpdate => "End could not be updated", EventCompetitionEndUpdateStatus.Succeeded => "End update confirmed", _ => "No end update needed" });
    public string EndDescription => Localize(EndStatus == EventCompetitionEndUpdateStatus.CouldNotUpdate ? "The last fetch before the end is retained. This end update will not restart, even if results are reopened." : "Fetches are paused until the WOM end matches the event end exactly. Publishing is still available and retains the last fetch before the end.");
    public bool OperationPending => CompetitionManagement?.OperationPhase is EventCompetitionManagementOperationPhase.Pending or EventCompetitionManagementOperationPhase.Claimed or EventCompetitionManagementOperationPhase.Sending or EventCompetitionManagementOperationPhase.Retry or EventCompetitionManagementOperationPhase.Unknown;
    public WomIssue? MainIssue => CompetitionManagement is { } management && (management.Status == "Unknown" || management.OperationPhase == EventCompetitionManagementOperationPhase.Unknown)
        ? new(Localize("The Wise Old Man operation is unconfirmed."), Localize("It may have reached Wise Old Man. The website is checking without sending it again."), "is-warning")
        : OperationPending && CompetitionManagement?.OperationType == EventCompetitionManagementOperationType.Delete
            ? new(Localize("Deletion is queued."), Localize("The competition stays on Wise Old Man until deletion is confirmed."), "is-info")
        : EndNeedsAttention ? new(EndTitle, EndDescription, "is-warning")
        : CompetitionManagement?.Status is "Conflict" or "Failed" ? new(Localize("Updates need attention"), LocalizeManagedError(CompetitionManagement.LastErrorCode, CompetitionManagement.LastError, "The managed WOM operation needs Admin attention."), "is-warning")
        : CompetitionManagement?.CredentialStatus is EventCompetitionCredentialStatus.Invalid or EventCompetitionCredentialStatus.Revoked
            ? new(Localize("Wise Old Man rejected the management code."), Localize("Website changes are paused until a valid code is saved. Fetching still follows the current eligibility shown below."), "is-warning")
        : CompetitionIntegration?.LastErrorKind == "NotFound" ? new(Localize("Wise Old Man could not find that competition."), Localize("Previously fetched data is retained. Check the competition on Wise Old Man."), "is-error")
        : CompetitionIntegration?.LastErrorKind is "RateLimited" or "Unavailable" ? new(Localize(CompetitionIntegration.LastErrorKind == "RateLimited" ? "Wise Old Man is limiting requests." : "Wise Old Man isn’t responding."), Localize("Previously fetched data is retained. The next permitted fetch is shown below."), "is-warning")
        : EventView!.State == EventState.Live && CompetitionIntegration?.LastSuccessfulAt is { } fetched && fetched < (clock ?? TimeProvider.System).GetUtcNow().AddHours(-1)
            ? new(Localize("The data is more than an hour old."), Localize("The next scheduled fetch is shown below."), "is-info") : null;
    public string UpdateTitle => EndNeedsAttention ? EndTitle : OperationPending ? Localize(CompetitionManagement?.OperationPhase == EventCompetitionManagementOperationPhase.Unknown ? "Update unconfirmed" : "Update queued")
        : ReadOnly || EventView!.State == EventState.AwaitingFinalReview ? Localize("Updates stopped")
        : CompetitionManagement?.Status is "Conflict" or "Failed" || CompetitionManagement?.CredentialStatus is EventCompetitionCredentialStatus.Invalid or EventCompetitionCredentialStatus.Revoked ? Localize("Updates paused")
        : CompetitionManagement?.CanWrite != true ? Localize("Not sent to Wise Old Man") : Localize(CompetitionManagement.LastAppliedAt is null ? "Ready to update" : "Up to date");
    public string UpdateDescription => EndNeedsAttention ? EndDescription : OperationPending ? Localize("Sending the latest details. Not confirmed yet.")
        : ReadOnly || EventView!.State == EventState.AwaitingFinalReview ? Localize("No further website updates are scheduled for this event.")
        : CompetitionManagement?.Status is "Conflict" or "Failed" ? Localize("See the message above. Changes on Wise Old Man are not overwritten here.")
        : CompetitionManagement?.CanWrite != true ? Localize("This connection cannot send website changes to Wise Old Man. A valid management code is required.")
        : Localize(BeforeLive ? "Name, dates and finalized teams update automatically when they change here." : "Teams are fixed since the event went live. Name and dates still update.");
    public string NextFetchNote => ReadOnly ? Localize("No more fetches") : EndNeedsAttention ? Localize("Fetches paused by the unmatched end")
        : BeforeLive ? Localize("Starts when the event goes live") : CompetitionIntegration?.RetryDueAt is not null ? Localize("Automatic retry")
        : Localize(EventView!.State == EventState.AwaitingFinalReview ? "Hourly until results are published" : "Hourly while the event runs");
    public IReadOnlyList<WomCheck> CreateChecks
    {
        get
        {
            var preview = CompetitionManagement?.Preview;
            var errors = preview?.Errors ?? [];
            string[] dateErrors = ["The WOM competition end must be after its start.", "The WOM competition schedule must be in the future.", "The event must have a configured start and end before WOM management."];
            var dates = EventView!.StartsAt is not null && EventView.EndsAt is not null && !errors.Intersect(dateErrors).Any();
            var checks = new List<WomCheck>
            {
                new(Localize(BeforeLive ? "Before the event starts" : "The event has started"), Localize("Create is only available before the event goes live."), BeforeLive, null),
                new(Localize(preview?.IsFinalizedPreLive == true ? "Teams are finalized" : "Teams aren’t finalized yet"), Localize("Close signups and finalize teams with a draft or directly."), preview?.IsFinalizedPreLive == true, $"/Admin/Events/Draft/{EventView.Id}"),
                new(Localize(dates ? "Dates are set and in the future" : "A future schedule is required"), dates ? Fmt(EventView.StartsAt) + " – " + Fmt(EventView.EndsAt) : Localize("Wise Old Man needs a future start and end."), dates, $"/Admin/Events/Schedule/{EventView.Id}")
            };
            foreach (var error in errors.Where(value => !dateErrors.Contains(value) && value != "A WOM competition can only be created before the event starts."))
                checks.Add(new(Localize("Create requirement"), PreviewError(error), false, null));
            if (preview?.HasManualLink == true) checks.Add(new(Localize("An external competition is already linked"), Localize("Disconnect the external link before creating a competition."), false, null));
            if (preview?.HasManagedLink == true) checks.Add(new(Localize("A managed competition already exists"), Localize("Use the existing competition’s management controls."), false, null));
            if (OperationPending) checks.Add(new(Localize("An operation is already being processed"), Localize("Wait until its outcome is confirmed."), false, null));
            return checks;
        }
    }
    public string PreviewError(string error)
    {
        // C-WOM-2: retain each concrete server refusal; translate its known dynamic template.
        (string Prefix, string Suffix, string Key)[] templates = [
            ("Team name '", "' is empty or duplicated after WOM normalization.", "Team name '{0}' is empty or duplicated after WOM normalization."),
            ("Team '", "' must be between 1 and 30 characters for WOM.", "Team '{0}' must be between 1 and 30 characters for WOM."),
            ("Player name '", "' is not a valid WOM name.", "Player name '{0}' is not a valid WOM name."),
            ("Player name '", "' is duplicated after WOM normalization.", "Player name '{0}' is duplicated after WOM normalization."),
            ("Team ", " has no published roster members.", "Team {0} has no published roster members.")];
        foreach (var item in templates)
            if (error.StartsWith(item.Prefix, StringComparison.Ordinal) && error.EndsWith(item.Suffix, StringComparison.Ordinal)) return Localize(item.Key, error[item.Prefix.Length..^item.Suffix.Length]);
        if (error.StartsWith("Published participant ", StringComparison.Ordinal)) return Localize("A published participant has no eligible Playing assignment. Check Teams / Draft.");
        return LocalizeManagedErrorText(error, error);
    }
    public IReadOnlyList<string> MissingFor(EventCompetitionTeamActivity team) => team.Participants.SelectMany(person => (person.PlayingAccountNames ?? []).Except(person.Accounts.Select(account => account.CharacterName), StringComparer.OrdinalIgnoreCase)).ToArray();
    public sealed record WomIssue(string Title, string Description, string Class);
    public sealed record WomCheck(string Title, string Description, bool Done, string? Link);
}
