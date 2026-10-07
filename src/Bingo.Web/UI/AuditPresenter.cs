using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Domain.Auditing;
using Bingo.Domain.Evidence;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.UI;

public sealed record AuditFieldChange(string Field, string Before, string After);

public sealed record AuditPresentation(string Action, string Actor, string Target, string? Reason,
    IReadOnlyList<AuditFieldChange> Changes, string ActionKey, string? Details, string? BeforeState, string? AfterState,
    string? LifecycleSummary = null, string? Context = null, bool Sensitive = false, bool TechnicalOnly = false);

/// <summary>Read-only, tolerant projection shared by full Audit and recent activity.</summary>
public static class AuditPresenter
{
    private static readonly string[] SensitiveNames = ["password", "token", "secret", "credential", "code", "hash"];
    // These producers store the supplied reason directly in Details, even if it begins with a brace.
    private static readonly HashSet<string> PlainReasonActions = new(StringComparer.Ordinal)
    {
        "submission.created", "submission.request_changes", "submission.resubmitted", "submission.approved", "submission.rejected", "submission.reversed", "submission.withdrawn", "submission.corrected", "submission.replaced", "submission.duplicate", "submission.hidden", "submission.shown", "submission.contribution_rebalanced", "event.started", "event.ended",
        "event.resumed", "event.cancelled", "event.unfinalized", "account.captain_auto_disabled", "team.member_removed"
    };
    // Owning tickets extend these explicit labels as their mutations are migrated.
    private static readonly IReadOnlyDictionary<string, string> Actions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["account.disabled"] = "Account disabled",
        ["account.restored"] = "Account restored",
        ["account.username_changed"] = "Username changed",
        ["account.role_changed"] = "Account role changed",
        ["account.captain_auto_created"] = "Captain account created",
        ["account.captain_auto_disabled"] = "Captain account disabled",
        ["account.admin_granted"] = "Administrator access granted",
        ["account.admin_revoked"] = "Administrator access revoked",
        ["account.discord_linked"] = "Discord identity linked",
        ["account.discord_unlinked"] = "Discord identity unlinked",
        ["account.discord_replaced"] = "Discord identity replaced",
        ["event.created"] = "Event created",
        ["event.updated"] = "Event updated",
        ["event.hidden"] = "Event hidden",
        ["event.restored"] = "Event restored",
        ["event.started"] = "Event started",
        ["event.ended"] = "Event ended",
        ["event.resumed"] = "Event resumed",
        ["event.cancelled"] = "Event cancelled",
        ["event.unfinalized"] = "Event reopened for final review",
        ["event.final_review_overridden"] = "Final review blocker overridden",
        ["event.completion_time_corrected"] = "Completion time corrected",
        ["event.started_automatically"] = "Event started automatically",
        ["event.finalized"] = "Event finalized",
        ["event.identity_updated"] = "Event identity and time zone changed",
        ["event.submissions_reopened"] = "Submissions reopened",
        ["event.signup_code_changed"] = "Signup code settings changed",
        ["event.evidence_code_mode"] = "Evidence code settings changed",
        ["evidence_code.created"] = "Evidence code created",
        ["catalogue.boss_created"] = "Boss or activity added",
        ["catalogue.boss_updated"] = "Boss or activity updated",
        ["catalogue.boss_deleted"] = "Boss or activity deleted",
        ["catalogue.boss_api_updated"] = "Boss or activity refreshed",
        ["catalogue.boss_api_verified"] = "Boss or activity verified",
        ["catalogue.boss_toggled"] = "Boss or activity availability changed",
        ["catalogue.drop_created"] = "Drop added",
        ["catalogue.drop_updated"] = "Drop updated",
        ["catalogue.drop_deleted"] = "Drop deleted",
        ["catalogue.drop_toggled"] = "Drop availability changed",
        ["catalogue.item_api_updated"] = "Item pricing updated",
        ["catalogue.item_api_refreshed"] = "Item pricing refreshed",
        ["board.published"] = "Board published",
        ["board.updated"] = "Board updated",
        ["team.created"] = "Team created",
        ["team.member_added"] = "Team member added",
        ["team.member_removed"] = "Team member removed",
        ["team.member_moved"] = "Team member moved",
        ["draft.finalized"] = "Draft finalized",
        ["draft.reopened"] = "Draft reopened",
        ["draft.cancelled"] = "Draft cancelled",
        ["submission.created"] = "Evidence submitted",
        ["submission.request_changes"] = "Evidence changes requested",
        ["submission.withdrawn"] = "Evidence withdrawn",
        ["submission.corrected"] = "Evidence metadata corrected",
        ["submission.replaced"] = "Evidence screenshot replaced",
        ["submission.duplicate"] = "Evidence marked duplicate",
        ["submission.hidden"] = "Evidence hidden",
        ["submission.shown"] = "Evidence made visible",
        ["submission.contribution_rebalanced"] = "Evidence contribution recalculated",
        ["submission.resubmitted"] = "Historical linked evidence submitted",
        ["submission.approved"] = "Evidence approved",
        ["submission.rejected"] = "Evidence rejected",
        ["submission.reversed"] = "Evidence approval reversed",
        // S12 (T1): a label for every recorded action key; the completeness test fails on a new unlabelled key.
        ["account.bootstrap_created"] = "First administrator account created",
        ["account.onboarded"] = "Account set up",
        ["account.owner_bootstrapped"] = "Owner account set up",
        ["account.owner_recovered"] = "Owner access recovered",
        ["account.owner_recovery_password_reset"] = "Owner password reset by recovery",
        ["account.owner_recovery_reset_link_created"] = "Owner recovery link created",
        ["account.ownership_transferred"] = "Ownership transferred",
        ["account.password_changed"] = "Password changed",
        ["account.password_reset"] = "Password reset",
        ["account.reset_link_created"] = "Password-reset link created",
        ["account.retained_owner_promoted"] = "Retained owner promoted",
        ["board.approved"] = "Board approved",
        ["board.auto_unapproved"] = "Board approval withdrawn automatically",
        ["board.created"] = "Board created",
        ["board.editing_acquired"] = "Board editing started",
        ["board.editing_released"] = "Board editing released",
        ["board.editing_taken_over"] = "Board editing taken over",
        ["board.expected_team_size_changed"] = "Planning team size changed",
        ["board.published_corrected"] = "Corrected board published",
        ["board.published_correction_discarded"] = "Board correction discarded",
        ["board.published_correction_started"] = "Board correction started",
        ["board.resized"] = "Board resized",
        ["board.tile_created"] = "Tile added",
        ["board.tile_edited"] = "Tile edited",
        ["board.tile_moved"] = "Tile moved",
        ["board.tile_removed"] = "Tile removed",
        ["board.tiles_swapped"] = "Tiles swapped",
        ["board.unapproved"] = "Board approval withdrawn",
        ["catalogue.artwork_reset"] = "Artwork reset",
        ["catalogue.artwork_updated"] = "Artwork updated",
        ["catalogue.event_start_price_checked"] = "Item prices checked at event start",
        ["draft.control_acquired"] = "Draft control taken",
        ["draft.control_released"] = "Draft control released",
        ["draft.control_taken_over"] = "Draft control taken over",
        ["draft.order_scrambled"] = "Draft order shuffled",
        ["draft.pick_recorded"] = "Draft pick recorded",
        ["draft.pick_undone"] = "Draft pick undone",
        ["draft.started"] = "Draft started",
        ["draft.team_removed"] = "Draft team removed",
        ["event.capacity_increased"] = "Participant cap increased",
        ["event.competition_changed"] = "WOM competition changed",
        ["event.competition_cleared"] = "WOM competition cleared",
        ["event.competition_created"] = "WOM competition created",
        ["event.competition_credential_adopted"] = "WOM competition access adopted",
        ["event.competition_credential_replaced"] = "WOM competition access replaced",
        ["event.competition_deleted"] = "WOM competition deleted",
        ["event.competition_linked"] = "WOM competition linked",
        ["event.discarded"] = "Event discarded",
        ["event.ended_automatically"] = "Event ended automatically",
        ["event.legacy_finalized_archived"] = "Legacy finished event archived",
        ["event.results_published"] = "Results published",
        ["event.schedule_updated"] = "Schedule updated",
        ["event.signup_administration_updated"] = "Signup administration updated",
        ["event.signup_closed"] = "Signups closed",
        ["event.signup_closed_automatically"] = "Signups closed automatically",
        ["event.signup_opened"] = "Signups opened",
        ["event.signup_opened_automatically"] = "Signups opened automatically",
        ["event.signup_opening_failed"] = "Automatic signup opening failed",
        ["event.signup_reopened"] = "Signups reopened",
        ["event.start_postponed"] = "Automatic start postponed",
        ["historical_import.applied"] = "Historical event imported",
        ["participant.accounts_changed"] = "Participant accounts changed",
        ["participant.admin_confirmed"] = "Participant confirmed by an admin",
        ["participant.admin_created"] = "Participant added by an admin",
        ["participant.admin_moved_to_waiting"] = "Participant moved to the waiting list",
        ["participant.admin_note_updated"] = "Participant note updated",
        ["participant.admin_restored"] = "Participant restored by an admin",
        ["participant.admin_saved_created"] = "Participant saved by an admin",
        ["participant.admin_withdrawn"] = "Participant withdrawn by an admin",
        ["participant.corrected"] = "Participant corrected",
        ["participant.event_account_added"] = "Event account added",
        ["participant.event_account_corrected"] = "Event account corrected",
        ["participant.event_account_removed"] = "Event account removed",
        ["participant.live_replaced"] = "Participant replaced during the event",
        ["participant.live_withdrawn"] = "Participant withdrawn during the event",
        ["participant.ownership_transferred"] = "Participant ownership transferred",
        ["participant.payment_updated"] = "Payment status updated",
        ["participant.prelive_replaced"] = "Participant replaced before the event",
        ["participant.prelive_withdrawn"] = "Participant withdrawn before the event",
        ["participant.primary_switched"] = "Primary account switched",
        ["participant.promoted"] = "Participant promoted from the waiting list",
        ["participant.promotion_follow_up_completed"] = "Promotion follow-up completed",
        ["participant.rejoined"] = "Participant rejoined",
        ["participant.restored"] = "Participant restored",
        ["participant.waiting"] = "Participant placed on the waiting list",
        ["participant.withdrawn"] = "Participant withdrawn",
        ["roster.finalized_added"] = "Added to a finalized roster",
        ["roster.finalized_removed"] = "Removed from a finalized roster",
        // Composed keys (S12): SignupService writes $"{action}.wom_sync" for the two roster actions.
        ["roster.finalized_added.wom_sync"] = "WOM sync after adding to a finalized roster",
        ["roster.finalized_removed.wom_sync"] = "WOM sync after removing from a finalized roster",
        ["signup_cocaptain.disabled"] = "Co-captain signups turned off",
        ["signup_cocaptain.enabled"] = "Co-captain signups turned on",
        ["signup_question.account_added"] = "Account question added",
        ["signup_question.account_edited"] = "Account question edited",
        ["signup_question.created"] = "Signup question added",
        ["signup_question.deleted"] = "Signup question deleted",
        ["signup_question.edited"] = "Signup question edited",
        ["signup_question.reordered"] = "Signup questions reordered",
        ["submission.historical_action"] = "Historical evidence action",
        ["team.inclusion_changed"] = "Team inclusion changed",
        ["team.membership_role_changed"] = "Team role changed",
        ["team.preformed_corrected"] = "Preformed team corrected",
        ["team.preformed_roster_csv_imported"] = "Preformed rosters imported",
        ["team.role_roster_published"] = "Team roles published",
        ["team.updated"] = "Team updated"
    };

    private static readonly IReadOnlyDictionary<string, string> Targets = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["account"] = "Account",
        ["event"] = "Event",
        ["board"] = "Board",
        ["team"] = "Team",
        ["membership"] = "Membership",
        ["participant"] = "Participant",
        ["submission"] = "Evidence",
        ["draft"] = "Draft",
        ["draft_publication"] = "Draft publication",
        ["boss_activity"] = "Boss or activity",
        ["source_drop"] = "Drop",
        ["catalogue_item"] = "Item",
        ["tile"] = "Tile",
        ["signup_question"] = "Signup question",
        ["competition"] = "WOM competition"
    };

    /// <summary>Every labelled action key (S12) and record type, for filters and the label completeness test.</summary>
    public static IReadOnlyDictionary<string, string> ActionLabels => Actions;
    public static IReadOnlyDictionary<string, string> TargetLabels => Targets;

    public static string ActionKey(ReviewActionType action) => action switch
    {
        ReviewActionType.Submitted => "submission.created",
        ReviewActionType.RequestChanges => "submission.request_changes",
        ReviewActionType.EditMetadata => "submission.corrected",
        ReviewActionType.Approve => "submission.approved",
        ReviewActionType.Reject => "submission.rejected",
        ReviewActionType.MarkDuplicate => "submission.duplicate",
        ReviewActionType.HidePublicEvidence => "submission.hidden",
        ReviewActionType.ShowPublicEvidence => "submission.shown",
        ReviewActionType.ReverseApproval => "submission.reversed",
        ReviewActionType.Withdraw => "submission.withdrawn",
        ReviewActionType.Resubmit => "submission.resubmitted",
        ReviewActionType.ReplaceEvidence => "submission.replaced",
        ReviewActionType.RebalanceContribution => "submission.contribution_rebalanced",
        _ => "submission.historical_action"
    };

    public static AuditEntry FromReviewAction(ReviewAction action, string actorUsername, Guid eventId)
        => new(action.Id, action.PerformedAt, action.PerformedByAccountId, actorUsername, ActionKey(action.Action),
            "submission", action.SubmissionId.ToString("D"), action.Note, eventId, action.BeforeSnapshot, action.AfterSnapshot);

    public static AuditPresentation Present(AuditEntry entry, IStringLocalizer<AuditResource> text)
    {
        var before = ReadSnapshot(entry.BeforeState, "before");
        var after = ReadSnapshot(entry.AfterState, "after");
        var details = ReadFields(entry.Details);
        if (before.Count == 0 && after.Count == 0)
        {
            before = ReadNestedFields(entry.Details, "before");
            after = ReadNestedFields(entry.Details, "after");
        }
        var isCreation = before.Count == 0 && after.Count > 0;
        var isDeletion = before.Count > 0 && after.Count == 0;
        var changes = isCreation || isDeletion
            ? []
            : before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(key => !Sensitive(key) && !IsTechnicalField(key))
            .Select(key => new AuditFieldChange(text[Humanize(key)], Value(before.GetValueOrDefault(key), text), Value(after.GetValueOrDefault(key), text)))
            // T1-8: rows that read the same before and after (including empty → empty) are not changes.
            .Where(change => change.Before != change.After)
            .ToArray();
        var reason = Reason(entry, details);
        var target = text[Targets.GetValueOrDefault(entry.TargetType, "Recorded target")].Value;
        var targetName = after.GetValueOrDefault("Name")
            ?? before.GetValueOrDefault("Name")
            ?? after.FirstOrDefault(pair => pair.Key.EndsWith(" · Name", StringComparison.OrdinalIgnoreCase)).Value
            ?? before.FirstOrDefault(pair => pair.Key.EndsWith(" · Name", StringComparison.OrdinalIgnoreCase)).Value;
        if (!string.IsNullOrWhiteSpace(targetName)) target += $" · {targetName}";
        var targetIdentity = Guid.TryParse(entry.TargetId, out _) ? entry.TargetId : null;
        if (targetIdentity is null && !string.IsNullOrWhiteSpace(entry.TargetId)) target += $" · {entry.TargetId}";
        // Historic code payloads were not always sanitized. Never display their values.
        var sensitiveAction = entry.Action.Contains("code", StringComparison.OrdinalIgnoreCase)
            || entry.Action.Contains("password", StringComparison.OrdinalIgnoreCase)
            || entry.Action.Contains("credential", StringComparison.OrdinalIgnoreCase);
        var technicalDetails = sensitiveAction ? text["Sensitive details withheld"].Value : Technical(entry.Details, text);
        if (targetIdentity is not null)
        {
            var identity = $"{text["Target identity"]}: {targetIdentity}";
            technicalDetails = string.IsNullOrWhiteSpace(technicalDetails) ? identity : $"{identity} · {technicalDetails}";
        }
        return new(text[Actions.GetValueOrDefault(entry.Action, "Recorded administrative action")], entry.ActorUsername,
            target, sensitiveAction ? null : reason, sensitiveAction ? [] : changes, entry.Action,
            technicalDetails,
            Technical(entry.BeforeState, text), Technical(entry.AfterState, text),
            isCreation ? text["Added"].Value : isDeletion ? text["Deleted"].Value : null,
            sensitiveAction ? null : Context(entry, reason, isCreation || isDeletion), sensitiveAction,
            // T1 review L1: values did change, but only in internal fields kept in Technical details.
            TechnicalOnly: !sensitiveAction && !isCreation && !isDeletion && changes.Length == 0 && before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase)
                .Any(key => !Sensitive(key) && IsTechnicalField(key) && before.GetValueOrDefault(key) != after.GetValueOrDefault(key)));
    }

    // Audit drawer "context" (reference Audit.dc.html present()): the reopening explanations, or
    // plain recorded details that are neither the reason nor a creation/deletion marker.
    private static string? Context(AuditEntry entry, string? reason, bool lifecycle)
    {
        if (entry.Details is null) return null;
        if (entry.Action is "draft.reopened" or "event.submissions_reopened" && reason is not null)
            return entry.Details[..entry.Details.IndexOf(';', StringComparison.Ordinal)] + ".";
        return reason is null && !lifecycle && IsPlainText(entry.Details) && !PlainReasonActions.Contains(entry.Action) ? entry.Details : null;
    }

    // T1-8 (a): readable values in Changes. Empty values are a dash; decimals are rounded like the
    // site's EHB values ("0.##", current culture); ISO instants use the drawer's "When" format.
    private static string Value(string? value, IStringLocalizer<AuditResource> text) => value switch
    {
        null or "null" or "" => "—",
        "true" => text["Yes"],
        "false" => text["No"],
        _ when IsoInstant.IsMatch(value) && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant) => When(instant, CultureInfo.CurrentCulture),
        _ when DecimalNumber.IsMatch(value) && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) => number.ToString("0.##", CultureInfo.CurrentCulture),
        _ => value
    };

    private static readonly Regex IsoInstant = new(@"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}", RegexOptions.CultureInvariant);
    private static readonly Regex DecimalNumber = new(@"\A-?\d+\.\d+\z", RegexOptions.CultureInvariant);

    /// <summary>The Audit drawer's "When" format: full Copenhagen date and time with its UTC offset.</summary>
    public static string When(DateTimeOffset value, IFormatProvider? provider = null)
    {
        var local = DateTimePresentation.ToTimezone(value);
        var offset = local.Offset;
        return local.ToString("dddd d MMMM yyyy, HH:mm:ss", provider ?? CultureInfo.CurrentCulture)
            + " (UTC" + (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture) + ")";
    }

    /// <summary>
    /// T1-8 (a): internal bookkeeping fields stay in Technical details only, never in Changes:
    /// identifiers (a name segment "Id", ending "Id"/"_id"/" id"), versions, editing leases and
    /// concurrency markers. Applied to the last segment of a flattened nested name.
    /// </summary>
    public static bool IsTechnicalField(string key)
    {
        var name = key.Split(" · ").Last().Trim();
        name = Regex.Replace(name, @"\s*\[\d+\]\z", string.Empty);
        if (name.Equals("id", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(name, @"(?:[a-z0-9]Id|Ids|[_ ][Ii]d|[_ ][Ii]ds)\z")) return true;
        return TechnicalWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase))
            || name.EndsWith("Version", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_version", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly string[] TechnicalWords = ["lease", "concurrency", "rowversion", "xmin", "etag", "lockedby", "locked_by"];

    private static string? Reason(AuditEntry entry, Dictionary<string, string> details)
    {
        if (PlainReasonActions.Contains(entry.Action)) return entry.Details;
        if (details.TryGetValue("reason", out var structuredReason)) return structuredReason;
        if (entry.Action is "account.disabled" or "event.hidden" or "event.restored")
            return IsPlainText(entry.Details) ? entry.Details : null;
        // Retained production payloads combine context and the user reason. Match their
        // explicit prefixes only; unfamiliar shapes remain available in Technical details.
        var pattern = entry.Action switch
        {
            "draft.reopened" => @"\APublication cycle \d+; (?<reason>.+)\z",
            "event.submissions_reopened" => @"\AUntil [^;]+; (?<reason>.+)\z",
            _ => null
        };
        if (pattern is null || entry.Details is null) return null;
        var match = Regex.Match(entry.Details, pattern, RegexOptions.Singleline);
        return match.Success ? match.Groups["reason"].Value : null;
    }

    private static string Humanize(string key) => key switch
    {
        "Role" => "Account role",
        "State" => "Status",
        "Timezone" => "Time zone",
        "Active" => "Status",
        _ => SentenceCase(Regex.Replace(key.Replace('_', ' '), "(?<=[a-z0-9])([A-Z])", " $1"))
    };

    // README integration 8: split names are sentence-cased ("Completions per hour"); nested
    // " · " paths keep each part's first letter.
    private static string SentenceCase(string value) => string.Join(" · ", value.Split(" · ").Select(part =>
        part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

    private static bool Sensitive(string key) => SensitiveNames
        .Any(word => key.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static bool IsPlainText(string? value) => !string.IsNullOrWhiteSpace(value) && !value.TrimStart().StartsWith('{') && !value.TrimStart().StartsWith('[');

    private static Dictionary<string, string> ReadFields(string? json)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            Flatten(document.RootElement, "", result);
        }
        catch (JsonException) { /* Legacy free text and malformed JSON are valid history, not a page failure. */ }
        return result;
    }

    private static Dictionary<string, string> ReadSnapshot(string? json, string nestedName)
    {
        var nested = ReadNestedFields(json, nestedName);
        return nested.Count > 0 ? nested : ReadFields(json);
    }

    private static Dictionary<string, string> ReadNestedFields(string? json, string name)
    {
        if (string.IsNullOrWhiteSpace(json)) return new(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind == JsonValueKind.Object)
                foreach (var property in document.RootElement.EnumerateObject())
                    if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return ReadFields(property.Value.GetRawText());
        }
        catch (JsonException) { }
        return new(StringComparer.OrdinalIgnoreCase);
    }

    private static void Flatten(JsonElement element, string prefix, Dictionary<string, string> result)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray()) Flatten(item, $"{prefix} [{++index}]", result);
            return;
        }
        if (element.ValueKind != JsonValueKind.Object)
        {
            if (!string.IsNullOrEmpty(prefix)) result[prefix] = element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText();
            return;
        }
        foreach (var property in element.EnumerateObject())
        {
            if (Sensitive(property.Name)) continue;
            var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix} · {property.Name}";
            Flatten(property.Value, key, result);
        }
    }

    private static string? Technical(string? raw, IStringLocalizer<AuditResource> text)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (IsPlainText(raw)) return raw;
        try
        {
            using var document = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 16 });
            return JsonSerializer.Serialize(Sanitize(document.RootElement));
        }
        catch (JsonException) { return text["Historic details could not be interpreted."].Value; }
    }

    private static object? Sanitize(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Where(property => !Sensitive(property.Name))
            .GroupBy(property => property.Name).ToDictionary(group => group.Key, group => Sanitize(group.Last().Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(Sanitize).ToArray(),
        _ => element.Clone()
    };
}
