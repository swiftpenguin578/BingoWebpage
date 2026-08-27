using System.ComponentModel.DataAnnotations;
using Bingo.Application.Access;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

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
            await administration.TransferOwnershipAsync(User.GetAccountId()!.Value, Input.CurrentPassword, Input.DestinationUsername, ct);
            TempData["StatusMessage"] = Localize("Super Admin ownership was transferred. Your account is now an Admin account.");
            TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString();
            return RedirectToPage("/Index");
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
            .Select(x => new DestinationOption(x.Id, x.PublicUsername!))
            .ToListAsync(ct);
    }

    public sealed class TransferInput
    {
        [Required, DataType(DataType.Password), Display(Name = "Your current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, StringLength(100), Display(Name = "New Super Admin")]
        public string DestinationUsername { get; set; } = string.Empty;
    }

    public sealed record DestinationOption(Guid Id, string Username);
}
