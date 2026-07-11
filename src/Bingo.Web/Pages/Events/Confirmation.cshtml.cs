using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Events;

public sealed class ConfirmationModel : PageModel
{
    public string EventName { get; private set; } = string.Empty; public string Status { get; private set; } = string.Empty; public int? WaitingPosition { get; private set; }
    public string? EditToken { get; private set; }
    public IActionResult OnGet()
    {
        if (TempData["SignupEventName"] is not string name) return RedirectToPage("/Index");
        EventName = name; Status = TempData["SignupStatus"]?.ToString() ?? "Received"; WaitingPosition = TempData["WaitingPosition"] as int?; EditToken = TempData["EditToken"] as string; return Page();
    }
}
