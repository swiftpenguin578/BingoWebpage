using System.ComponentModel.DataAnnotations;
using Bingo.Application.Access;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Admin.Accounts;

[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
public sealed class TransferModel(AccountAdministrationService administration, ApplicationDbContext db, IStringLocalizer<SharedResource>? text = null) : PageModel
{
    [BindProperty] public TransferInput Input { get; set; } = new();
    public IReadOnlyList<DestinationOption> Destinations { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) => await LoadDestinations(ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await LoadDestinations(ct);
            return Page();
        }

        try
        {
            await administration.TransferOwnershipAsync(User.GetAccountId()!.Value, Input.CurrentPassword, Input.DestinationId, Input.ExpectedAuthorizationVersion, ct);
            TempData["StatusMessage"] = Localize("Super Admin ownership was transferred. Your account is now an Admin account.");
            TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString();
            return RedirectToPage("/Index");
        }
        catch (Exception exception) when (exception is StaleAccountChangeException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, Localize("This record was changed by another administrator. Current values are shown; review them before trying again."));
            await LoadDestinations(ct);
            return Page();
        }
        catch (AccountActionException exception)
        {
            ModelState.AddModelError(exception.Message == "The current password is incorrect." ? "Input.CurrentPassword" : string.Empty, Localize(exception.Message));
            await LoadDestinations(ct);
            return Page();
        }
        catch (InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, Localize("This action is not available."));
            await LoadDestinations(ct);
            return Page();
        }
    }

    private string Localize(string key, params object[] arguments) => text?[key, arguments].Value ?? string.Format(System.Globalization.CultureInfo.CurrentCulture, key, arguments);
    private async Task LoadDestinations(CancellationToken ct)
    {
        var currentId = User.GetAccountId();
        Destinations = await db.Accounts.AsNoTracking()
            .Where(x => x.AccountType == AccountType.WebsiteAccount && x.Active && x.Id != currentId && x.GlobalRole != GlobalRole.SuperAdmin)
            .OrderBy(x => x.PublicUsername)
            .Select(x => new DestinationOption(x.Id, x.PublicUsername!, x.AuthorizationVersion))
            .ToListAsync(ct);
    }

    public sealed class TransferInput
    {
        [Required, DataType(DataType.Password), Display(Name = "Your current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, Display(Name = "New Super Admin")]
        public Guid DestinationId { get; set; }
        public long ExpectedAuthorizationVersion { get; set; }
    }

    public sealed record DestinationOption(Guid Id, string Username, long AuthorizationVersion);
}
