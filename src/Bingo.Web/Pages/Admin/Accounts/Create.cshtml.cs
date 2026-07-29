using System.ComponentModel.DataAnnotations;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Accounts;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class CreateModel(
    ApplicationDbContext dbContext,
    EmergencyCredentialService credentials) : PageModel
{
    [BindProperty]
    public CreateInput Input { get; set; } = new();
    public IReadOnlyList<SelectListItem> Events { get; private set; } = [];
    public IReadOnlyList<TeamOption> Teams { get; private set; } = [];

    public async Task OnGetAsync(Guid? eventId, Guid? teamId, CancellationToken cancellationToken)
    {
        Input.EventId = eventId; Input.TeamId = teamId;
        await LoadOptions(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        ValidateCaptainScope();
        if (Input.EventId is { } eventId && !await dbContext.Events.AnyAsync(bingoEvent => bingoEvent.Id == eventId, cancellationToken)) ModelState.AddModelError("Input.EventId", "Choose an available event.");
        if (Input.EventId is not null && Input.TeamId is not null && !await dbContext.Teams.AnyAsync(team => team.Id == Input.TeamId && team.EventId == Input.EventId && team.Active, cancellationToken)) ModelState.AddModelError("Input.TeamId", "Choose a team belonging to the selected event.");
        if (!ModelState.IsValid)
        {
            await LoadOptions(cancellationToken);
            return Page();
        }

        Bingo.Domain.Access.Account account;
        try { account = await credentials.CreateAsync(User.GetAccountId()!.Value, Input.Username, Input.EventId!.Value, Input.TeamId!.Value, cancellationToken); }
        catch (InvalidOperationException) { ModelState.AddModelError(string.Empty, "This action is not available for this account."); await LoadOptions(cancellationToken); return Page(); }
        TempData["StatusMessage"] = $"Created disabled emergency credential {account.LoginName}. Create a setup link before enabling it.";
        TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString();
        return RedirectToPage("Index");
    }

    private async Task LoadOptions(CancellationToken ct)
    {
        Events = await dbContext.Events.AsNoTracking().OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(ct);
        Teams = Input.EventId is { } eventId
            ? await dbContext.Teams.AsNoTracking().Where(team => team.EventId == eventId && team.Active).OrderBy(team => team.Name).Select(team => new TeamOption(team.Id, team.Name)).ToListAsync(ct)
            : [];
    }

    private void ValidateCaptainScope()
    {
        if (Input.EventId is null)
        {
            ModelState.AddModelError("Input.EventId", "An event is required for a captain.");
        }

        if (Input.TeamId is null)
        {
            ModelState.AddModelError("Input.TeamId", "A team is required for a captain.");
        }

    }

    public sealed class CreateInput
    {
        [Required, StringLength(100, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Display(Name = "Event")]
        public Guid? EventId { get; set; }

        [Display(Name = "Team")]
        public Guid? TeamId { get; set; }

        [Display(Name = "Require password change")]
        public bool MustChangePassword { get; set; } = true;
    }
    public sealed record TeamOption(Guid Id, string Name);
}
