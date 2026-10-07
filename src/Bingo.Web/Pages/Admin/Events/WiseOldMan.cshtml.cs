using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Access;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class WiseOldManModel(
    ApplicationDbContext dbContext,
    IEventCompetitionSynchronizationService competitionSynchronization,
    IEventCompetitionManagementService competitionManagement,
    IEventCompetitionActivityProjection activityProjection,
    IStringLocalizer<SharedResource>? text = null,
    IHostEnvironment? environment = null) : PageModel
{
    public EventSummary? EventView { get; private set; }
    public EventCompetitionView? CompetitionIntegration { get; private set; }
    public EventCompetitionManagementView? CompetitionManagement { get; private set; }
    public EventCompetitionActivityProjection? Activity { get; private set; }
    public bool ShowDevelopmentCompetitionControl { get; private set; }

    [BindProperty, Range(1, long.MaxValue)] public long? CompetitionId { get; set; }
    [BindProperty] public long EventVersion { get; set; }
    [BindProperty, DataType(DataType.Password), StringLength(4000), Display(Name = "Wise Old Man management code")]
    public string? CompetitionVerificationCode { get; set; }
    [BindProperty] public bool ConfirmCompetitionClear { get; set; }
    [BindProperty, StringLength(2000)] public string? CompetitionClearReason { get; set; }
    [BindProperty] public bool ConfirmManagedCompetitionDelete { get; set; }
    [BindProperty, Range(1, long.MaxValue)] public long? ManagedCompetitionDeleteId { get; set; }
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
        => await LoadAsync(id, ct) ? Page() : NotFound();

    public async Task<IActionResult> OnGetCurrentAsync(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!await LoadAsync(id, ct)) return NotFound();
        return new JsonResult(new { eventId = id, version = EventVersion.ToString(CultureInfo.InvariantCulture), state = EventView!.State.ToString(), integration = CompetitionIntegration, management = CompetitionManagement, activity = Activity });
    }

    public async Task<IActionResult> OnPostCompetitionAsync(Guid id, CancellationToken ct)
    {
        if (HasBindingErrors(nameof(CompetitionId)) || CompetitionId is null)
            return RedirectWithStatus(id, Localize("Enter a valid competition ID."), UiMessageType.Error);

        try
        {
            var result = await competitionSynchronization.ConfigureAsync(id, EventVersion, CompetitionId, Actor, cancellationToken: ct);
            return RedirectWithStatus(id,
                result.Succeeded ? Localize("Competition linked and validated.") : result.Error ?? Localize("The competition could not be configured."),
                result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        }
        catch (UnauthorizedAccessException exception)
        {
            return RedirectWithStatus(id, exception.Message, UiMessageType.Error);
        }
    }

    public async Task<IActionResult> OnPostDisconnectCompetitionAsync(Guid id, CancellationToken ct)
    {
        if (!ConfirmCompetitionClear)
            return RedirectWithStatus(id, Localize("Confirm Disconnect before removing this external WOM link."), UiMessageType.Warning);

        var current = await competitionSynchronization.GetAsync(id, ct);
        if (current?.Configured != true || current.Provenance != EventCompetitionProvenance.External)
            return RedirectWithStatus(id, Localize("Only an externally-created WOM link can be disconnected here."), UiMessageType.Error);

        try
        {
            var result = await competitionSynchronization.ConfigureAsync(
                id, EventVersion, null, Actor, confirmCompetitionClear: true,
                competitionClearReason: CompetitionClearReason, cancellationToken: ct);
            return RedirectWithStatus(id,
                result.Succeeded ? Localize("The external WOM link was disconnected. The remote competition was not changed.") : result.Error ?? Localize("The external WOM link could not be disconnected."),
                result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        }
        catch (UnauthorizedAccessException exception)
        {
            return RedirectWithStatus(id, exception.Message, UiMessageType.Error);
        }
    }

    public async Task<IActionResult> OnPostFetchCompetitionAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await competitionSynchronization.RefreshAsync(id, Actor, ct);
            var message = result.Succeeded
                ? Localize("WOM data fetch completed.")
                : result.Skipped
                    ? RefreshSkipDescription(result.SkipReason)
                    : CompetitionRefreshFailure(result);
            return RedirectWithStatus(id, message, result.Succeeded ? UiMessageType.Success : UiMessageType.Warning, result.Skipped ? "skipped" : result.Succeeded ? "applied" : "failed", result.ErrorKind, result.SkipReason, result.RetryAt);
        }
        catch (UnauthorizedAccessException exception)
        {
            return RedirectWithStatus(id, exception.Message, UiMessageType.Error);
        }
    }

    public async Task<IActionResult> OnPostCreateManagedCompetitionAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await competitionManagement.CreateAsync(id, EventVersion, Actor, ct);
            return RedirectWithStatus(id,
                result.Succeeded ? Localize("Managed WOM competition creation was accepted and queued.") : LocalizeManagedError(result.ErrorCode, result.Error, "The managed WOM competition could not be created."),
                result.Succeeded ? UiMessageType.Success : UiMessageType.Error, result.Succeeded ? result.Pending ? "queued" : "applied" : "refused", result.ErrorCode, retryAt: result.RetryAt, status: result.Status, operationId: result.OperationId);
        }
        catch (UnauthorizedAccessException exception)
        {
            return RedirectWithStatus(id, exception.Message, UiMessageType.Error);
        }
    }

    public async Task<IActionResult> OnPostAdoptCompetitionCredentialAsync(Guid id, CancellationToken ct)
    {
        if (HasBindingErrors(nameof(CompetitionVerificationCode)))
            return RedirectWithStatus(id, Localize("Enter a valid Wise Old Man management code."), UiMessageType.Error);

        try
        {
            var result = await competitionManagement.ReplaceCredentialAsync(
                id, EventVersion, CompetitionVerificationCode ?? string.Empty, Actor, ct);
            var message = result.Succeeded
                ? result.Status == "Unverified"
                    ? Localize("The management code was stored protected and remains unverified until a legitimate management operation succeeds.")
                    : Localize("The Wise Old Man management code was stored protected.")
                : LocalizeManagedError(result.ErrorCode, result.Error, "The Wise Old Man management code could not be stored.");
            return RedirectWithStatus(id, message, result.Succeeded ? UiMessageType.Success : UiMessageType.Error, result.Succeeded ? result.Pending ? "queued" : "applied" : "refused", result.ErrorCode, retryAt: result.RetryAt, status: result.Status, operationId: result.OperationId);
        }
        catch (UnauthorizedAccessException exception)
        {
            return RedirectWithStatus(id, exception.Message, UiMessageType.Error);
        }
        finally
        {
            // Never allow model-state recovery, redirect data, logging or a
            // rendered response to carry the one-time credential input.
            CompetitionVerificationCode = null;
            ModelState.Remove(nameof(CompetitionVerificationCode));
        }
    }

    public async Task<IActionResult> OnPostDeleteManagedCompetitionAsync(Guid id, CancellationToken ct)
    {
        if (HasBindingErrors(nameof(ManagedCompetitionDeleteId)) || ManagedCompetitionDeleteId is null)
            return RedirectWithStatus(id, Localize("Select the exact managed WOM competition before deleting it."), UiMessageType.Error);

        try
        {
            var result = await competitionManagement.DeleteAsync(
                id, EventVersion, ManagedCompetitionDeleteId.Value, ConfirmManagedCompetitionDelete, Actor, ct);
            return RedirectWithStatus(id,
                result.Succeeded ? Localize("Website-created WOM deletion was accepted and queued.") : LocalizeManagedError(result.ErrorCode, result.Error, "The managed WOM competition could not be deleted."),
                result.Succeeded ? UiMessageType.Success : UiMessageType.Error, result.Succeeded ? result.Pending ? "queued" : "applied" : "refused", result.ErrorCode, retryAt: result.RetryAt, status: result.Status, operationId: result.OperationId);
        }
        catch (UnauthorizedAccessException exception)
        {
            return RedirectWithStatus(id, exception.Message, UiMessageType.Error);
        }
    }

    public async Task<IActionResult> OnPostMakeDevelopmentCompetitionDueAsync(Guid id, CancellationToken ct)
    {
        if (environment?.IsDevelopment() != true) return NotFound();
        try
        {
            var madeDue = await competitionSynchronization.MakeDevelopmentRefreshDueAsync(id, Actor, ct);
            return RedirectWithStatus(id,
                madeDue ? Localize("Development TEST 15 competition refresh is due.") : Localize("The Development TEST 15 refresh control is unavailable."),
                madeDue ? UiMessageType.Success : UiMessageType.Error);
        }
        catch (UnauthorizedAccessException exception)
        {
            return RedirectWithStatus(id, exception.Message, UiMessageType.Error);
        }
    }

    public string FetchStatusLabel => CompetitionIntegration is null || !CompetitionIntegration.Configured
        ? Localize("Configuration")
        : CompetitionIntegration.LastErrorKind switch
        {
            "RateLimited" => Localize("Rate limited"),
            "Unavailable" => Localize("Unavailable"),
            "Permanent" => Localize("Configuration"),
            _ => Activity?.State switch
            {
                EventCompetitionActivityState.WaitingForFirstSync => Localize("First fetch pending"),
                EventCompetitionActivityState.Partial => Localize("Partial"),
                EventCompetitionActivityState.Incomplete => Localize("Incomplete"),
                EventCompetitionActivityState.TemporarilyUnavailable => Localize("Unavailable"),
                EventCompetitionActivityState.Stale => Localize("Stale"),
                EventCompetitionActivityState.Complete => Localize("Complete"),
                _ => Localize("First fetch pending")
            }
        };

    public string ProvenanceLabel => CompetitionIntegration?.Provenance switch
    {
        EventCompetitionProvenance.WebsiteCreated => Localize("Website-created · manageable").ToString(),
        EventCompetitionProvenance.External when CompetitionIntegration.WriteCapability == EventCompetitionWriteCapability.Writable => Localize("External · manageable").ToString(),
        EventCompetitionProvenance.External => Localize("External · read-only").ToString(),
        _ => Localize("Unknown provenance · read-only").ToString()
    };

    public string CapabilityExplanation => CompetitionIntegration?.Provenance == EventCompetitionProvenance.External
        && CompetitionIntegration.WriteCapability != EventCompetitionWriteCapability.Writable
        ? Localize("This externally-created link can fetch and display WOM data. It cannot synchronize local changes or queue update-all writes upstream.")
        : CompetitionIntegration?.Provenance == EventCompetitionProvenance.External
            ? Localize("This externally-created link has a protected management code. Schedule, roster and update-all writes are available when the credential is validated; remote deletion is never available.")
            : Localize("A website-created competition can be managed here while the event is pre-Live. Its remote deletion is available only before the event has ever been Live.");

    public string ManagedStatusLabel => CompetitionManagement?.Status switch
    {
        "Pending" => Localize("Queued"),
        "Unknown" => Localize("Unknown outcome"),
        "Active" => Localize("Active"),
        "Failed" => Localize("Failed"),
        "Conflict" => Localize("Conflict"),
        "Deleted" => Localize("Deleted"),
        _ => Localize("Not configured")
    };

    public string CredentialLabel => CompetitionManagement?.CredentialState switch
    {
        nameof(EventCompetitionCredentialStatus.Unverified) => Localize("Supplied · unverified"),
        nameof(EventCompetitionCredentialStatus.Valid) => Localize("Validated"),
        nameof(EventCompetitionCredentialStatus.Invalid) => Localize("Rejected"),
        nameof(EventCompetitionCredentialStatus.Revoked) => Localize("Revoked"),
        nameof(EventCompetitionCredentialStatus.Unavailable) => Localize("Validation unavailable · unverified"),
        _ => Localize("None supplied")
    };

    public string LocalizeManagedError(string? code, string? message, string fallback)
    {
        if (string.Equals(code, "SharedSource", StringComparison.Ordinal)) return Localize("This managed WOM competition is referenced by another event; automatic updates are paused.");
        if (string.Equals(code, "ExternalDrift", StringComparison.Ordinal)) return Localize("The managed WOM competition changed outside Bingo; automatic updates are paused for Admin review.");
        if (string.Equals(code, "SourceMissing", StringComparison.Ordinal)) return Localize("The managed WOM competition no longer exists; automatic updates are paused.");
        if (string.Equals(code, "UnknownOutcome", StringComparison.Ordinal) || string.Equals(code, "ClaimExpired", StringComparison.Ordinal) || string.Equals(code, "ReconciliationUnavailable", StringComparison.Ordinal)) return Localize("The WOM operation outcome is uncertain and is being checked without another write.");
        if (string.Equals(code, "DeleteStillPresent", StringComparison.Ordinal)) return Localize("The WOM competition still exists; deletion was not retried and will be checked again.");
        if (string.Equals(code, "DeleteReconciliationRequired", StringComparison.Ordinal) || string.Equals(code, "ReconciliationRequired", StringComparison.Ordinal)) return Localize("The WOM operation outcome remains unresolved; Admin review is required.");
        if (string.Equals(code, "AdminRevoked", StringComparison.Ordinal)) return Localize("The originating Admin is no longer enabled for this WOM operation.");
        if (string.Equals(code, "StaleCreate", StringComparison.Ordinal) || string.Equals(code, "StaleUpdate", StringComparison.Ordinal) || string.Equals(code, "StaleDelete", StringComparison.Ordinal)) return Localize("The event changed before the WOM operation was dispatched; review the current values and retry.");
        if (string.Equals(code, "InvalidConfiguration", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(message))
            return string.Join(" ", message.Split(". ", StringSplitOptions.RemoveEmptyEntries).Select(part => LocalizeManagedErrorText(part.EndsWith('.') ? part : part + ".", fallback)));
        if (!string.IsNullOrWhiteSpace(code)) return Localize("The managed WOM operation needs Admin attention.");
        return string.IsNullOrWhiteSpace(message) ? Localize(fallback) : LocalizeManagedErrorText(message, fallback);
    }

    private async Task<bool> LoadAsync(Guid id, CancellationToken ct)
    {
        var item = await dbContext.Events.AsNoTracking()
            .Where(value => value.Id == id && value.State != EventState.Discarded && value.HiddenAt == null)
            .Select(value => new EventSummary(value.Id, value.Name, value.Slug, value.State, value.Version, value.EventStartsAt, value.EventEndsAt, value.ActualStartedAt, value.HiddenAt != null))
            .SingleOrDefaultAsync(ct);
        if (item is null) return false;

        EventView = item;
        CompetitionIntegration = await competitionSynchronization.GetAsync(id, ct);
        CompetitionManagement = await competitionManagement.GetAsync(id, ct);
        Activity = await activityProjection.GetAsync(id, ct);
        ShowDevelopmentCompetitionControl = environment?.IsDevelopment() == true && item.State == EventState.Live && item.Slug == "test-15-dkl-live" && CompetitionIntegration?.Configured == true;
        EventVersion = item.Version;
        CompetitionId = CompetitionIntegration?.CompetitionId;
        return true;
    }

    private string CompetitionRefreshFailure(EventCompetitionRefreshResult result)
    {
        var retryAt = result.RetryAt is { } value ? DateTimePresentation.Format(value, "dd MMM yyyy, HH:mm", provider: CultureInfo.CurrentCulture) : null;
        return result.ErrorKind switch
        {
            "RateLimited" when retryAt is not null => Localize("WOM data is temporarily rate-limited. Try again after {0}.", retryAt),
            "RateLimited" => Localize("WOM data is temporarily rate-limited."),
            "NotFound" => Localize("Wise Old Man could not find that competition."),
            "Invalid" => Localize("Wise Old Man returned invalid competition details."),
            _ when retryAt is not null => Localize("WOM data is temporarily unavailable. Try again after {0}.", retryAt),
            _ => Localize("WOM data fetch failed.")
        };
    }

    private string LocalizeManagedErrorText(string message, string? fallback = null)
    {
        const string emptyTeamPrefix = "Cannot create WOM competition with empty teams. Affected team: ";
        if (message.StartsWith(emptyTeamPrefix, StringComparison.Ordinal))
            return Localize("Cannot create WOM competition with empty teams. Affected team: {0}.", message[emptyTeamPrefix.Length..].TrimEnd('.'));
        const string assignmentPrefix = "Participant ";
        const string assignmentSeparator = " has no eligible Playing assignment for team ";
        if (message.StartsWith(assignmentPrefix, StringComparison.Ordinal) && message.Contains(assignmentSeparator, StringComparison.Ordinal))
        {
            var separator = message.IndexOf(assignmentSeparator, StringComparison.Ordinal);
            return Localize("Participant {0} has no eligible Playing assignment for team {1}.", message[assignmentPrefix.Length..separator], message[(separator + assignmentSeparator.Length)..].TrimEnd('.'));
        }
        return message switch
        {
            "The event name must be between 1 and 50 characters for WOM." => Localize(message),
            "The WOM competition end must be after its start." => Localize(message),
            "The WOM competition schedule must be in the future." => Localize(message),
            "A WOM competition can only be created before the event starts." => Localize(message),
            "The event must be visible before WOM management." => Localize(message),
            "The event must have a configured start and end before WOM management." => Localize(message),
            "The event must have at least one active team before WOM management." => Localize(message),
            _ => Localize(fallback ?? "The managed WOM operation needs Admin attention.")
        };
    }

    public string RefreshSkipDescription(EventCompetitionRefreshSkipReason? reason) => Localize(reason switch
    {
        EventCompetitionRefreshSkipReason.WithinHour => "The last successful fetch was less than an hour ago.",
        EventCompetitionRefreshSkipReason.RetryDelay => "An automatic retry is pending.",
        EventCompetitionRefreshSkipReason.NotDue => "The next fetch slot is not due yet.",
        EventCompetitionRefreshSkipReason.RefreshInProgress => "A fetch is already running. Its lease expires at the next eligible time.",
        EventCompetitionRefreshSkipReason.EventUnavailable => "Fetching is unavailable in this event state.",
        EventCompetitionRefreshSkipReason.NoCompetition => "Link a competition before fetching data.",
        EventCompetitionRefreshSkipReason.IncompleteEventWindow => "The event start and end must be recorded before fetching data.",
        EventCompetitionRefreshSkipReason.ServiceUnavailable => "The Wise Old Man fetch service is unavailable.",
        EventCompetitionRefreshSkipReason.EndWindowUnmatched => "Fetches are paused until the WOM end matches the event end exactly.",
        EventCompetitionRefreshSkipReason.EndCouldNotBeUpdated => "The WOM end could not be updated. The last fetch before the end is retained.",
        EventCompetitionRefreshSkipReason.EventNotInFinalReview => "The event is not in final review.",
        _ => "WOM data was not fetched. Check the current connection before trying again."
    });

    private bool WantsJson => Request.GetTypedHeaders().Accept?.Any(value => value.MediaType.Value == "application/json") == true;
    private IActionResult RedirectWithStatus(Guid id, string message, UiMessageType type, string? outcome = null,
        string? errorCode = null, EventCompetitionRefreshSkipReason? skipReason = null, DateTimeOffset? retryAt = null, string? status = null, Guid? operationId = null)
    {
        // Never echo a submitted credential, including provider/authorization error text.
        if (!string.IsNullOrEmpty(CompetitionVerificationCode)) message = message.Replace(CompetitionVerificationCode, "[redacted]", StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(CompetitionVerificationCode)) message = message.Replace(CompetitionVerificationCode.Trim(), "[redacted]", StringComparison.Ordinal);
        CompetitionVerificationCode = null;
        ModelState.Remove(nameof(CompetitionVerificationCode));
        if (WantsJson) return new JsonResult(new { succeeded = type == UiMessageType.Success,
            outcome = outcome ?? (type == UiMessageType.Success ? "applied" : "refused"), message,
            error = type == UiMessageType.Success ? null : message, errorCode, skipReason = skipReason?.ToString(), retryAt, status, operationId });
        TempData["StatusMessage"] = message;
        TempData[UiMessage.TypeKey] = type.ToString();
        CompetitionVerificationCode = null;
        return RedirectToPage("/Admin/Events/WiseOldMan", new { id });
    }

    private bool HasBindingErrors(string field) => ModelState.TryGetValue(field, out var entry) && entry.Errors.Count > 0;
    private LifecycleActor Actor => new(User.GetAccountId()!.Value, User.Identity!.Name!);
    private string Localize(string key, params object[] arguments)
        => text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);

    public sealed record EventSummary(Guid Id, string Name, string Slug, EventState State, long Version, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, DateTimeOffset? ActualStartedAt, bool IsHidden);
}
