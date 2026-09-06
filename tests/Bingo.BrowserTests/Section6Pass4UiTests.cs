namespace Bingo.BrowserTests;

public sealed class Section6Pass4UiTests
{
    [Fact]
    public void PublishedTeamsUseTheSharedContextNavigationWithoutChangingTheLandingRoute()
    {
        var root = FindRepositoryRoot();
        var layout = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Shared", "_Layout.cshtml"));
        var teamsModel = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Events", "Teams.cshtml.cs"));
        var danishResource = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Resources", "SharedResource.da.resx"));
        var styles = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "css", "site.public-ui.css"));

        Assert.Contains("var showPublishedTeamsNavigation", layout);
        Assert.Contains("ViewData[\"PublicEventBoardPublished\"]", teamsModel);
        Assert.Contains("asp-page=\"/Events/Teams\"", layout);
        Assert.Contains("@T[\"Teams\"]", layout);
        Assert.DoesNotContain("@T[\"Teams / Hold\"]", layout);
        Assert.Contains("<data name=\"Teams\" xml:space=\"preserve\"><value>Hold</value></data>", danishResource);
        Assert.Contains("padding-top: 1rem", styles);
        Assert.Contains("padding-top: 0.75rem", styles);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
