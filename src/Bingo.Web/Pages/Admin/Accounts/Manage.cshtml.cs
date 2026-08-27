using System.ComponentModel.DataAnnotations;
using Bingo.Application.Access;
using Bingo.Domain.Access;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Admin.Accounts;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class ManageModel(ApplicationDbContext db, AccountAdministrationService administration, AccountIdentityService identities, IStringLocalizer<SharedResource>? text = null) : PageModel
{
    public AccountDetails? AccountView { get; private set; }
    [BindProperty, StringLength(500)] public string Reason { get; set; } = string.Empty;
    [BindProperty] public bool Overlay { get; set; }
    public bool IsOverlay => Overlay || string.Equals(Request.Query["overlay"], "1", StringComparison.Ordinal);
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        Overlay = string.Equals(Request.Query["overlay"], "1", StringComparison.Ordinal);
        return await Load(id, ct) ? Page() : NotFound();
    }
    public Task<IActionResult> OnPostGrantAdminAsync(Guid id, [FromForm] bool overlay, CancellationToken ct) { Overlay = ResolveSubmittedOverlay(overlay); return Mutate(id, x => administration.GrantAdminAsync(User.GetAccountId()!.Value, id, x), Localize("Admin access granted."), ct); }
    public Task<IActionResult> OnPostRevokeAdminAsync(Guid id, [FromForm] bool overlay, CancellationToken ct) { Overlay = ResolveSubmittedOverlay(overlay); return Mutate(id, x => administration.RevokeAdminAsync(User.GetAccountId()!.Value, id, x), Localize("Admin access revoked."), ct); }
    public Task<IActionResult> OnPostDisableAsync(Guid id, [FromForm] bool overlay, CancellationToken ct) { Overlay = ResolveSubmittedOverlay(overlay); return Mutate(id, x => administration.DisableAsync(User.GetAccountId()!.Value, id, Reason, x), Localize("Account disabled."), ct); }
    public Task<IActionResult> OnPostRestoreAsync(Guid id, [FromForm] bool overlay, CancellationToken ct) { Overlay = ResolveSubmittedOverlay(overlay); return Mutate(id, x => administration.RestoreAsync(User.GetAccountId()!.Value, id, x), Localize("Account restored."), ct); }
    public Task<IActionResult> OnPostEnableEmergencyAsync(Guid id, [FromForm] bool overlay, CancellationToken ct) { Overlay = ResolveSubmittedOverlay(overlay); return Mutate(id, x => administration.SetEmergencyEnabledAsync(User.GetAccountId()!.Value, id, true, x), Localize("Emergency credential enabled."), ct); }
    public Task<IActionResult> OnPostDisableEmergencyAsync(Guid id, [FromForm] bool overlay, CancellationToken ct) { Overlay = ResolveSubmittedOverlay(overlay); return Mutate(id, x => administration.SetEmergencyEnabledAsync(User.GetAccountId()!.Value, id, false, x), Localize("Emergency credential disabled."), ct); }
    public async Task<IActionResult> OnPostGenerateResetLinkAsync(Guid id, [FromForm] bool overlay, CancellationToken ct) { Overlay = ResolveSubmittedOverlay(overlay); return await GenerateLink(id, false, ct); }
    public async Task<IActionResult> OnPostGenerateEmergencyLinkAsync(Guid id, [FromForm] bool overlay, CancellationToken ct) { Overlay = ResolveSubmittedOverlay(overlay); return await GenerateLink(id, true, ct); }

    private async Task<IActionResult> GenerateLink(Guid id, bool emergency, CancellationToken ct)
    {
        try
        {
            var token = emergency
                ? await identities.GenerateEmergencyCredentialLinkAsync(User.GetAccountId()!.Value, id, ct)
                : await identities.GenerateResetLinkAsync(User.GetAccountId()!.Value, id, ct);
            TempData["CredentialLink"] = Url.Page("/Account/ResetPassword", pageHandler: null, values: new { token }, protocol: Request.Scheme);
            TempData["CredentialLinkTargetId"] = id.ToString();
            TempData["CredentialLinkPurpose"] = emergency ? "emergency" : "reset";
            TempData["StatusMessage"] = emergency
                ? Localize("Generated a one-time setup or reset link. It expires after 60 minutes.")
                : Localize("Generated a one-time reset link. It expires after 60 minutes.");
            TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString();
            return RedirectToPage(new { id, overlay = IsOverlay ? "1" : null });
        }
        catch (InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, Localize("The account link could not be generated."));
            await Load(id, ct);
            return Page();
        }
    }

    private async Task<IActionResult> Mutate(Guid id, Func<CancellationToken, Task> action, string message, CancellationToken ct)
    {
        try
        {
            await action(ct);
            TempData["StatusMessage"] = message;
            TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString();
            if (IsOverlay)
            {
                return RedirectToPage(new { id, overlay = "1" });
            }
            return RedirectToPage("Index");
        }
        catch (InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, Localize("The account change could not be saved."));
            await Load(id, ct);
            return Page();
        }
    }

    private async Task<bool> Load(Guid id, CancellationToken ct)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (account is null) return false;
        var scope = await (from access in db.AccountEventAccesses.AsNoTracking()
                           join bingoEvent in db.Events.AsNoTracking() on access.EventId equals bingoEvent.Id
                           join team in db.Teams.AsNoTracking() on access.TeamId equals team.Id
                           where access.AccountId == id
                           select new EmergencyScope(bingoEvent.Name, team.Name, access.Enabled, access.CutoffDisabled)).SingleOrDefaultAsync(ct);
        var characters = await (from link in db.AccountOsrsCharacters.AsNoTracking()
                                join character in db.OsrsCharacters.AsNoTracking() on link.OsrsCharacterId equals character.Id
                                where link.AccountId == id
                                orderby link.Position
                                select new CharacterView(character.DisplayName, link.Active, link.Preferred)).ToListAsync(ct);
        var participation = await (from participant in db.EventParticipants.AsNoTracking()
                                   join bingoEvent in db.Events.AsNoTracking() on participant.EventId equals bingoEvent.Id
                                   where participant.AccountId == id
                                   select new { participant.Id, EventName = bingoEvent.Name, participant.SignupStatus }).ToListAsync(ct);
        var participantIds = participation.Select(x => x.Id).ToArray();
        var memberships = await (from membership in db.TeamMemberships.AsNoTracking()
                                 join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
                                 where participantIds.Contains(membership.EventParticipantId)
                                 select new { membership.EventParticipantId, TeamName = team.Name, membership.Role, membership.JoinedAt, membership.LeftAt }).ToListAsync(ct);
        var roles = participation.Select(p => new EventRoleView(p.EventName, p.SignupStatus.ToString(), memberships.Where(m => m.EventParticipantId == p.Id).Select(m => new TeamRoleView(m.TeamName, m.Role, m.JoinedAt, m.LeftAt)).ToList())).ToList();
        var disableHistory = await db.AuditEntries.AsNoTracking().Where(x => x.TargetType == "account" && x.TargetId == id.ToString() && (x.Action == "account.disabled" || x.Action == "account.restored")).OrderByDescending(x => x.OccurredAt).Select(x => new DisableHistoryView(x.Action == "account.disabled" ? "Disabled" : "Restored", x.OccurredAt, x.ActorUsername)).ToListAsync(ct);
        AccountView = new AccountDetails(account.Id, account.LoginName, account.AccountType, account.GlobalRole, account.Active, account.DiscordUserId is not null, account.DiscordDisplayName, account.LastLoginAt, account.DisabledAt, account.PasswordHash is not null, scope, characters, roles, disableHistory);
        return true;
    }

    private bool ResolveSubmittedOverlay(bool overlay)
    {
        if (!Request.HasFormContentType) return overlay || IsOverlay;
        var submitted = Request.Form["overlay"].ToString();
        ModelState.Remove("overlay");
        return overlay || string.Equals(submitted, "1", StringComparison.Ordinal) || string.Equals(Request.Query["overlay"], "1", StringComparison.Ordinal);
    }

    private string Localize(string key, params object[] arguments) => text?[key, arguments].Value ?? string.Format(System.Globalization.CultureInfo.CurrentCulture, key, arguments);

    public sealed record AccountDetails(Guid Id, string Username, AccountType AccountType, GlobalRole? Role, bool Active, bool DiscordLinked, string? DiscordDisplayName, DateTimeOffset? LastLoginAt, DateTimeOffset? DisabledAt, bool HasPassword, EmergencyScope? Scope, IReadOnlyList<CharacterView> Characters, IReadOnlyList<EventRoleView> EventRoles, IReadOnlyList<DisableHistoryView> DisableHistory);
    public sealed record EmergencyScope(string EventName, string TeamName, bool Enabled, bool CutoffDisabled);
    public sealed record CharacterView(string DisplayName, bool Active, bool Preferred) { public string NormalizedName => AccountAuthenticationService.NormalizeUsername(DisplayName); }
    public sealed record EventRoleView(string EventName, string ParticipationState, IReadOnlyList<TeamRoleView> TeamRoles);
    public sealed record TeamRoleView(string TeamName, TeamMembershipRole Role, DateTimeOffset JoinedAt, DateTimeOffset? LeftAt);
    public sealed record DisableHistoryView(string State, DateTimeOffset OccurredAt, string ActorName);
}
