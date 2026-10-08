using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Bingo.Application.Access;
using Bingo.Application.Signups;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Admin.Events;

// U5 / brief 87: Participants.dc.html on the new layout. The directory is server
// rendered and re-read in place; the drawer, Add and the row actions post JSON
// through AdminFetch. Page state (tab, payment, search, sort, paging, drawer) lives
// in the query (A2) and is bound with [FromQuery].
[AdminDesign]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class ParticipantsModel(
    ApplicationDbContext db,
    ISignupService signupService,
    IStringLocalizer<SharedResource>? text = null) : PageModel
{
    public BingoEvent? Event { get; private set; }
    public ParticipantsQuery Query { get; private set; } = ParticipantsQuery.From(null, null, null, null, null, null, null);
    public ParticipantsListView List { get; private set; } = new(new(0, 0, 0, 0, 0, 0), [], 0, 1, 1, false);
    public ParticipantRosterPolicy Policy { get; private set; } = new(false, false, string.Empty);
    public string EventTimezone { get; private set; } = DateTimePresentation.DefaultTimezoneId;
    public Guid? DrawerParticipant { get; private set; }
    public bool DrawerAdd { get; private set; }
    public ParticipantDrawerView? DrawerView { get; private set; }
    public int PlayingSlots { get; private set; }

    // Compatibility for older direct PageModel tests: the current page rows.
    public IReadOnlyList<ParticipantsListRow> Participants => List.Rows;

    public async Task<IActionResult> OnGetAsync(Guid id,
        [FromQuery(Name = "tab")] string? tab = null, [FromQuery(Name = "pay")] string? pay = null, [FromQuery(Name = "q")] string? search = null,
        [FromQuery(Name = "sort")] string? sort = null, [FromQuery(Name = "dir")] string? direction = null,
        [FromQuery(Name = "page")] string? page = null, [FromQuery(Name = "per")] string? perPage = null,
        [FromQuery(Name = "participant")] Guid? participant = null, [FromQuery(Name = "add")] string? add = null,
        CancellationToken ct = default)
    {
        Query = ParticipantsQuery.From(tab, pay, search, sort, direction, page, perPage);
        if (!await LoadAsync(id, ct)) return NotFound();
        DrawerParticipant = participant;
        DrawerAdd = participant is null && add == "1" && Policy.Editable;
        if (participant is { } selected) DrawerView = await new ParticipantDrawerReader(db, Localize).ReadAsync(Event!, selected, ct);
        return Page();
    }

    // A10 (brief 87 1c): a retired handler name (CreateInternalParticipant, CancelWomValidation)
    // or any other unknown handler is refused before the page could render without its data.
    public override void OnPageHandlerExecuting(Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context)
    {
        if (context.HandlerMethod is null) context.Result = NotFound();
    }

    // U5-Q3: no-store current state of one participant (drawer open and lost-response re-read).
    public async Task<IActionResult> OnGetCurrentAsync(Guid id, Guid participant, CancellationToken ct)
    {
        NoStore();
        var bingoEvent = await VisibleEventAsync(id, ct);
        if (bingoEvent is null) return NotFound();
        var view = await new ParticipantDrawerReader(db, Localize).ReadAsync(bingoEvent, participant, ct);
        return view is null ? NotFound() : new JsonResult(view);
    }

    // B-Participants-5: Add search by username, Discord name or saved RSN.
    public async Task<IActionResult> OnGetSearchOwnerAccountsAsync(Guid id, [FromQuery] string? search, CancellationToken ct)
    {
        NoStore();
        return new JsonResult(await new ParticipantsListReader(db).SearchOwnersAsync(id, search, ct));
    }

    // F04: the chosen website account's saved Playing accounts and stored EHB.
    public async Task<IActionResult> OnGetOwnerAccountsAsync(Guid id, [FromQuery] Guid owner, CancellationToken ct)
    {
        NoStore();
        return new JsonResult(await new ParticipantsListReader(db).OwnerAccountsAsync(id, owner, ct));
    }

    // S4: Paid/Unpaid works in every retained state (Service gate; the service checks).
    public async Task<IActionResult> OnPostPaymentAsync(Guid id, Guid participantId, PaymentStatus payment, CancellationToken ct)
    {
        var result = await signupService.SetPaymentAsync(id, participantId, User.GetAccountId(), User.Identity?.Name ?? "Admin", payment, ct);
        if (WantsJson) return Outcome(result.Succeeded, result.Error);
        SetStatus(result.Succeeded ? Localize("Payment saved.") : result.Error ?? Localize("Payment could not be saved."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToPage(null, null, new { id }, null);
    }

    // Withdrawing a Confirmed participant promotes the next waiter (no suppress option).
    public async Task<IActionResult> OnPostWithdrawAsync(Guid id, Guid participantId, CancellationToken ct, [FromForm] bool confirmLifecycleAction = false)
    {
        if (!confirmLifecycleAction)
        {
            if (WantsJson) return Outcome(false, "Confirm the withdrawal before continuing.");
            SetStatus(Localize("Confirm the withdrawal before continuing."), UiMessageType.Error);
            return RedirectToPage(null, null, new { id }, null);
        }
        if (!await db.EventParticipants.AnyAsync(item => item.Id == participantId && item.EventId == id, ct))
            return WantsJson ? Outcome(false, "This participant isn't part of this event.") : NotFound();
        var result = await signupService.WithdrawAsync(id, participantId, User.GetAccountId(), User.Identity?.Name ?? "Admin", true, cancellationToken: ct);
        if (WantsJson)
        {
            var promoted = result.PromotedParticipantIds is { Count: > 0 } ids ? await NamesAsync(id, ids, ct) : [];
            return Outcome(result.Succeeded, result.Error, new { capacity = result.EffectiveParticipantCap, promoted });
        }
        SetStatus(result.Succeeded ? Localize("Participant withdrawn. The waiting list was promoted where a place became available.") : result.Error ?? Localize("The participant could not be withdrawn."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToPage(null, null, new { id }, null);
    }

    // F01: confirm the selected waiter; "Confirm and add a place" adds exactly one.
    public async Task<IActionResult> OnPostConfirmAsync(Guid id, [FromForm] ParticipantActionInput input, CancellationToken ct)
    {
        var actorId = User.GetAccountId(); if (actorId is null) return Forbid();
        var result = await signupService.ConfirmWaitingParticipantAsync(new(id, input.ParticipantId, actorId.Value, User.Identity?.Name ?? "Admin",
            input.AddPlace, input.EventVersion, input.ResponseVersion), ct);
        return Outcome(result.Succeeded, result.Error, new { capacity = result.EffectiveParticipantCap, addedPlace = result.AddedPlace, changed = result.Changed });
    }

    // F02: full event and another waiter; team membership ends (disclosed in the confirmation).
    public async Task<IActionResult> OnPostMoveToWaitingAsync(Guid id, [FromForm] ParticipantActionInput input, CancellationToken ct)
    {
        var actorId = User.GetAccountId(); if (actorId is null) return Forbid();
        var result = await signupService.MoveConfirmedParticipantToWaitingAsync(new(id, input.ParticipantId, actorId.Value, User.Identity?.Name ?? "Admin",
            input.EventVersion, input.ResponseVersion), ct);
        var promoted = result.PromotedParticipantId is { } next ? await NamesAsync(id, [next], ct) : [];
        return Outcome(result.Succeeded, result.Error, new { waitingPosition = result.WaitingPosition, promoted, changed = result.Changed });
    }

    // F03 / D3: Restore uses stored account data (no WOM); "Restore and add a place" when full.
    public async Task<IActionResult> OnPostRestoreAsync(Guid id, [FromForm] ParticipantActionInput input, CancellationToken ct)
    {
        var actorId = User.GetAccountId(); if (actorId is null) return Forbid();
        var result = await signupService.RestoreAdminParticipantAsync(new(id, input.ParticipantId, actorId.Value, User.Identity?.Name ?? "Admin",
            input.AddPlace, input.EventVersion, input.ResponseVersion), ct);
        return Outcome(result.Succeeded, result.Error, new
        {
            status = result.Status is SignupStatus.WaitingList ? "waiting" : "confirmed", waitingPosition = result.WaitingPosition,
            capacity = result.EffectiveParticipantCap, addedPlace = result.AddedPlace, changed = result.Changed
        });
    }

    // U5-Q2: the drawer's single Save. Classified Service: the signup service allows
    // payment and the private note in every retained state and refuses account or
    // answer changes once the draft has started (D16).
    public async Task<IActionResult> OnPostSaveParticipantAsync(Guid id, [FromForm] ParticipantSaveInput input, CancellationToken ct)
    {
        var actorId = User.GetAccountId();
        if (actorId is null) return Forbid();
        List<AdminDrawerAccount>? playing = null, informational = null;
        Dictionary<Guid, string>? answers = null;
        try
        {
            if (!string.IsNullOrEmpty(input.Accounts))
            {
                var accounts = JsonSerializer.Deserialize<List<ParticipantSaveAccount>>(input.Accounts, JsonOptions) ?? [];
                playing = accounts.Where(item => item.Role != "alt").Select(item => new AdminDrawerAccount(item.AssignmentId, item.Name ?? string.Empty, item.Ehb, item.Primary)).ToList();
                informational = accounts.Where(item => item.Role == "alt").Select(item => new AdminDrawerAccount(item.AssignmentId, item.Name ?? string.Empty, null)).ToList();
            }
            if (!string.IsNullOrEmpty(input.Answers))
                answers = JsonSerializer.Deserialize<Dictionary<Guid, string>>(input.Answers, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return SaveOutcome(new AdminParticipantDrawerSaveResult(false, "The changes could not be read. Nothing was saved.", "refused"));
        }
        var result = await signupService.SaveAdminParticipantDrawerAsync(new AdminParticipantDrawerSaveRequest(
            id, input.ParticipantId, actorId.Value, User.Identity?.Name ?? "Admin",
            input.Paid ? PaymentStatus.Paid : PaymentStatus.Unpaid, input.Note,
            input.ExpectedPaid ? PaymentStatus.Paid : PaymentStatus.Unpaid, input.ExpectedNote,
            input.ExpectedResponseVersion, playing, informational, answers), ct);
        return SaveOutcome(result);
    }

    // Retired signup-settings owner. Keep handler selection and the existing
    // Setup gate so historical posts receive the pinned Manage redirect.
    public IActionResult OnPostSignupAdministration(Guid id)
        => RedirectToPage("Manage", new { id });

    // F04 / brief 87 1c: Add from a website account's saved Playing accounts (no questions,
    // no WOM). Full events go to the waiting list unless "Confirm and add a place" (exactly +1).
    // Replaces the retired CreateInternalParticipant/CancelWomValidation handlers (A10).
    public async Task<IActionResult> OnPostAddAsync(Guid id, [FromForm] ParticipantAddInput input, CancellationToken ct)
    {
        var actorId = User.GetAccountId();
        if (actorId is null) return Forbid();
        if (input.Owner is not { } owner) return Outcome(false, "Choose a website account.");
        if (input.Primary is not { } primary || input.Accounts.Count == 0) return Outcome(false, "Select at least one saved Playing account.");
        var result = await signupService.AddSavedParticipantAsync(new AddSavedParticipantRequest(id, owner, actorId.Value, User.Identity?.Name ?? "Admin",
            input.Accounts, primary, input.Paid ? PaymentStatus.Paid : PaymentStatus.Unpaid, input.AddPlace, input.EventVersion), ct);
        var name = result.ParticipantId is { } added ? (await NamesAsync(id, [added], ct)).FirstOrDefault() : null;
        return Outcome(result.Succeeded, result.Error, new
        {
            participantId = result.ParticipantId, name, status = result.Status is SignupStatus.WaitingList ? "waiting" : "confirmed",
            waitingPosition = result.WaitingPosition, capacity = result.EffectiveParticipantCap, addedPlace = result.AddedPlace
        });
    }

    private async Task<bool> LoadAsync(Guid id, CancellationToken ct)
    {
        Event = await VisibleEventAsync(id, ct);
        if (Event is null) return false;
        EventTimezone = Event.Timezone;
        Policy = ParticipantRosterPolicy.For(Event, Localize);
        List = await new ParticipantsListReader(db).ReadAsync(Event, Query, ct);
        PlayingSlots = await db.SignupQuestions.AsNoTracking().CountAsync(x => x.EventId == id && x.Active && x.Type == SignupQuestionType.Account && x.AccountAnswerRole == EventCharacterRole.Playing, ct);
        return true;
    }

    private Task<BingoEvent?> VisibleEventAsync(Guid id, CancellationToken ct) =>
        db.Events.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.HiddenAt == null && item.State != EventState.Discarded, ct);

    private async Task<List<string>> NamesAsync(Guid eventId, IEnumerable<Guid> participantIds, CancellationToken ct)
    {
        var ids = participantIds.ToList();
        var names = await db.AdminPrimaryCharacters().AsNoTracking().Where(item => item.EventId == eventId && ids.Contains(item.ParticipantId))
            .Select(item => new { item.ParticipantId, item.Name }).ToListAsync(ct);
        return ids.Select(value => names.FirstOrDefault(item => item.ParticipantId == value)?.Name).OfType<string>().ToList();
    }

    /// <summary>The URL of this directory with the current query, optionally changed (A2).</summary>
    public string DirectoryUrl(string? tab = null, string? pay = null, string? search = null, string? sort = null, string? direction = null,
        int? page = null, int? perPage = null)
    {
        var values = new List<string>();
        void Add(string key, string? value, string? fallback) { if (!string.IsNullOrEmpty(value) && value != fallback) values.Add($"{key}={Uri.EscapeDataString(value)}"); }
        Add("tab", tab ?? Query.Tab, "confirmed");
        Add("pay", pay ?? Query.Pay, "any");
        Add("q", search ?? Query.Search, string.Empty);
        var activeSort = sort ?? Query.Sort;
        Add("sort", activeSort, "default");
        if (activeSort != "default") Add("dir", direction ?? (Query.Descending ? "desc" : "asc"), null);
        Add("per", (perPage ?? Query.PerPage).ToString(CultureInfo.InvariantCulture), "25");
        Add("page", (page ?? List.Page).ToString(CultureInfo.InvariantCulture), "1");
        return $"/Admin/Events/Participants/{Event?.Id}" + (values.Count == 0 ? string.Empty : "?" + string.Join("&", values));
    }

    private bool WantsJson => Request.GetTypedHeaders().Accept?.Any(value => value.MediaType.Value == "application/json") == true;

    private JsonResult Outcome(bool succeeded, string? error, object? data = null) => new(new
    {
        outcome = succeeded ? "done" : IsStale(error) ? "stale" : "refused",
        message = error is null ? null : Localize(error),
        data
    });

    private static bool IsStale(string? error) => error is not null &&
        (error.Contains("changed while you were editing", StringComparison.OrdinalIgnoreCase) || error.Contains("changed this participant", StringComparison.OrdinalIgnoreCase) ||
         error.Contains("version is required", StringComparison.OrdinalIgnoreCase));

    private JsonResult SaveOutcome(AdminParticipantDrawerSaveResult result) => new(new
    {
        outcome = result.Outcome,
        message = result.Error is null ? null : Localize(result.Error),
        field = result.Field,
        changed = result.Changed
    });

    private void NoStore() { if (HttpContext is { } context) context.Response.Headers.CacheControl = "no-store"; }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string Localize(string key, params object[] arguments) => text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);
    private void SetStatus(string message, UiMessageType type)
    {
        TempData["StatusMessage"] = message;
        TempData[UiMessage.TypeKey] = type.ToString();
    }

    public sealed class ParticipantActionInput
    {
        public Guid ParticipantId { get; set; }
        public long? EventVersion { get; set; }
        public int? ResponseVersion { get; set; }
        public bool AddPlace { get; set; }
    }
    public sealed class ParticipantSaveInput
    {
        public Guid ParticipantId { get; set; }
        public int? ExpectedResponseVersion { get; set; }
        public bool ExpectedPaid { get; set; }
        public string? ExpectedNote { get; set; }
        public bool Paid { get; set; }
        [StringLength(4000)] public string? Note { get; set; }
        public string? Accounts { get; set; }
        public string? Answers { get; set; }
    }
    public sealed record ParticipantSaveAccount(Guid? AssignmentId, string? Name, decimal? Ehb, string? Role, bool Primary);
    public sealed class ParticipantAddInput
    {
        public Guid? Owner { get; set; }
        public List<Guid> Accounts { get; set; } = [];
        public Guid? Primary { get; set; }
        public bool Paid { get; set; }
        public bool AddPlace { get; set; }
        public long? EventVersion { get; set; }
    }
}
