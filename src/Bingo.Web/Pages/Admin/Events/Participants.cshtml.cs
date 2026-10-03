using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Security;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class ParticipantsModel(
    ApplicationDbContext db,
    ISignupService signupService,
    IAuditWriter? auditWriter = null,
    ISecretHasher? hasher = null,
    IStringLocalizer<SharedResource>? text = null) : PageModel
{
    public EventView? Event { get; private set; }
    public string EventTimezone { get; private set; } = DateTimePresentation.DefaultTimezoneId;
    public IReadOnlyList<ParticipantRow> Participants { get; private set; } = [];
    public IReadOnlyList<TeamOption> ParticipantTeams { get; private set; } = [];
    public IReadOnlyList<ParticipantModel.QuestionView> ActiveSignupQuestions { get; private set; } = [];
    public int TotalParticipantCount { get; private set; }
    public int WithdrawnParticipantCount { get; private set; }
    public bool WomValidationConfirmationRequired { get; private set; }
    public bool WomValidationConfirmationCancelled { get; private set; }
    public string WomValidationConfirmationNames => string.Join(", ", InternalParticipant.AccountAnswers.Values
        .Where(answer => !string.IsNullOrWhiteSpace(answer.CharacterName))
        .Select(answer => answer.CharacterName!.Trim())
        .DistinctBy(name => name.ToUpperInvariant(), StringComparer.Ordinal));

    [BindProperty] public InternalParticipantInput InternalParticipant { get; set; } = new();
    [BindProperty] public SignupAdministrationInput SignupAdministration { get; set; } = new();
    [BindProperty] public SignupCodeInput SignupCode { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ParticipantSearch { get; set; }
    [BindProperty(SupportsGet = true)] public string? ParticipantStatus { get; set; }
    [BindProperty(SupportsGet = true)] public string? ParticipantPayment { get; set; }
    [BindProperty(SupportsGet = true)] public string? ParticipantDiscord { get; set; }
    [BindProperty(SupportsGet = true)] public bool? ParticipantCaptain { get; set; }
    [BindProperty(SupportsGet = true)] public string? ParticipantSource { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? ParticipantTeamId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    [BindProperty(SupportsGet = true)] public string? Direction { get; set; }
    public bool AddParticipant => WomValidationConfirmationRequired || WomValidationConfirmationCancelled ||
        Request.Query.TryGetValue("addParticipant", out var value) && (value == "1" || bool.TryParse(value, out var enabled) && enabled);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
        => await LoadAsync(id, ct) ? Page() : NotFound();

    public async Task<IActionResult> OnGetSearchOwnerAccountsAsync(string? search, CancellationToken ct)
    {
        search = search?.Trim();
        if (string.IsNullOrWhiteSpace(search) || search.Length > 100) return new JsonResult(Array.Empty<OwnerAccountOption>());

        var normalized = search.ToUpperInvariant();
        var accounts = await db.Accounts.AsNoTracking()
            .Where(item => item.Active && item.AccountType == AccountType.WebsiteAccount && item.NormalizedLoginName.Contains(normalized))
            .OrderBy(item => item.LoginName)
            .Take(10)
            .Select(item => new OwnerAccountOption(item.Id, item.LoginName))
            .ToListAsync(ct);
        return new JsonResult(accounts);
    }

    public async Task<IActionResult> OnPostWithdrawAsync(Guid id, Guid participantId, CancellationToken ct, [FromForm] bool confirmLifecycleAction = false)
    {
        if (!confirmLifecycleAction)
        {
            SetStatus(Localize("Confirm the withdrawal before continuing."), UiMessageType.Error);
            return FilteredRedirect(id);
        }
        var participant = await db.EventParticipants.SingleOrDefaultAsync(item => item.Id == participantId && item.EventId == id, ct);
        if (participant is null) return NotFound();

        var result = await signupService.WithdrawAsync(id, participantId, User.GetAccountId(), User.Identity?.Name ?? "Admin", true, cancellationToken: ct);
        SetStatus(result.Succeeded ? Localize("Participant withdrawn. The waiting list was promoted where a place became available.") : result.Error ?? Localize("The participant could not be withdrawn."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return FilteredRedirect(id);
    }

    public async Task<IActionResult> OnPostCancelWomValidationAsync(Guid id, CancellationToken ct)
    {
        InternalParticipant.WomValidationConfirmationToken = null;
        ModelState.Remove("InternalParticipant.WomValidationConfirmationToken");
        TryGetTempData()?.Remove("WomValidationConfirmationToken");
        if (!await LoadAsync(id, ct)) return NotFound();
        InternalParticipant.WomValidationConfirmationToken = null;
        ModelState.Remove("InternalParticipant.WomValidationConfirmationToken");
        WomValidationConfirmationRequired = false;
        WomValidationConfirmationCancelled = true;
        return Page();
    }

    public async Task<IActionResult> OnPostPaymentAsync(Guid id, Guid participantId, PaymentStatus payment, CancellationToken ct)
    {
        var result = await signupService.SetPaymentAsync(id, participantId, User.GetAccountId(), User.Identity?.Name ?? "Admin", payment, ct);
        SetStatus(result.Succeeded ? Localize("Payment saved.") : result.Error ?? Localize("Payment could not be saved."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return FilteredRedirect(id);
    }

    public async Task<IActionResult> OnPostSignupAdministrationAsync(Guid id, CancellationToken ct)
    {
        var actorId = User.GetAccountId();
        if (actorId is null) return Forbid();
        var result = await signupService.UpdateSignupAdministrationAsync(id, SignupAdministration.Version, SignupAdministration.ParticipantCap, true, actorId.Value, User.Identity?.Name ?? "Admin", cancellationToken: ct);
        if (WantsSignupSettingsJson) return new JsonResult(result);
        if (!result.Succeeded)
        {
            SetStatus(result.Error ?? Localize("Signup settings could not be saved."), UiMessageType.Error);
            return FilteredRedirect(id);
        }

        var message = result.PromotedParticipants > 0
            ? Localize("Signup settings saved at capacity {0}. {1} waiting-list participant(s) were promoted automatically.", result.EffectiveParticipantCap?.ToString(CultureInfo.CurrentCulture) ?? string.Empty, result.PromotedParticipants)
            : Localize("Signup capacity and waiting-list settings saved at capacity {0}.", result.EffectiveParticipantCap?.ToString(CultureInfo.CurrentCulture) ?? string.Empty);
        SetStatus(message, UiMessageType.Success);
        return FilteredRedirect(id);
    }

    private bool WantsSignupSettingsJson => Request.GetTypedHeaders().Accept?.Any(value => value.MediaType.Value == "application/json") == true;

    public async Task<IActionResult> OnPostSignupCodeAsync(Guid id, CancellationToken ct)
    {
        IActionResult response;
        try { response = await SaveSignupCodeAsync(id, ct); }
        catch (Exception ex) when (IsSignupSerializationConflict(ex))
        {
            db.ChangeTracker.Clear();
            SetStatus(Localize("This event changed while you were editing it. Review the latest values and try again."), UiMessageType.Error);
            response = WantsSignupSettingsJson
                ? new JsonResult(new SignupAdministrationResult(false, "This event changed while you were editing it. Review the latest values and try again.", SubmittedEventVersion: SignupCode.Version))
                : FilteredRedirect(id);
        }
        if (response is JsonResult { Value: SignupAdministrationResult { Succeeded: false, Settings: null } result })
        {
            var current = await db.Events.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.HiddenAt == null, ct);
            if (current is not null) response = new JsonResult(result with
            {
                Settings = new(current.Version, current.ParticipantCap, current.WaitingListEnabled,
                    current.RequireSignupCode, current.SignupCodeHash is not null)
            });
        }
        return response;
    }

    private static bool IsSignupSerializationConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.SerializationFailure }) return true;
        return false;
    }

    private async Task<IActionResult> SaveSignupCodeAsync(Guid id, CancellationToken ct)
    {
        var actorId = User.GetAccountId();
        if (actorId is null) return Forbid();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var bingoEvent = await db.Events
            .FromSqlInterpolated($"SELECT * FROM events WHERE id = {id} AND hidden_at IS NULL FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (bingoEvent is null) return NotFound();
        if (bingoEvent.Version != SignupCode.Version)
        {
            SetStatus(Localize("This event changed while you were editing it. Review the latest values and try again."), UiMessageType.Error);
            return CodeResult(false, "This event changed while you were editing it. Review the latest values and try again.");
        }
        if (!bingoEvent.AcceptsWaitingList)
        {
            SetStatus(Localize("Signup settings are locked because the draft has started or this event has moved on."), UiMessageType.Error);
            return CodeResult(false, "Signup settings are locked because the draft has started or this event has moved on.");
        }

        const string codeField = "SignupCode.NewSignupCode";
        if (ModelState.TryGetValue(codeField, out var codeState) && codeState.Errors.Count > 0)
        {
            var errors = codeState.Errors.Select(error => error.ErrorMessage).ToArray();
            if (WantsSignupSettingsJson) return CodeResult(false, string.Join(" ", errors));
            var requireCode = SignupCode.RequireSignupCode;
            if (!await LoadAsync(id, ct)) return NotFound();
            SignupCode.RequireSignupCode = requireCode;
            // Render only this form's validation, without echoing the submitted secret.
            ModelState.Clear();
            foreach (var error in errors) ModelState.AddModelError(codeField, error);
            return Page();
        }

        try
        {
            var form = await db.SignupForms.SingleAsync(item => item.EventId == id, ct);
            var currentRequiresCode = bingoEvent.RequireSignupCode;
            var currentCodeHash = bingoEvent.SignupCodeHash;
            if (SignupCode.RequireSignupCode && !currentRequiresCode && string.IsNullOrWhiteSpace(SignupCode.NewSignupCode))
            {
                SetStatus(Localize("Enter a new signup code or turn code protection off."), UiMessageType.Error);
                return CodeResult(false, "Enter a new signup code or turn code protection off.");
            }

            var hash = !SignupCode.RequireSignupCode
                ? null
                : string.IsNullOrWhiteSpace(SignupCode.NewSignupCode)
                    ? currentCodeHash
                    : (hasher ?? throw new InvalidOperationException("Signup-code hashing is unavailable.")).Hash(SignupCode.NewSignupCode);
            if (SignupCode.RequireSignupCode && string.IsNullOrWhiteSpace(hash))
            {
                SetStatus(Localize("Enter a new signup code or turn code protection off."), UiMessageType.Error);
                return CodeResult(false, "Enter a new signup code or turn code protection off.");
            }

            var before = new { RequireSignupCode = currentRequiresCode, HasSignupCode = currentCodeHash is not null };
            bingoEvent.ConfigureSignup(true, SignupCode.RequireSignupCode, hash);
            // Code settings share the event admission version.  Invalidate every
            // form rendered before this rotation, even when the compatibility
            // form row is the only other versioned record being advanced.
            bingoEvent.AdvanceVersion();
            // SignupForm retains the historical columns for old rows and old
            // readers, but the event is the single active admission authority.
            form.ConfigureSignupCode(SignupCode.RequireSignupCode, hash);
            form.AdvanceVersion();
            var changeKind = !SignupCode.RequireSignupCode
                ? "disabled"
                : !currentRequiresCode
                    ? "enabled"
                    : !string.IsNullOrWhiteSpace(SignupCode.NewSignupCode) ? "changed" : "retained";
            await (auditWriter ?? throw new InvalidOperationException("Signup-code auditing is unavailable.")).WriteAndSaveAsync(
                actorId,
                User.Identity?.Name ?? "Admin",
                "event.signup_code_changed",
                "event",
                id.ToString(),
                JsonSerializer.Serialize(new
                {
                    before,
                    after = new { RequireSignupCode = bingoEvent.RequireSignupCode, HasSignupCode = bingoEvent.SignupCodeHash is not null },
                    changeKind,
                    codeReplaced = SignupCode.RequireSignupCode && !string.IsNullOrWhiteSpace(SignupCode.NewSignupCode)
                }),
                id,
                ct);
            await transaction.CommitAsync(ct);
            SetStatus(Localize("Signup-code protection saved."), UiMessageType.Success);
            return CodeResult(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            SetStatus(Localize("This event changed while you were editing it. Review the latest values and try again."), UiMessageType.Error);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            SetStatus(Localize("The signup-code setting could not be changed in this event state."), UiMessageType.Error);
        }
        return WantsSignupSettingsJson
            ? new JsonResult(new SignupAdministrationResult(false, "The signup-code setting could not be saved.", SubmittedEventVersion: SignupCode.Version))
            : FilteredRedirect(id);

        IActionResult CodeResult(bool succeeded, string? error = null) => WantsSignupSettingsJson
            ? new JsonResult(new SignupAdministrationResult(succeeded, error,
                SubmittedEventVersion: SignupCode.Version,
                Settings: new(bingoEvent.Version, bingoEvent.ParticipantCap, bingoEvent.WaitingListEnabled,
                    bingoEvent.RequireSignupCode, bingoEvent.SignupCodeHash is not null)))
            : FilteredRedirect(id);
    }

    public async Task<IActionResult> OnPostCreateInternalParticipantAsync(Guid id, CancellationToken ct)
    {
        var actorId = User.GetAccountId();
        if (actorId is null) return Forbid();
        if (InternalParticipant.OwnerAccountId is null)
        {
            SetStatus(Localize("Select an active website account for this participant."), UiMessageType.Error);
            return FilteredRedirect(id);
        }

        var result = await signupService.CreateAdminParticipantAsync(new AdminParticipantChangeRequest(id, null, actorId.Value, User.Identity?.Name ?? "Admin", InternalParticipant.OwnerAccountId,
            InternalParticipant.AccountAnswers.ToDictionary(item => item.Key, item => new AdminAccountAnswer(item.Value.CharacterName, item.Value.Ehb)), InternalParticipant.Answers, null, InternalParticipant.WomValidationConfirmationToken), ct);
        if (result.WomValidationConfirmationToken is { } confirmationToken)
        {
            if (!await LoadAsync(id, ct)) return NotFound();
            InternalParticipant.WomValidationConfirmationToken = confirmationToken;
            ModelState.Remove("InternalParticipant.WomValidationConfirmationToken");
            WomValidationConfirmationRequired = true;
            return Page();
        }
        SetStatus(result.Succeeded
            ? result.Status == SignupStatus.WaitingList ? Localize("Internal participant created at waiting-list position {0}.", result.WaitingPosition?.ToString(CultureInfo.CurrentCulture) ?? string.Empty) : Localize("Internal participant created.")
            : Localize(result.Error ?? "Internal participant could not be created."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return FilteredRedirect(id);
    }

    private async Task<bool> LoadAsync(Guid id, CancellationToken ct)
    {
        var bingoEvent = await db.Events.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.HiddenAt == null && item.State != EventState.Discarded, ct);
        if (bingoEvent is null) return false;
        EventTimezone = bingoEvent.Timezone;

        var allParticipants = await db.EventParticipants.AsNoTracking()
            .Where(item => item.EventId == id &&
                !db.TeamMemberships.Any(membership => membership.EventParticipantId == item.Id && membership.LeftAt == null &&
                    db.Teams.Any(team => team.Id == membership.TeamId && team.EventId == id && team.Active && !team.IncludedInDraft)))
            .OrderBy(item => item.SignedUpAt).ThenBy(item => item.SignupSequence).ToListAsync(ct);
        var capacityConfirmedCount = await db.EventParticipants.AsNoTracking()
            .CountAsync(item => item.EventId == id && item.SignupStatus == SignupStatus.Confirmed, ct);
        var capacityWaitingCount = await db.EventParticipants.AsNoTracking()
            .CountAsync(item => item.EventId == id && item.SignupStatus == SignupStatus.WaitingList, ct);
        if (TryGetTempData()?.Peek("WomValidationConfirmationToken") is string pendingValidation)
            InternalParticipant.WomValidationConfirmationToken = pendingValidation;
        ActiveSignupQuestions = await db.SignupQuestions.AsNoTracking().Where(item => item.EventId == id && item.Active).OrderBy(item => item.Position)
            .Select(item => new ParticipantModel.QuestionView(item.Id, item.Label, item.Type, item.Required, true, item.AccountAnswerRole, item.SystemField, item.Options == null ? Array.Empty<string>() : item.Options.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), null)).ToListAsync(ct);
        TotalParticipantCount = allParticipants.Count;
        WithdrawnParticipantCount = allParticipants.Count(item => item.SignupStatus == SignupStatus.Withdrawn);

        var participants = allParticipants.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(ParticipantSearch))
        {
            var search = ParticipantSearch.Trim().ToUpperInvariant();
            var matchingIds = await db.AdminPrimaryCharacters().AsNoTracking().Where(item => item.EventId == id && item.NormalizedName.Contains(search)).Select(item => item.ParticipantId).ToListAsync(ct);
            participants = participants.Where(item => matchingIds.Contains(item.Id));
        }
        if (Enum.TryParse<SignupStatus>(ParticipantStatus, true, out var status)) participants = participants.Where(item => item.SignupStatus == status);
        if (ParticipantPayment == "paid") participants = participants.Where(item => item.PaymentReceived);
        if (ParticipantPayment == "unpaid") participants = participants.Where(item => !item.PaymentReceived);
        if (ParticipantCaptain is not null) participants = participants.Where(item => item.CaptainVolunteer == ParticipantCaptain);
        if (Enum.TryParse<SignupSource>(ParticipantSource, true, out var source)) participants = participants.Where(item => item.Source == source);

        var participantIds = allParticipants.Select(item => item.Id).ToList();
        var memberships = await db.TeamMemberships.AsNoTracking().Where(item => item.LeftAt == null && participantIds.Contains(item.EventParticipantId)).ToListAsync(ct);
        if (ParticipantTeamId is { } selectedTeam) participants = participants.Where(item => memberships.Any(membership => membership.EventParticipantId == item.Id && membership.TeamId == selectedTeam));
        var ownerIds = allParticipants.Where(item => item.AccountId != null).Select(item => item.AccountId!.Value).Distinct().ToList();
        var owners = await db.Accounts.AsNoTracking().Where(item => ownerIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, ct);
        if (ParticipantDiscord == "linked") participants = participants.Where(item => item.AccountId is { } owner && owners.TryGetValue(owner, out var account) && account.DiscordUserId != null);
        if (ParticipantDiscord == "unlinked") participants = participants.Where(item => item.AccountId is null || !owners.TryGetValue(item.AccountId.Value, out var account) || account.DiscordUserId == null);

        var authorities = await db.AdminPrimaryCharacters().AsNoTracking().Where(item => item.EventId == id).ToDictionaryAsync(item => item.ParticipantId, ct);
        var waiting = allParticipants
            .Where(item => item.SignupStatus == SignupStatus.WaitingList)
            .OrderBy(item => item.WaitingListedAt ?? item.SignedUpAt)
            .ThenBy(item => item.SignupSequence)
            .ThenBy(item => item.Id)
            .Select((item, index) => (item.Id, Position: index + 1))
            .ToDictionary(item => item.Id, item => item.Position);
        var coCaptainQuestion = ActiveSignupQuestions.FirstOrDefault(question => question.SystemField == SignupSystemField.CoCaptainName);
        var coCaptainAnswers = coCaptainQuestion is null
            ? new Dictionary<Guid, string>()
            : await db.SignupAnswers.AsNoTracking()
                .Where(answer => participantIds.Contains(answer.EventParticipantId) && answer.SignupQuestionId == coCaptainQuestion.Id && answer.OsrsCharacterId == null)
                .ToDictionaryAsync(answer => answer.EventParticipantId, answer => answer.Value, ct);
        var teams = await db.Teams.AsNoTracking().Where(item => item.EventId == id).OrderBy(item => item.Name).ToListAsync(ct);
        ParticipantTeams = teams.Select(item => new TeamOption(item.Id, item.Name)).ToList();
        var teamNames = teams.ToDictionary(item => item.Id, item => item.Name);
        var rows = participants.Select(item => new ParticipantRow(item.Id, item.SignupSequence,
            authorities.TryGetValue(item.Id, out var primary) ? primary.Name : "External roster member",
            authorities.TryGetValue(item.Id, out primary) ? primary.Ehb : 0m,
            item.SignupStatus, item.PaymentStatus, item.SignedUpAt, item.CaptainVolunteer,
            coCaptainAnswers.GetValueOrDefault(item.Id),
            waiting.TryGetValue(item.Id, out var position) ? position : null, item.Source,
            item.AccountId is { } owner && owners.TryGetValue(owner, out var account) && account.Active && account.AccountType == AccountType.WebsiteAccount ? account.LoginName : null,
            item.AccountId is { } linkedOwner && owners.TryGetValue(linkedOwner, out var discordAccount) && discordAccount.DiscordUserId is not null,
            memberships.Where(membership => membership.EventParticipantId == item.Id).Select(membership => teamNames.GetValueOrDefault(membership.TeamId)).FirstOrDefault())).ToList();

        var activeSort = string.IsNullOrWhiteSpace(Sort) ? "ownership" : Sort.ToLowerInvariant();
        var descending = !string.Equals(Direction, "asc", StringComparison.OrdinalIgnoreCase);
        Participants = (activeSort switch
        {
            "sequence" => descending ? rows.OrderByDescending(item => item.Sequence) : rows.OrderBy(item => item.Sequence),
            "participant" => descending ? rows.OrderByDescending(item => item.Name).ThenBy(item => item.Sequence) : rows.OrderBy(item => item.Name).ThenBy(item => item.Sequence),
            "ehb" => descending ? rows.OrderByDescending(item => item.Ehb).ThenBy(item => item.Sequence) : rows.OrderBy(item => item.Ehb).ThenBy(item => item.Sequence),
            "status" => descending ? rows.OrderByDescending(item => item.Status).ThenBy(item => item.Sequence) : rows.OrderBy(item => item.Status).ThenBy(item => item.Sequence),
            "payment" => descending ? rows.OrderByDescending(item => item.Payment).ThenBy(item => item.Sequence) : rows.OrderBy(item => item.Payment).ThenBy(item => item.Sequence),
            "signedup" => descending ? rows.OrderByDescending(item => item.SignedUpAt).ThenBy(item => item.Sequence) : rows.OrderBy(item => item.SignedUpAt).ThenBy(item => item.Sequence),
            "captain" => descending ? rows.OrderByDescending(item => item.CaptainVolunteer).ThenBy(item => item.Sequence) : rows.OrderBy(item => item.CaptainVolunteer).ThenBy(item => item.Sequence),
            "ownership" => descending ? rows.OrderByDescending(item => item.TeamName ?? string.Empty).ThenBy(item => item.Sequence) : rows.OrderBy(item => item.TeamName ?? string.Empty).ThenBy(item => item.Sequence),
            _ => rows.OrderBy(item => item.Sequence)
        }).ToList();

        Event = new EventView(bingoEvent.Id, bingoEvent.Name, bingoEvent.State, bingoEvent.DraftLocked, bingoEvent.ParticipantCap ?? 0,
            capacityConfirmedCount, capacityWaitingCount, true, bingoEvent.Version,
            bingoEvent.RequireSignupCode, bingoEvent.SignupCodeHash is not null,
            !bingoEvent.DraftLocked && bingoEvent.State is (EventState.Draft or EventState.SignupOpen or EventState.SignupClosed));
        SignupAdministration = new SignupAdministrationInput { ParticipantCap = bingoEvent.ParticipantCap ?? 1, WaitingListEnabled = true, Version = bingoEvent.Version };
        SignupCode = new SignupCodeInput { RequireSignupCode = bingoEvent.RequireSignupCode, Version = bingoEvent.Version };
        return true;
    }

    private ITempDataDictionary? TryGetTempData()
    {
        var httpContext = PageContext?.HttpContext;
        if (httpContext?.RequestServices is not { } services) return null;
        return services.GetService<ITempDataDictionaryFactory>()?.GetTempData(httpContext);
    }

    private RedirectToPageResult FilteredRedirect(Guid id)
        => RedirectToPage(null, null, new { id, ParticipantSearch, ParticipantStatus, ParticipantPayment, ParticipantDiscord, ParticipantCaptain, ParticipantSource, ParticipantTeamId, Sort, Direction }, "players");

    private string Localize(string key, params object[] arguments) => text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);
    private void SetStatus(string message, UiMessageType type)
    {
        TempData["StatusMessage"] = message;
        TempData[UiMessage.TypeKey] = type.ToString();
    }

    public sealed record EventView(Guid Id, string Name, EventState State, bool DraftLocked, int ParticipantCap, int Confirmed, int Waiting, bool WaitingListEnabled, long Version, bool RequireSignupCode, bool HasSignupCode, bool CanEditParticipant);
    public sealed record ParticipantRow(Guid Id, long Sequence, string Name, decimal Ehb, SignupStatus Status, PaymentStatus Payment, DateTimeOffset SignedUpAt, bool CaptainVolunteer, string? CoCaptainName, int? WaitingPosition, SignupSource Source, string? WebsiteUsername, bool DiscordLinked, string? TeamName);
    public sealed record TeamOption(Guid Id, string Name);
    public sealed record OwnerAccountOption(Guid Id, string Username);
    public sealed class InternalParticipantInput
    {
        public Guid? OwnerAccountId { get; set; }
        public Dictionary<Guid, ParticipantModel.AccountInput> AccountAnswers { get; set; } = [];
        public Dictionary<Guid, string> Answers { get; set; } = [];
        public string? WomValidationConfirmationToken { get; set; }
    }

    public sealed class SignupAdministrationInput
    {
        [Range(1, 10000)] public int ParticipantCap { get; set; }
        public bool WaitingListEnabled { get; set; }
        public long Version { get; set; }
        public bool ConfirmWaitingListDisablement { get; set; }
    }

    public sealed class SignupCodeInput
    {
        public bool RequireSignupCode { get; set; }
        [StringLength(100), Display(Name = "New signup code")] public string? NewSignupCode { get; set; }
        public long Version { get; set; }
    }
}
