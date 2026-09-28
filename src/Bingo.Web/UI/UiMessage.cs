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
        if (Enum.TryParse<UiMessageType>(explicitType, true, out var type) && Enum.IsDefined(type)) return type;

        // Legacy callers without a declared severity remain neutral in every language.
        // Outcome wording must never turn an unsuccessful operation into a success toast.
        return UiMessageType.Information;
    }
}
