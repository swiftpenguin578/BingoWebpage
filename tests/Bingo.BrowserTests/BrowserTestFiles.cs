namespace Bingo.BrowserTests;

internal static class BrowserTestFiles
{
    private static readonly string[] ActiveStylesheets =
    [
        "site.transitional.foundation.css",
        "site.public-ui.css",
        "site.transitional.application.css"
    ];

    public static string ReadActiveStyles(string repositoryRoot) => string.Join(
        Environment.NewLine,
        ActiveStylesheets.Select(file => File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Bingo.Web",
            "wwwroot",
            "css",
            file))));
}
