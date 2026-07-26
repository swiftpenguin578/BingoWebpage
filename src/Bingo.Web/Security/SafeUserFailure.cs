using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Bingo.Web.Security;

/// <summary>Prevents implementation details from becoming public-facing error text.</summary>
public static class SafeUserFailure
{
    private static readonly Action<ILogger, Exception?> UnexpectedFailure = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(4101, "PublicWorkflowUnexpectedFailure"),
        "Unexpected public account or captain workflow failure.");
    private static readonly HashSet<string> Expected =
    [
        "That Discord account is already linked.",
        "That username or Discord account is already in use.",
        "Discord authentication is required.",
        "Passwords must be between 10 and 200 characters.",
        "This link is no longer valid.",
        "The current password is incorrect.",
        "This submission is no longer editable.",
        "This event is not accepting submissions.",
        "You can only submit evidence for your own team."
    ];

    public static string Message(IStringLocalizer<SharedResource> text, ILogger logger, Exception exception)
    {
        if (exception is InvalidOperationException && Expected.Contains(exception.Message)) return text[exception.Message];
        UnexpectedFailure(logger, exception);
        return text["We could not complete that request. Please try again or contact an administrator."];
    }
}
