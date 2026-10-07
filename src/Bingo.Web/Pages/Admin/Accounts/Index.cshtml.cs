using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Access;
using Bingo.Domain.Access;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Admin.Accounts;

// WA-5 Accounts: directory, account drawer (?account=) and Transfer dialog on one URL.
// Handlers answer XHR requests with a JSON outcome; plain form posts re-render the
// directory with the drawer open (refusal/stale) or redirect back to it (success).
[Authorize(Policy = AuthorizationPolicies.Admin)]
[AdminDesign]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IndexModel(
    ApplicationDbContext db,
    AccountAdministrationService administration,
    AccountIdentityService identities,
    IAuthorizationService authorization,
    TimeProvider time,
    IStringLocalizer<AdminCommunityResource>? community = null,
    IStringLocalizer<SharedResource>? shared = null) : PageModel
{
    public const int PageSize = 25;
    public const int SearchLimit = 100;
    private const int MaxPage = int.MaxValue / PageSize;
    public const string StaleMessage = "This record was changed by another administrator. Current values are shown; review them before trying again.";

    [BindProperty(SupportsGet = true, Name = "q")] public string? Query { get; set; }
    [BindProperty(SupportsGet = true, Name = "role")] public string? RoleQuery { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public string? PageQuery { get; set; }
    [BindProperty(SupportsGet = true, Name = "account")] public string? AccountQuery { get; set; }

    public string Search { get; private set; } = string.Empty;
    public GlobalRole? Role { get; private set; }
    public int PageNumber { get; private set; } = 1;
    public Guid? RequestedAccount { get; private set; }
    public IReadOnlyList<WebsiteAccountRow> WebsiteAccounts { get; private set; } = [];
    public bool WebsiteHasNextPage { get; private set; }
    public int WebsiteTotalCount { get; private set; }
    public int WebsiteDisabledCount { get; private set; }
    public int MatchingCount { get; private set; }
    public bool Filtered => Search.Length > 0 || Role is not null;
    public bool BeyondResults => WebsiteAccounts.Count == 0 && PageNumber > 1 && MatchingCount > 0;
    public int FirstRow => (PageNumber - 1) * PageSize + 1;
    public int LastRow => FirstRow + WebsiteAccounts.Count - 1;

    public AccountActor? Actor { get; private set; }
    public bool IsSuperAdmin => Actor?.Role == GlobalRole.SuperAdmin;
    public AccountDetails? AccountView { get; private set; }
    public bool DrawerRequested => AccountQuery is not null;
    public DrawerBanner? Banner { get; private set; }
    public bool AccountChangeStale { get; private set; }
    public ResetLinkView? ResetLink { get; private set; }
    public IReadOnlyList<DestinationOption> Destinations { get; private set; } = [];
    public string? TransferError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        return Page();
    }

    public Task<IActionResult> OnPostGrantAdminAsync([FromForm] long? expectedAuthorizationVersion, CancellationToken ct) =>
        MutateAsync("GrantAdmin", expectedAuthorizationVersion, (actor, target, version, token) => administration.GrantAdminAsync(actor, target, version, token), ct);
    public Task<IActionResult> OnPostRevokeAdminAsync([FromForm] long? expectedAuthorizationVersion, CancellationToken ct) =>
        MutateAsync("RevokeAdmin", expectedAuthorizationVersion, (actor, target, version, token) => administration.RevokeAdminAsync(actor, target, version, token), ct);
    public Task<IActionResult> OnPostDisableAsync([FromForm] long? expectedAuthorizationVersion, [FromForm] string? reason, CancellationToken ct) =>
        MutateAsync("Disable", expectedAuthorizationVersion, (actor, target, version, token) => administration.DisableAsync(actor, target, reason ?? string.Empty, version, token), ct);
    public Task<IActionResult> OnPostRestoreAsync([FromForm] long? expectedAuthorizationVersion, CancellationToken ct) =>
        MutateAsync("Restore", expectedAuthorizationVersion, (actor, target, version, token) => administration.RestoreAsync(actor, target, version, token), ct);

    public async Task<IActionResult> OnPostGenerateResetLinkAsync(CancellationToken ct)
    {
        NoStore();
        if (!TryTarget(out var target) || !await TargetExistsAsync(target, ct)) return NotFound();
        try
        {
            var token = await identities.GenerateResetLinkAsync(User.GetAccountId()!.Value, target, ct);
            // D5: the secret travels only in this no-store response (JSON for the drawer,
            // or the re-rendered page without script). Never TempData, URL, history or storage.
            var expiresAt = await db.PasswordCredentialTokens.AsNoTracking()
                .Where(item => item.AccountId == target && item.Purpose == PasswordCredentialTokenPurpose.Reset && item.UsedAt == null && item.SupersededAt == null)
                .OrderByDescending(item => item.CreatedAt).Select(item => item.ExpiresAt).FirstAsync(ct);
            var link = Url.Page("/Account/ResetPassword", pageHandler: null, values: new { token }, protocol: Request.Scheme)!;
            var expires = DateTimePresentation.Format(expiresAt, "HH:mm", provider: CultureInfo.CurrentCulture);
            if (JsonRequest) return new JsonResult(new { outcome = "completed", accountId = target, link, expires });
            ResetLink = new ResetLinkView(target, link, expires);
            await LoadAsync(ct);
            return Page();
        }
        catch (Exception exception) when (exception is AccountActionException or InvalidOperationException)
        {
            var message = exception is AccountActionException ? Localize(exception.Message) : Localize("The account link could not be generated.");
            if (JsonRequest) return new JsonResult(new { outcome = "refused", message });
            Banner = new DrawerBanner("is-warning", L("No link was created."), message);
            await LoadAsync(ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostTransferAsync([Bind(Prefix = "Input")] TransferInput input, CancellationToken ct)
    {
        // The page is Admin-scoped; ownership transfer stays SuperAdmin-only at the handler.
        if (!(await authorization.AuthorizeAsync(User, AuthorizationPolicies.SuperAdmin)).Succeeded) return Forbid();
        if (!ModelState.IsValid)
        {
            var errors = string.Join(" ", ModelState.Where(entry => entry.Value?.Errors.Count > 0).SelectMany(entry => entry.Value!.Errors.Select(error => error.ErrorMessage)));
            if (JsonRequest) return new JsonResult(new { outcome = "invalid", field = string.Empty, message = errors });
            return await TransferPageAsync(errors, ct);
        }
        try
        {
            var before = await db.Accounts.AsNoTracking().Where(item => item.Id == input.DestinationId).Select(item => item.GlobalRole).SingleOrDefaultAsync(ct);
            await administration.TransferOwnershipAsync(User.GetAccountId()!.Value, input.CurrentPassword, input.DestinationId, input.ExpectedAuthorizationVersion, input.DestinationUsernameConfirmation, ct);
            var destination = await db.Accounts.AsNoTracking().Where(item => item.Id == input.DestinationId).Select(item => item.PublicUsername).SingleAsync(ct);
            if (JsonRequest) return new JsonResult(new { outcome = "completed", destination, before = RoleLabel(before ?? GlobalRole.User) });
            TempData["StatusMessage"] = Localize("Super Admin ownership was transferred. Your account is now an Admin account.");
            TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
            return RedirectToPage("/Index");
        }
        catch (Exception exception) when (exception is StaleAccountChangeException or DbUpdateConcurrencyException)
        {
            if (JsonRequest) return new JsonResult(new { outcome = "recipient", disabled = false, destinations = await DestinationJsonAsync(ct) });
            return await TransferPageAsync(Localize(StaleMessage), ct);
        }
        catch (AccountActionException exception)
        {
            var field = exception.Message switch
            {
                "The current password is incorrect." => "password",
                "The destination username confirmation does not match the selected account." => "confirmation",
                "Choose another active website account." or "The selected account is disabled. Restore it before transferring ownership." or "The selected account is no longer available." => "recipient",
                _ => string.Empty
            };
            if (JsonRequest)
            {
                if (field == "recipient") return new JsonResult(new { outcome = "recipient", disabled = exception.Message.Contains("disabled", StringComparison.Ordinal), destinations = await DestinationJsonAsync(ct) });
                return new JsonResult(new { outcome = field.Length > 0 ? "invalid" : "refused", field, message = Localize(exception.Message) });
            }
            return await TransferPageAsync(Localize(exception.Message), ct);
        }
        catch (InvalidOperationException)
        {
            if (JsonRequest) return new JsonResult(new { outcome = "refused", field = string.Empty, message = Localize("This action is not available.") });
            return await TransferPageAsync(Localize("This action is not available."), ct);
        }
    }

    private async Task<IActionResult> TransferPageAsync(string error, CancellationToken ct)
    {
        TransferError = error;
        await LoadAsync(ct);
        return Page();
    }

    private async Task<IActionResult> MutateAsync(string kind, long? expectedVersion, Func<Guid, Guid, long, CancellationToken, Task> action, CancellationToken ct)
    {
        if (!TryTarget(out var target) || !await TargetExistsAsync(target, ct)) return NotFound();
        try
        {
            // A form without the version it was rendered from can never be fresh.
            if (expectedVersion is null) throw new StaleAccountChangeException();
            await action(User.GetAccountId()!.Value, target, expectedVersion.Value, ct);
            if (JsonRequest) return new JsonResult(new { outcome = "completed" });
            TempData["StatusMessage"] = Localize(kind switch
            {
                "GrantAdmin" => "Admin access granted.",
                "RevokeAdmin" => "Admin access revoked.",
                "Disable" => "Account disabled.",
                _ => "Account restored."
            });
            TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
            ReadQuery();
            return Redirect(DirectoryUrl(account: target));
        }
        catch (Exception exception) when (exception is StaleAccountChangeException or DbUpdateConcurrencyException)
        {
            if (JsonRequest) return new JsonResult(new { outcome = "stale", message = Localize(StaleMessage) });
            AccountChangeStale = true;
            Banner = new DrawerBanner("is-warning", L("This record was changed by another administrator."), L("Current values are shown; review them before trying again."), Localize(StaleMessage));
            await LoadAsync(ct);
            return Page();
        }
        catch (AccountActionException exception)
        {
            var invalid = exception.Message.StartsWith("A disable reason", StringComparison.Ordinal);
            if (JsonRequest) return new JsonResult(new { outcome = invalid ? "invalid" : "refused", message = Localize(exception.Message) });
            Banner = new DrawerBanner("is-warning", L("That couldn’t be done."), Localize(exception.Message));
            await LoadAsync(ct);
            return Page();
        }
        catch (InvalidOperationException)
        {
            if (JsonRequest) return new JsonResult(new { outcome = "refused", message = Localize("The account change could not be saved.") });
            Banner = new DrawerBanner("is-warning", L("That couldn’t be done."), Localize("The account change could not be saved."));
            await LoadAsync(ct);
            return Page();
        }
    }

    private bool JsonRequest => Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
        && Request.Headers.XRequestedWith == "XMLHttpRequest";

    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
    }

    private bool TryTarget(out Guid target) => Guid.TryParse(AccountQuery, out target) && target != Guid.Empty;

    private Task<bool> TargetExistsAsync(Guid target, CancellationToken ct) =>
        db.Accounts.AsNoTracking().AnyAsync(item => item.Id == target && item.AccountType == AccountType.WebsiteAccount, ct);

    private void ReadQuery()
    {
        var search = Query?.Trim() ?? string.Empty;
        Search = search.Length > SearchLimit ? search[..SearchLimit].TrimEnd() : search;
        Role = (RoleQuery ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "user" => GlobalRole.User,
            "admin" => GlobalRole.Admin,
            "superadmin" => GlobalRole.SuperAdmin,
            _ => null
        };
        // Strict positive integers only (A7); anything else is the first page.
        PageNumber = PageQuery is { Length: > 0 and <= 9 } text && text.All(char.IsAsciiDigit)
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var page) && page >= 1
            ? Math.Min(page, MaxPage) : 1;
        RequestedAccount = Guid.TryParse(AccountQuery, out var account) && account != Guid.Empty ? account : null;
    }

    public async Task LoadAsync(CancellationToken ct)
    {
        ReadQuery();
        var actorId = User.GetAccountId();
        Actor = actorId is null ? null : await db.Accounts.AsNoTracking().Where(item => item.Id == actorId.Value)
            .Select(item => new AccountActor(item.Id, item.GlobalRole ?? GlobalRole.User, item.PublicUsername ?? item.LoginName)).SingleOrDefaultAsync(ct);
        await LoadDirectoryAsync(ct);
        if (RequestedAccount is { } id) AccountView = await LoadAccountAsync(id, ct);
        if (IsSuperAdmin) Destinations = await DestinationsAsync(ct);
    }

    private async Task LoadDirectoryAsync(CancellationToken ct)
    {
        var websiteQuery = db.Accounts.AsNoTracking().Where(x => x.AccountType == AccountType.WebsiteAccount);
        WebsiteTotalCount = await websiteQuery.CountAsync(ct);
        WebsiteDisabledCount = await websiteQuery.CountAsync(x => !x.Active, ct);

        var query = websiteQuery;
        if (Search.Length > 0)
        {
            // Same normalization as stored usernames (A7): case never hides a match.
            var normalized = AccountAuthenticationService.NormalizeUsername(Search);
            query = query.Where(x => x.NormalizedPublicUsername != null && x.NormalizedPublicUsername.Contains(normalized));
        }
        if (Role is not null) query = query.Where(x => x.GlobalRole == Role);
        MatchingCount = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.PublicUsername).ThenBy(x => x.Id)
            .Skip((PageNumber - 1) * PageSize).Take(PageSize + 1)
            .Select(x => new WebsiteAccountRow(x.Id, x.PublicUsername!, x.GlobalRole!.Value, x.Active, x.DiscordUserId != null, x.DiscordDisplayName, x.LastLoginAt, ""))
            .ToListAsync(ct);
        WebsiteHasNextPage = rows.Count > PageSize;
        WebsiteAccounts = rows.Take(PageSize).ToList();

        var ids = WebsiteAccounts.Select(x => x.Id).ToArray();
        var participations = await (from participant in db.EventParticipants.AsNoTracking()
                                    join bingoEvent in db.Events.AsNoTracking() on participant.EventId equals bingoEvent.Id
                                    where participant.AccountId != null && ids.Contains(participant.AccountId.Value) && bingoEvent.HiddenAt == null
                                    orderby bingoEvent.EventStartsAt descending, bingoEvent.Name
                                    select new Participation(participant.Id, participant.AccountId ?? Guid.Empty, bingoEvent.Name)).ToListAsync(ct);
        var participantIds = participations.Select(x => x.Id).ToArray();
        var roles = await (from membership in db.TeamMemberships.AsNoTracking()
                           join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
                           where participantIds.Contains(membership.EventParticipantId) && membership.LeftAt == null
                           select new CurrentRole(membership.EventParticipantId, team.Name, membership.Role)).ToListAsync(ct);
        WebsiteAccounts = WebsiteAccounts.Select(x => x with { EventRoleSummary = BuildSummary(x.Id, participations, roles) }).ToList();
    }

    private string BuildSummary(Guid accountId, IEnumerable<Participation> participants, IEnumerable<CurrentRole> roles)
    {
        // Reference: up to three events; a current Captain/Co-captain role is named after the event.
        var items = participants.Where(x => x.AccountId == accountId).Select(x =>
        {
            var membership = roles.FirstOrDefault(role => role.EventParticipantId == x.Id && role.Role != TeamMembershipRole.Participant);
            return membership is null ? x.EventName : L("{0}: {1}", x.EventName, TeamRoleLabel(membership.Role));
        }).Distinct().Take(3).ToArray();
        return string.Join("; ", items);
    }

    private async Task<AccountDetails?> LoadAccountAsync(Guid id, CancellationToken ct)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (account is null || account.AccountType != AccountType.WebsiteAccount) return null;
        var characters = await (from link in db.AccountOsrsCharacters.AsNoTracking()
                                join character in db.OsrsCharacters.AsNoTracking() on link.OsrsCharacterId equals character.Id
                                where link.AccountId == id
                                orderby link.Position
                                select new CharacterView(character.DisplayName, link.Active, link.Preferred)).ToListAsync(ct);
        var participation = await (from participant in db.EventParticipants.AsNoTracking()
                                   join bingoEvent in db.Events.AsNoTracking() on participant.EventId equals bingoEvent.Id
                                   where participant.AccountId == id && bingoEvent.HiddenAt == null
                                   orderby bingoEvent.EventStartsAt descending, bingoEvent.Name
                                   select new { participant.Id, EventName = bingoEvent.Name, EventTimezone = bingoEvent.Timezone, participant.SignupStatus }).ToListAsync(ct);
        var participantIds = participation.Select(x => x.Id).ToArray();
        var memberships = await (from membership in db.TeamMemberships.AsNoTracking()
                                 join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
                                 where participantIds.Contains(membership.EventParticipantId)
                                 orderby membership.JoinedAt
                                 select new { membership.EventParticipantId, TeamName = team.Name, membership.Role, membership.JoinedAt, membership.LeftAt }).ToListAsync(ct);
        var roles = participation.Select(p => new EventRoleView(p.EventName, p.EventTimezone, p.SignupStatus,
            memberships.Where(m => m.EventParticipantId == p.Id).Select(m => new TeamRoleView(m.TeamName, m.Role, m.JoinedAt, m.LeftAt)).ToList())).ToList();
        var disableHistory = await db.AuditEntries.AsNoTracking()
            .Where(x => x.TargetType == "account" && x.TargetId == id.ToString() && (x.Action == "account.disabled" || x.Action == "account.restored"))
            .OrderByDescending(x => x.OccurredAt)
            .Select(x => new DisableHistoryView(x.Action == "account.disabled" ? "Disabled" : "Restored", x.OccurredAt, x.ActorUsername, x.Action == "account.disabled" ? x.Details : null))
            .ToListAsync(ct);
        var disabledBy = account.DisabledByAccountId is { } by
            ? await db.Accounts.AsNoTracking().Where(x => x.Id == by).Select(x => x.PublicUsername ?? x.LoginName).SingleOrDefaultAsync(ct)
            : null;
        return new AccountDetails(account.Id, account.AuthorizationVersion, account.PublicUsername ?? account.LoginName, account.AccountType, account.GlobalRole, account.Active,
            account.DiscordUserId is not null, account.DiscordDisplayName, account.LastLoginAt, account.DisabledAt, account.PasswordHash is not null,
            characters, roles, disableHistory)
        {
            IsSelf = Actor?.Id == account.Id,
            DisabledBy = disabledBy,
            DisabledByMe = account.DisabledByAccountId is not null && account.DisabledByAccountId == Actor?.Id,
            DisabledReason = account.DisabledReason,
            Capabilities = Capabilities(account)
        };
    }

    // Mirrors AccountAdministrationService/AccountResetTokenPolicy so absent authority is
    // explained instead of offered; the services remain the authority on every post.
    private AccountCapabilities Capabilities(Bingo.Domain.Access.Account target)
    {
        var owner = Actor?.Role == GlobalRole.SuperAdmin;
        if (Actor?.Id == target.Id) return new(owner ? "self-owner" : "self", null, false, false, null, owner);
        if (target.GlobalRole == GlobalRole.SuperAdmin) return new("protected", null, false, false, null, false);
        if (target.GlobalRole == GlobalRole.Admin && !owner) return new("admin-only", null, false, false, null, false);
        var role = owner ? target.GlobalRole == GlobalRole.Admin ? "revoke" : target.Active ? "grant" : "grant-blocked" : null;
        return new(owner ? null : "owner-roles", role, target.Active, !target.Active, target.Active ? "disable" : "restore", false);
    }

    private async Task<object[]> DestinationJsonAsync(CancellationToken ct) =>
        (await DestinationsAsync(ct)).Select(item => (object)new { id = item.Id, username = item.Username, roleLabel = RoleLabel(item.Role), authorizationVersion = item.AuthorizationVersion }).ToArray();

    private async Task<IReadOnlyList<DestinationOption>> DestinationsAsync(CancellationToken ct)
    {
        var currentId = User.GetAccountId();
        return await db.Accounts.AsNoTracking()
            .Where(x => x.AccountType == AccountType.WebsiteAccount && x.Active && x.Id != currentId && x.GlobalRole != GlobalRole.SuperAdmin)
            .OrderBy(x => x.PublicUsername)
            .Select(x => new DestinationOption(x.Id, x.PublicUsername!, x.GlobalRole ?? GlobalRole.User, x.AuthorizationVersion))
            .ToListAsync(ct);
    }

    public string DirectoryUrl(string? search = null, GlobalRole? role = null, bool clearRole = false, int? page = null, Guid? account = null)
    {
        var values = new List<string>();
        var q = search ?? Search;
        if (q.Length > 0) values.Add("q=" + Uri.EscapeDataString(q));
        var r = clearRole ? null : role ?? Role;
        if (r is not null) values.Add("role=" + RoleKey(r.Value));
        var p = page ?? PageNumber;
        if (p > 1) values.Add("page=" + p.ToString(CultureInfo.InvariantCulture));
        if (account is not null) values.Add("account=" + account.Value.ToString());
        return "/Admin/Accounts" + (values.Count > 0 ? "?" + string.Join("&", values) : string.Empty);
    }

    public static string RoleKey(GlobalRole role) => role switch { GlobalRole.SuperAdmin => "superadmin", GlobalRole.Admin => "admin", _ => "user" };
    public string RoleLabel(GlobalRole role) => role switch { GlobalRole.SuperAdmin => L("Super Admin"), GlobalRole.Admin => L("Admin"), _ => L("User") };
    public string TeamRoleLabel(TeamMembershipRole role) => role switch { TeamMembershipRole.Captain => L("Captain"), TeamMembershipRole.CoCaptain => L("Co-captain"), _ => L("Member") };
    public DateTimeOffset UtcNow => time.GetUtcNow();

    private string L(string key, params object[] arguments) => community?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);
    private string Localize(string key, params object[] arguments) => shared?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);

    public sealed record AccountActor(Guid Id, GlobalRole Role, string Username);
    public sealed record WebsiteAccountRow(Guid Id, string Username, GlobalRole Role, bool Active, bool DiscordLinked, string? DiscordDisplayName, DateTimeOffset? LastLoginAt, string EventRoleSummary);
    public sealed record AccountDetails(Guid Id, long AuthorizationVersion, string Username, AccountType AccountType, GlobalRole? Role, bool Active, bool DiscordLinked, string? DiscordDisplayName, DateTimeOffset? LastLoginAt, DateTimeOffset? DisabledAt, bool HasPassword, IReadOnlyList<CharacterView> Characters, IReadOnlyList<EventRoleView> EventRoles, IReadOnlyList<DisableHistoryView> DisableHistory)
    {
        public bool IsSelf { get; init; }
        public string? DisabledBy { get; init; }
        public bool DisabledByMe { get; init; }
        public string? DisabledReason { get; init; }
        public AccountCapabilities Capabilities { get; init; } = new(null, null, false, false, null, false);
    }
    /// <param name="Note">self, self-owner, protected, admin-only, owner-roles or null.</param>
    /// <param name="Role">grant, revoke, grant-blocked or null.</param>
    /// <param name="Status">disable, restore or null.</param>
    public sealed record AccountCapabilities(string? Note, string? Role, bool Reset, bool ResetBlocked, string? Status, bool Transfer);
    public sealed record CharacterView(string DisplayName, bool Active, bool Preferred);
    public sealed record EventRoleView(string EventName, string Timezone, SignupStatus ParticipationState, IReadOnlyList<TeamRoleView> TeamRoles);
    public sealed record TeamRoleView(string TeamName, TeamMembershipRole Role, DateTimeOffset JoinedAt, DateTimeOffset? LeftAt);
    public sealed record DisableHistoryView(string State, DateTimeOffset OccurredAt, string ActorName, string? Reason);
    public sealed record DrawerBanner(string Tone, string Title, string Text, string? Message = null);
    public sealed record ResetLinkView(Guid AccountId, string Link, string Expires);
    public sealed record DestinationOption(Guid Id, string Username, GlobalRole Role, long AuthorizationVersion);
    private sealed record Participation(Guid Id, Guid AccountId, string EventName);
    private sealed record CurrentRole(Guid EventParticipantId, string TeamName, TeamMembershipRole Role);

    public sealed class TransferInput
    {
        [Required, DataType(DataType.Password), Display(Name = "Your current password")]
        public string CurrentPassword { get; set; } = string.Empty;
        [Required, Display(Name = "New Super Admin")]
        public Guid DestinationId { get; set; }
        [Required, StringLength(100), Display(Name = "Destination username confirmation")]
        public string DestinationUsernameConfirmation { get; set; } = string.Empty;
        public long ExpectedAuthorizationVersion { get; set; }
    }
}
