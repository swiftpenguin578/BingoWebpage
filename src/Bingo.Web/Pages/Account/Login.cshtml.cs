using System.ComponentModel.DataAnnotations;
using Bingo.Web;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Account;

public sealed partial class LoginModel(
    AccountAuthenticationService authenticationService,
    LoginThrottleService throttles,
    IStringLocalizer<SharedResource> text,
    ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool AccessChanged { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        if (AccessChanged)
        {
            TempData["StatusMessage"] = text["Your access changed. Please sign in again."].Value;
            TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Warning.ToString();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var normalized = AccountAuthenticationService.NormalizeUsername(Input.Username);
        var network = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (throttles.IsBlocked(normalized, network))
        {
            LogBlockedAttempt(logger);
            ModelState.AddModelError(string.Empty, text["The username or password is incorrect."]);
            return Page();
        }
        var account = await authenticationService.ValidateCredentialsAsync(
            Input.Username,
            Input.Password,
            cancellationToken);
        if (account is null)
        {
            throttles.RecordFailure(normalized, network);
            LogFailedAttempt(logger);
            ModelState.AddModelError(string.Empty, text["The username or password is incorrect."]);
            return Page();
        }
        throttles.Clear(normalized);

        var emergencyAccess = account.AccountType == Bingo.Domain.Access.AccountType.EmergencyCaptain
            ? await authenticationService.GetEmergencyAccessAsync(account.Id, cancellationToken)
            : null;
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            authenticationService.CreatePrincipal(account, emergencyAccess: emergencyAccess),
            ChangePasswordModel.CreatePasswordSessionProperties(Input.RememberMe, DateTimeOffset.UtcNow));
        if (account.MustChangePassword)
        {
            return RedirectToPage("/Account/ChangePassword", new { ReturnUrl = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : null });
        }

        TempData["StatusMessage"] = text["Signed in successfully."].Value;
        TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString();
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : account.AccountType == Bingo.Domain.Access.AccountType.EmergencyCaptain ? "/Captain" : "/");
    }

    public sealed class LoginInput
    {
        [Required(ErrorMessage = "A public username is required."), StringLength(100), Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "A password is required."), DataType(DataType.Password), Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    [LoggerMessage(LogLevel.Warning, "Password login blocked by independent identifier/network throttle.")]
    private static partial void LogBlockedAttempt(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Password login failed.")]
    private static partial void LogFailedAttempt(ILogger logger);
}
