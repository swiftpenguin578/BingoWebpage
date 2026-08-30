using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Account;

[Authorize]
public sealed class MyAccountsModel(
    MyAccountsService accounts,
    IWiseOldManPlayerLookup wiseOldMan,
    IStringLocalizer<SharedResource> text,
    ILogger<MyAccountsModel> logger) : PageModel
{
    public IReadOnlyList<MyAccountCharacter> Links { get; private set; } = [];
    public Guid? FetchFailureLinkId { get; private set; }
    public string? FetchFailureLabel { get; private set; }
    public string? FetchFailureEhb { get; private set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    [BindProperty] public AddInput Add { get; set; } = new();
    [BindProperty] public EditInput Edit { get; set; } = new();
    [BindProperty] public LinkInput Action { get; set; } = new();
    [BindProperty] public UnlinkInput Unlink { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        NormalizeReturnUrl();
        return await LoadAsync(ct) ? Page() : Forbid();
    }

    public async Task<IActionResult> OnPostAddAsync(CancellationToken ct)
    {
        NormalizeReturnUrl();
        KeepValidationFor(nameof(Add));
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            await accounts.AddOrReactivateAsync(AccountId, Add.CharacterName, Add.PersonalLabel, Add.SavedEhb, ct);
            return Success("Character added to My Accounts.");
        }
        catch (InvalidOperationException exception) { return await FailureAsync(exception, ct); }
    }

    public async Task<IActionResult> OnPostFetchAddAsync(CancellationToken ct)
    {
        NormalizeReturnUrl();
        KeepValidationFor(nameof(Add));
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            var result = await wiseOldMan.LookupPlayerAsync(Add.CharacterName, ct);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, LookupFailure(result));
                return await ReloadAsync(ct);
            }
            Add.SavedEhb = MyAccountsService.RoundEhb(result.Ehb!.Value);
            ModelState.Remove("Add.SavedEhb");
            return await ReloadAsync(ct);
        }
        catch (InvalidOperationException exception) { return await FailureAsync(exception, ct); }
    }

    public async Task<IActionResult> OnPostUpdateAsync(CancellationToken ct)
    {
        NormalizeReturnUrl();
        KeepValidationFor(nameof(Edit));
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            await accounts.UpdateAsync(AccountId, Edit.LinkId, Edit.CharacterName, Edit.PersonalLabel, Edit.SavedEhb, ct);
            return Success("My Accounts details saved.");
        }
        catch (MyAccountsCorrectionConflictException exception)
        {
            ModelState.AddModelError(string.Empty, text["The corrected character is already registered in {0}.", exception.EventName]);
            return await ReloadAsync(ct);
        }
        catch (InvalidOperationException exception) { return await FailureAsync(exception, ct); }
    }

    public async Task<IActionResult> OnPostFetchAsync(CancellationToken ct)
    {
        NormalizeReturnUrl();
        KeepValidationFor(nameof(Edit));
        if (!ModelState.IsValid) { PreserveFetchValues(); return await ReloadAsync(ct); }
        try
        {
            var result = await wiseOldMan.LookupPlayerAsync(Edit.CharacterName, ct);
            if (!result.Succeeded)
            {
                PreserveFetchValues();
                ModelState.AddModelError(string.Empty, LookupFailure(result));
                return await ReloadAsync(ct);
            }
            var fetchedEhb = MyAccountsService.RoundEhb(result.Ehb!.Value);
            Edit.SavedEhb = fetchedEhb;
            FetchFailureLinkId = Edit.LinkId;
            FetchFailureLabel = Edit.PersonalLabel;
            FetchFailureEhb = fetchedEhb.ToString("0.00", CultureInfo.InvariantCulture);
            ModelState.Remove("Edit.SavedEhb");
            return await ReloadAsync(ct);
        }
        catch (InvalidOperationException exception) { PreserveFetchValues(); return await FailureAsync(exception, ct); }
    }

    public async Task<IActionResult> OnPostMoveUpAsync(CancellationToken ct) => await MoveAsync(-1, ct);
    public async Task<IActionResult> OnPostMoveDownAsync(CancellationToken ct) => await MoveAsync(1, ct);

    public async Task<IActionResult> OnPostUnlinkAsync(CancellationToken ct)
    {
        NormalizeReturnUrl();
        try
        {
            await accounts.UnlinkAsync(AccountId, Unlink.LinkId, Unlink.ConfirmRegistrationWarning, ct);
            return Success("Character unlinked from My Accounts.");
        }
        catch (MyAccountsConfirmationRequiredException)
        {
            ModelState.AddModelError(string.Empty, text["Confirm that the event registration will remain before unlinking this character."]);
            return await ReloadAsync(ct);
        }
        catch (InvalidOperationException exception) { return await FailureAsync(exception, ct); }
    }

    private async Task<IActionResult> MoveAsync(int direction, CancellationToken ct)
    {
        NormalizeReturnUrl();
        try
        {
            await accounts.MoveAsync(AccountId, Action.LinkId, direction, ct);
            return Success("My Accounts order updated.");
        }
        catch (InvalidOperationException exception) { return await FailureAsync(exception, ct); }
    }

    private void KeepValidationFor(string prefix)
    {
        foreach (var key in ModelState.Keys.Where(key => !key.StartsWith($"{prefix}.", StringComparison.Ordinal)).ToList())
            ModelState.Remove(key);
    }

    private Guid AccountId => User.GetAccountId() ?? throw new InvalidOperationException("An authenticated account is required.");

    private IActionResult Success(string message)
    {
        TempData["StatusMessage"] = text[message].Value;
        TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
        return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl) : RedirectToPage(new { ReturnUrl });
    }

    private void NormalizeReturnUrl()
    {
        if (!Url.IsLocalUrl(ReturnUrl)) ReturnUrl = null;
    }

    private async Task<IActionResult> FailureAsync(InvalidOperationException exception, CancellationToken ct)
    {
        ModelState.AddModelError(string.Empty, SafeUserFailure.Message(text, logger, exception));
        return await ReloadAsync(ct);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        if (!await LoadAsync(ct)) return Forbid();
        return Page();
    }

    private void PreserveFetchValues()
    {
        FetchFailureLinkId = Edit.LinkId;
        FetchFailureLabel = Edit.PersonalLabel;
        FetchFailureEhb = ModelState["Edit.SavedEhb"]?.AttemptedValue ?? Edit.SavedEhb?.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private string LookupFailure(WiseOldManPlayerLookupResult result) => result.Status switch
    {
        WiseOldManLookupStatus.NotFound => text["Wise Old Man could not find that character."].Value,
        WiseOldManLookupStatus.RateLimited when result.RetryAt is { } retryAt => text["Wise Old Man is temporarily busy. Try again after {0}.", DateTimePresentation.Format(retryAt, "dd MMM yyyy, HH:mm", provider: CultureInfo.CurrentCulture)].Value,
        WiseOldManLookupStatus.RateLimited => text["Wise Old Man is temporarily busy. Try again in about 1 minute."].Value,
        _ when result.RetryAt is { } retryAt => text["Wise Old Man is unavailable right now. Your current EHB was kept. Try again after {0}.", DateTimePresentation.Format(retryAt, "dd MMM yyyy, HH:mm", provider: CultureInfo.CurrentCulture)].Value,
        _ => text["Wise Old Man is unavailable right now. Your current EHB was kept."].Value
    };

    private async Task<bool> LoadAsync(CancellationToken ct)
    {
        if (User.GetAccountId() is not { } accountId || !await accounts.IsWebsiteAccountAsync(accountId, ct)) return false;
        Links = await accounts.ListAsync(accountId, ct);
        return true;
    }

    public sealed class AddInput
    {
        [Required(ErrorMessage = "An OSRS character name is required."), StringLength(100), Display(Name = "OSRS character")]
        public string CharacterName { get; set; } = string.Empty;
        [StringLength(100), Display(Name = "Personal label")]
        public string? PersonalLabel { get; set; }
        [Range(typeof(decimal), "0", "9999999999.99", ParseLimitsInInvariantCulture = true, ErrorMessage = "EHB cannot be negative."), Display(Name = "EHB")]
        public decimal? SavedEhb { get; set; }
    }

    public sealed class EditInput
    {
        public Guid LinkId { get; set; }
        [Required(ErrorMessage = "An OSRS character name is required."), StringLength(100), Display(Name = "OSRS character")]
        public string CharacterName { get; set; } = string.Empty;
        [StringLength(100), Display(Name = "Personal label")]
        public string? PersonalLabel { get; set; }
        [Range(typeof(decimal), "0", "9999999999.99", ParseLimitsInInvariantCulture = true, ErrorMessage = "EHB cannot be negative."), Display(Name = "EHB")]
        public decimal? SavedEhb { get; set; }
    }

    public sealed class LinkInput { public Guid LinkId { get; set; } }
    public sealed class UnlinkInput { public Guid LinkId { get; set; } public bool ConfirmRegistrationWarning { get; set; } }
}
