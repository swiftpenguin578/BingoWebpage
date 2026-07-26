using System.ComponentModel.DataAnnotations;
using Bingo.Web;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
namespace Bingo.Web.Pages.Account;

public sealed class ResetPasswordModel(AccountIdentityService identities, IStringLocalizer<SharedResource> text) : PageModel { [BindProperty] public InputModel Input { get; set; } = new(); public async Task<IActionResult> OnPostAsync(string token, CancellationToken ct) { if (!ModelState.IsValid) return Page(); try { var purpose = await identities.ConsumeResetAsync(token, Input.Password, ct); TempData["StatusMessage"] = purpose == Bingo.Domain.Access.PasswordCredentialTokenPurpose.EmergencySetup ? text["Emergency credential setup is complete. An Admin must enable it before it can be used."].Value : text["Your password was reset. Sign in with the new password."].Value; TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString(); return RedirectToPage("Login"); } catch (InvalidOperationException) { ModelState.AddModelError(string.Empty, text["This link is no longer valid."]); return Page(); } } public sealed class InputModel { [Required(ErrorMessage = "A password is required."), StringLength(200, MinimumLength = 10, ErrorMessage = "Passwords must be between 10 and 200 characters."), DataType(DataType.Password), Display(Name = "Password")] public string Password { get; set; } = string.Empty; [Required(ErrorMessage = "Confirm your password."), Compare(nameof(Password), ErrorMessage = "The passwords do not match."), DataType(DataType.Password), Display(Name = "Confirm password")] public string ConfirmPassword { get; set; } = string.Empty; } }
