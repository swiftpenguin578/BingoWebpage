using System.ComponentModel.DataAnnotations;
using Bingo.Application.Access;
using Bingo.Domain.Access;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Admin.Accounts;

[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
public sealed class TransferModel(AccountAdministrationService administration) : PageModel
{
    [BindProperty] public TransferInput Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();
        try
        {
            await administration.TransferOwnershipAsync(User.GetAccountId()!.Value, Input.CurrentPassword, Input.DestinationUsername, ct);
            TempData["StatusMessage"] = "Super Admin ownership was transferred. Your account is now an Admin account.";
            TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString();
            return RedirectToPage("/Index");
        }
        catch (InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, "This action is not available.");
            return Page();
        }
    }

    public sealed class TransferInput
    {
        [Required, DataType(DataType.Password), Display(Name = "Your current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, StringLength(100), Display(Name = "Destination username")]
        public string DestinationUsername { get; set; } = string.Empty;
    }
}
