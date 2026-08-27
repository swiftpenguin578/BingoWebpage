using Bingo.Domain.Events;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.UI;

public sealed record AdminEventStatePresentation(string Label, string Modifier)
{
    public static AdminEventStatePresentation For(EventState state, IStringLocalizer<SharedResource> text) => state switch
    {
        EventState.Draft => new(text["Setup"], "is-yellow"),
        EventState.SignupOpen => new(text["Signups open"], "is-cyan"),
        EventState.SignupClosed => new(text["Signups closed"], "is-orange"),
        EventState.Live => new(text["Live"], "is-green"),
        EventState.AwaitingFinalReview => new(text["Final review"], "is-purple"),
        EventState.Finalized => new(text["Finished"], "is-blue"),
        EventState.Archived => new(text["Archived"], "is-muted"),
        EventState.Cancelled => new(text["Cancelled"], "is-danger"),
        EventState.Discarded => new(text["Discarded"], "is-pink"),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown event lifecycle state.")
    };
}
