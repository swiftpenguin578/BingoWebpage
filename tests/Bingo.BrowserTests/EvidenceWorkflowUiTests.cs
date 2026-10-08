namespace Bingo.BrowserTests;

public sealed class EvidenceWorkflowUiTests
{
    [Fact]
    public void AdminReviewUsesOwnedQueueDetailSurfacesAndRetainsReviewBindings()
    {
        var repositoryRoot = FindRepositoryRoot();
        var queue = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Review", "Index.cshtml"));
        var queueModel = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Review", "Index.cshtml.cs"));
        var detail = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Review", "Details.cshtml"));
        var confirmation = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Shared", "_AdminConfirmation.cshtml"));
        var styles = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "css", "site.transitional.application.css"));

        // U8 (A10): the queue is a new-layout page (Review.dc.html); the same bindings are checked on its markup.
        Assert.Contains("ViewData[\"PageFamily\"] = \"review\";", queue);
        Assert.Contains("[AdminDesign]", queueModel);
        Assert.Contains("data-review-search", queue);
        Assert.Contains("data-review-status", queue);
        Assert.Contains("Model.Status", queue);
        Assert.Contains("public string Search", queueModel);
        Assert.Contains("ReviewList.Filter(AllRows, Search, Status)", queueModel);
        Assert.DoesNotContain("EventOption", queueModel);
        Assert.DoesNotContain("TeamOption", queueModel);
        Assert.DoesNotContain("TileOption", queueModel);
        Assert.Contains("No submissions match", queue);
        Assert.Contains("After end", queue);
        Assert.Contains("Model.DetailsUrl(row.Id)", queue);
        Assert.DoesNotContain("Reverse approval", queue);
        Assert.Contains("admin-review.js", queue);
        Assert.DoesNotContain("admin-review-queue.js", queue);
        Assert.DoesNotContain(".admin-shell-body .admin-review", styles); // U8 1d: transitional Review CSS retired
        Assert.DoesNotContain("Apply filters", queue);
        Assert.DoesNotContain("name=\"teamId\"", queue);
        Assert.DoesNotContain("name=\"tileId\"", queue);

        // U8 (A10): the workspace is a new-layout page; decisions answer in place, and the same server bindings stay.
        var workspace = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Review", "_ReviewWorkspace.cshtml"));
        Assert.Contains("ViewData[\"PageFamily\"] = \"review\";", detail);
        Assert.Contains("<partial name=\"_ReviewWorkspace\" model=\"Model\" />", detail);
        Assert.Contains("asp-page-handler=\"Approve\"", workspace);
        Assert.Contains("asp-page-handler=\"Reject\"", workspace);
        Assert.Contains("asp-page-handler=\"Reverse\"", workspace);
        Assert.Contains("asp-page-handler=\"Edit\"", workspace);
        Assert.Contains("var pendingOpen = Model.ReviewOpen && d.Status == SubmissionStatus.Pending;", workspace);
        Assert.Contains("var approvedOpen = Model.ReviewOpen && d.Status == SubmissionStatus.Approved;", workspace);
        Assert.Contains("Input.ExpectedVersion", workspace);
        Assert.Contains("name=\"confirmed\" value=\"true\"", workspace);
        Assert.Contains("asp-route-eventId", workspace);
        Assert.Contains("asp-route-search", workspace);
        Assert.Contains("asp-route-status", workspace);
        Assert.Contains("No screenshot available", workspace);
        Assert.Contains("No files are attached.", workspace);
        Assert.Contains("The same image is on another submission.", workspace);
        Assert.DoesNotContain("window.confirm", workspace);
        Assert.DoesNotContain("<script>", detail + workspace); // no inline script; the module is admin-review.js
        Assert.DoesNotContain("<dialog", workspace);
        Assert.DoesNotContain("TempData", detail + workspace);
    }

    [Fact]
    public void EvidenceJourneysExposeHistorySharedUploadAndScopedCorrectionDrops()
    {
        var repositoryRoot = FindRepositoryRoot();
        var teamBoard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Events", "TeamBoard.cshtml"));
        var forms = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Captain", "_SubmissionForms.cshtml"));
        var submission = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Submissions", "Submission.cshtml"));
        var submissionDetail = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Captain", "_SubmissionDetail.cshtml"));
        var upload = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Captain", "_EvidenceUpload.cshtml"));
        var review = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Review", "Details.cshtml"));
        var howTo = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "HowTo.cshtml"));
        var site = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "site.js"));
        var teamBoardScript = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "team-board-drawer.js"));

        Assert.Contains("@T[\"Team history\"]", teamBoard);
        Assert.Contains("href=\"@Url.Page(\"/Submissions/Index\", new { eventId = Model.Board.EventId, teamId = Model.Team.TeamId })\"", teamBoard);
        Assert.Contains("data-submission-history-link", teamBoard);
        Assert.Contains("<partial name=\"_EvidenceUpload\" model=\"@(\"Input.Evidence\")\" />", forms);
        Assert.Contains("<partial name=\"/Pages/Captain/_SubmissionDetail.cshtml\" model=\"Model\" />", submission);
        Assert.DoesNotContain("Resubmission.Evidence", submissionDetail);
        Assert.Contains("every later attempt is a new ordinary submission", howTo);
        Assert.DoesNotContain("linked resubmission", howTo);
        Assert.Contains("data-submission-drawer", teamBoardScript);
        Assert.Contains("window.history.pushState", teamBoardScript);
        Assert.Contains("ReviewTime.UtcFull(d.SubmittedAt)", File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Review", "_ReviewWorkspace.cshtml"))); // U8-Q1: UTC first
        // U8 (A10): the review correction scopes its drops to the chosen objective in the page module.
        var reviewWorkspace = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Review", "_ReviewWorkspace.cshtml"));
        Assert.Contains("data-requirement=\"@drop.RequirementId\"", reviewWorkspace);
        Assert.Contains("item.dataset.requirement !== req.value", File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "admin-review.js")));
        Assert.Contains("_ReviewWorkspace", review);
        Assert.Contains("initializeCorrectionDropSelectors", site);
        Assert.Contains("DOMContentLoaded", site);
        Assert.Contains("bingo:content-updated", site);
        Assert.Contains("syncCorrectionDropSelector", site);
        Assert.Contains("window.syncCorrectionDropSelector", site);
        Assert.Contains("group.querySelectorAll(\"option\")", site);
        Assert.DoesNotContain("options: [...group.options]", site);
        Assert.DoesNotContain("correctionDropListener", site);
        Assert.DoesNotContain("option.disabled", site);
        Assert.Contains("ValidateTarget", File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Infrastructure", "Evidence", "SubmissionService.cs")));
        Assert.Contains("class=\"public-ui-evidence-drop", upload);
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
