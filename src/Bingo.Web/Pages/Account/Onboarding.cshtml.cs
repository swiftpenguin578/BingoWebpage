using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Bingo.Web.Pages.Account;

public sealed class OnboardingModel(
    AccountIdentityService identities,
    AccountAuthenticationService authentication,
    DiscordOnboardingStateService onboardingState,
    IStringLocalizer<SharedResource> text,
    ILogger<OnboardingModel> logger,
    IWiseOldManPlayerLookup wiseOldMan) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();

    public IActionResult OnGet() => onboardingState.TryRead(Request, out _) ? Page() : RedirectToPage("Login");

    public async Task<IActionResult> OnPostFetchAsync(CancellationToken ct)
    {
        if (!onboardingState.TryRead(Request, out _)) return RedirectToPage("Login");
        KeepValidationForCharacterName();
        if (!ModelState.IsValid) return Page();
        try
        {
            var result = await wiseOldMan.LookupPlayerAsync(Input.OsrsCharacterName, ct);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, LookupFailure(result));
                return Page();
            }

            Input.SavedEhb = result.Ehb!.Value;
            ModelState.Remove("Input.SavedEhb");
            return Page();
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, SafeUserFailure.Message(text, logger, exception));
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!onboardingState.TryRead(Request, out var state)) return RedirectToPage("Login");
        if (!ModelState.IsValid) return Page();
        try
        {
            var account = await identities.CompleteOnboardingAsync(state.DiscordUserId, state.DisplayName, Input.Username, Input.OsrsCharacterName, Input.Password, ct, Input.SavedEhb);
            onboardingState.Consume(Response, state);
            await HttpContext.SignInAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, authentication.CreatePrincipal(account, "discord"));
            TempData["StatusMessage"] = text["Your account is ready."].Value;
            TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString();
            return LocalRedirect(Url.IsLocalUrl(state.ReturnUrl) ? state.ReturnUrl : "/");
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, SafeUserFailure.Message(text, logger, exception));
            return Page();
        }
    }

    private void KeepValidationForCharacterName()
    {
        foreach (var key in ModelState.Keys.Where(key => key != "Input.OsrsCharacterName").ToList())
            ModelState.Remove(key);
    }

    private string LookupFailure(WiseOldManPlayerLookupResult result) => result.Status switch
    {
        WiseOldManLookupStatus.NotFound => text["Wise Old Man could not find that character."].Value,
        WiseOldManLookupStatus.RateLimited when result.RetryAt is { } retryAt => text["Wise Old Man is temporarily busy. Try again after {0}.", DateTimePresentation.Format(retryAt, "dd MMM yyyy, HH:mm", provider: CultureInfo.CurrentCulture)].Value,
        WiseOldManLookupStatus.RateLimited => text["Wise Old Man is temporarily busy. Try again in about 1 minute."].Value,
        _ when result.RetryAt is { } retryAt => text["Wise Old Man is unavailable right now. Your current EHB was kept. Try again after {0}.", DateTimePresentation.Format(retryAt, "dd MMM yyyy, HH:mm", provider: CultureInfo.CurrentCulture)].Value,
        _ => text["Wise Old Man is unavailable right now. Your current EHB was kept."].Value
    };

    public sealed class InputModel
    {
        [Required(ErrorMessage = "A public username is required."), StringLength(100, MinimumLength = 1), Display(Name = "Website username")]
        public string Username { get; set; } = string.Empty;
        [Required(ErrorMessage = "An OSRS character name is required."), StringLength(100, MinimumLength = 1), Display(Name = "First OSRS character")]
        public string OsrsCharacterName { get; set; } = string.Empty;
        [Range(typeof(decimal), "0", "9999999999.99", ParseLimitsInInvariantCulture = true, ErrorMessage = "Saved EHB cannot be negative."), Display(Name = "Saved EHB")]
        public decimal? SavedEhb { get; set; }
        [Required(ErrorMessage = "A password is required."), StringLength(200, MinimumLength = 10, ErrorMessage = "Passwords must be between 10 and 200 characters."), DataType(DataType.Password), Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;
        [Required(ErrorMessage = "Confirm your password."), Compare(nameof(Password), ErrorMessage = "The passwords do not match."), DataType(DataType.Password), Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
