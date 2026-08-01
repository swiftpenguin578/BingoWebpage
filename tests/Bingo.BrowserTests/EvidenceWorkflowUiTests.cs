namespace Bingo.BrowserTests;

public sealed class EvidenceWorkflowUiTests
{
    [Fact]
    public void EvidenceJourneysExposeHistorySharedUploadAndScopedCorrectionDrops()
    {
        var repositoryRoot = FindRepositoryRoot();
        var teamBoard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Events", "TeamBoard.cshtml"));
        var forms = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Captain", "_SubmissionForms.cshtml"));
        var submission = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Captain", "Submission.cshtml"));
        var upload = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Captain", "_EvidenceUpload.cshtml"));
        var review = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Review", "Details.cshtml"));
        var site = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "site.js"));

        Assert.Contains("Team submission history", teamBoard);
        Assert.Contains("href=\"@Url.Page(\"/Captain/Index\", new { eventId = Model.Board.EventId, teamId = Model.Team.TeamId })\"", teamBoard);
        Assert.Contains("data-submission-history-link", teamBoard);
        Assert.Contains("<partial name=\"_EvidenceUpload\" model=\"@(\"Input.Evidence\")\" />", forms);
        Assert.Contains("<partial name=\"_EvidenceUpload\" model=\"@(\"Resubmission.Evidence\")\" />", submission);
        Assert.Contains("data-submission-result", File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "team-board-overlay.js")));
        Assert.Contains("UTC:", review);
        Assert.Contains("data-requirement-id=\"@d.RequirementId\"", review);
        Assert.Contains("Model.Drops.Where(x => x.RequirementId == Model.Input.RequirementId)", review);
        Assert.Contains("data-correction-drop-catalogue", review);
        Assert.DoesNotContain("@section Scripts", review);
        Assert.Contains("initializeCorrectionDropSelectors", site);
        Assert.Contains("DOMContentLoaded", site);
        Assert.Contains("bingo:content-updated", site);
        Assert.Contains("syncCorrectionDropSelector", site);
        Assert.Contains("onchange=\"syncCorrectionDropSelector(this)\"", review);
        Assert.Contains("window.syncCorrectionDropSelector", site);
        Assert.Contains("group.querySelectorAll(\"option\")", site);
        Assert.DoesNotContain("options: [...group.options]", site);
        Assert.DoesNotContain("correctionDropListener", site);
        Assert.DoesNotContain("option.disabled", site);
        Assert.Contains("ValidateTarget", File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Infrastructure", "Evidence", "SubmissionService.cs")));
        Assert.Contains("compact-evidence-drop", upload);
        Assert.Contains("name=\"@Model\"", upload);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the BingoWebpage repository root.");
    }
}
