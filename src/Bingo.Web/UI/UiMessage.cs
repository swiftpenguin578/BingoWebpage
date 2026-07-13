namespace Bingo.Web.UI;

public enum UiMessageType
{
    Information,
    Success,
    Warning,
    Error
}

public static class UiMessage
{
    public const string TypeKey = "StatusMessageType";

    public static UiMessageType Resolve(string message, string? explicitType)
    {
        if (Enum.TryParse<UiMessageType>(explicitType, true, out var type)) return type;

        var value = message.ToLowerInvariant();
        if (ContainsAny(value, "failed", "cannot", "can't", "invalid", "required", "not found", "unable", "must be", "error"))
            return UiMessageType.Error;
        if (ContainsAny(value, "locked", "already", "no longer", "conflict", "not saved", "could not be", "changed before", "needs "))
            return UiMessageType.Warning;
        if (ContainsAny(value, "added", "approved", "changed", "corrected", "created", "disabled", "enabled", "finalized", "imported", "increased", "re-enabled", "rejected", "reopened", "reset", "saved", "started", "submitted", "updated", "withdrawn"))
            return UiMessageType.Success;
        return UiMessageType.Information;
    }

    private static bool ContainsAny(string value, params string[] candidates) => candidates.Any(value.Contains);
}
