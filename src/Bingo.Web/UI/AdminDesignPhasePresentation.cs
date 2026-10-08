using Bingo.Domain.Events;

namespace Bingo.Web.UI;

/// <summary>Shared phase colours for the Admin design system, including future page bindings.</summary>
public sealed record AdminDesignPhasePresentation(string BadgeClass, string DotTone)
{
    public static AdminDesignPhasePresentation For(EventState state) => state switch
    {
        EventState.Draft => new("badge-neutral", "tone-draft"),
        EventState.SignupOpen => new("badge-success", "tone-open"),
        EventState.SignupClosed => new("badge-info", "tone-closed"),
        EventState.Live => new("badge-accent", "tone-live"),
        EventState.AwaitingFinalReview => new("badge-warning", "tone-review"),
        EventState.Finalized => new("badge-done", "tone-done"),
        EventState.Archived or EventState.Cancelled or EventState.Discarded => new("badge-outline", "tone-draft"),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown event lifecycle state.")
    };
}
