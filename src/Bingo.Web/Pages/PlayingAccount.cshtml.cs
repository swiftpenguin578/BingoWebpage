using Bingo.Application.Signups;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages;

/// <summary>Shared POST target for the header's playing-account switch; the swap rules stay in <see cref="IParticipantLiveService.SwapAsync"/>.</summary>
public sealed class PlayingAccountModel(IParticipantLiveService live, IStringLocalizer<SharedResource> text) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync(SwitchInput input, string? returnUrl, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();
        if (accountId is null) return Challenge();
        var result = await live.SwapAsync(new(
            input.EventId, input.ParticipantId, input.ExpectedCurrentCharacterId, input.NextCharacterId,
            accountId.Value, User.Identity?.Name ?? "participant"), cancellationToken);
        TempData["StatusMessage"] = result.Succeeded
            ? text["You're now playing as {0}.", result.CharacterName!].Value
            : text[result.Error ?? "The account swap could not be saved."].Value;
        TempData[Bingo.Web.UI.UiMessage.TypeKey] = (result.Succeeded ? Bingo.Web.UI.UiMessageType.Success : Bingo.Web.UI.UiMessageType.Error).ToString();
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }

    public sealed class SwitchInput
    {
        public Guid EventId { get; set; }
        public Guid ParticipantId { get; set; }
        public Guid ExpectedCurrentCharacterId { get; set; }
        public Guid NextCharacterId { get; set; }
    }
}
