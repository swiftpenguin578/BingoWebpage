using System.ComponentModel.DataAnnotations;
using Bingo.Application.Auditing;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Bingo.Web.Pages.Account;

[EnableRateLimiting("login")]
public sealed class LoginModel(
    AccountAuthenticationService authenticationService,
    IAuditWriter auditWriter) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var account = await authenticationService.ValidateCredentialsAsync(
            Input.Username,
            Input.Password,
            cancellationToken);
        if (account is null)
        {
            await auditWriter.WriteAsync(
                null,
                "anonymous",
                "login.failed",
                "account",
                details: $"Username: {Input.Username.Trim()}",
                cancellationToken: cancellationToken);
            ModelState.AddModelError(string.Empty, "The username or password is incorrect, or the account is not active.");
            return Page();
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            authenticationService.CreatePrincipal(account),
            new AuthenticationProperties { IsPersistent = Input.RememberMe });
        await auditWriter.WriteAsync(
            account.Id,
            account.Username,
            "login.succeeded",
            "account",
            account.Id.ToString(),
            cancellationToken: cancellationToken);

        if (account.MustChangePassword)
        {
            return RedirectToPage("/Account/ChangePassword");
        }

        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/");
    }

    public sealed class LoginInput
    {
        [Required, StringLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
