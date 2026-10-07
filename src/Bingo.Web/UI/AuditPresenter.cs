using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Domain.Auditing;
using Bingo.Domain.Evidence;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.UI;

public sealed record AuditFieldChange(string Field, string Before, string After);

public sealed record AuditPresentation(string Action, string Actor, string Target, string? Reason,
    IReadOnlyList<AuditFieldChange> Changes, string ActionKey, string? Details, string? BeforeState, string? AfterState,
    string? LifecycleSummary = null);

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
        ["submission.reversed"] = "Evidence approval reversed"
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
            .Where(key => !Sensitive(key) && before.GetValueOrDefault(key) != after.GetValueOrDefault(key))
            .Select(key => new AuditFieldChange(text[Humanize(key)], Value(before.GetValueOrDefault(key), text), Value(after.GetValueOrDefault(key), text)))
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
            target, sensitiveAction ? null : reason, changes, entry.Action,
            technicalDetails,
            Technical(entry.BeforeState, text), Technical(entry.AfterState, text),
            isCreation ? text["Added"].Value : isDeletion ? text["Deleted"].Value : null);
    }

    private static string Value(string? value, IStringLocalizer<AuditResource> text) => value switch
    {
        null or "null" => "—",
        "true" => text["Yes"],
        "false" => text["No"],
        _ => value
    };

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
        _ => Regex.Replace(key.Replace('_', ' '), "(?<=[a-z0-9])([A-Z])", " $1")
    };

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
