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
        var styles = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "css", "site.transitional.application.css"));

        Assert.Contains("admin-review-queue-page", queue);
        Assert.Contains("admin-review-table", queue);
        Assert.Contains("data-admin-review-filter-form", queue);
        Assert.Contains("data-admin-review-search", queue);
        Assert.Contains("data-admin-review-status", queue);
        Assert.Contains("data-review-team", queue);
        Assert.Contains("data-review-tile", queue);
        Assert.Contains("data-review-player", queue);
        Assert.Contains("name=\"status\"", queue);
        Assert.Contains("Model.Status", queue);
        Assert.Contains("public string Search", queueModel);
        Assert.DoesNotContain("EventOption", queueModel);
        Assert.DoesNotContain("TeamOption", queueModel);
        Assert.DoesNotContain("TileOption", queueModel);
        Assert.Contains("No submissions match these filters", queue);
        Assert.Contains("After event end", queue);
        Assert.Contains("Details", queue);
        Assert.Contains("asp-route-eventId", queue);
        Assert.Contains("asp-route-search", queue);
        Assert.Contains("asp-route-status", queue);
        Assert.Contains("admin-review-queue.js", queue);
        Assert.DoesNotContain("Administration", queue);
        Assert.DoesNotContain("Apply filters", queue);
        Assert.DoesNotContain("name=\"teamId\"", queue);
        Assert.DoesNotContain("name=\"tileId\"", queue);
        Assert.DoesNotContain("table-page", queue);
        Assert.DoesNotContain("class=\"panel", queue);
        Assert.DoesNotContain("class=\"form-select", queue);

        Assert.Contains("admin-review-detail-page", detail);
        Assert.Contains("admin-review-detail-workspace", detail);
        Assert.Contains("admin-review-facts", detail);
        Assert.Contains("admin-review-evidence-trigger", detail);
        Assert.Contains("<dialog id=\"evidence-lightbox\"", detail);
        Assert.Contains("Open original asset", detail);
        Assert.Contains("data-admin-reject-reveal", detail);
        Assert.Contains("data-admin-reject-panel", detail);
        Assert.Contains("data-admin-reject-cancel", detail);
        Assert.Contains("admin-review-secondary-grid", detail);
        Assert.Contains("Possible duplicate screenshot", detail);
        Assert.Contains("asp-page-handler=\"Approve\"", detail);
        Assert.Contains("asp-page-handler=\"Reject\"", detail);
        Assert.Contains("asp-page-handler=\"Reverse\"", detail);
        Assert.Contains("asp-page-handler=\"Edit\"", detail);
        Assert.Contains("Model.ReviewOpen && Model.Details.Status == SubmissionStatus.Pending", detail);
        Assert.Contains("var reversible = Model.ReviewOpen && Model.Details.Status == SubmissionStatus.Approved", detail);
        Assert.Contains("Input.ExpectedVersion", detail);
        Assert.Contains("data-correction-requirement", detail);
        Assert.Contains("data-correction-drop-catalogue", detail);
        Assert.Contains("syncCorrectionDropSelector(this)", detail);
        Assert.Contains("action-danger-outline", detail);
        Assert.Contains("asp-route-eventId", detail);
        Assert.Contains("asp-route-search", detail);
        Assert.Contains("asp-route-status", detail);
        Assert.Contains("No active evidence asset", detail);
        Assert.Contains("No evidence assets are attached", detail);
        Assert.DoesNotContain("class=\"table-page", detail);
        Assert.DoesNotContain("class=\"panel", detail);
        Assert.DoesNotContain("class=\"btn", detail);

        Assert.Contains(".admin-shell-body .admin-review-table", styles);
        Assert.Contains("@media (max-width: 1100px)", styles);
        Assert.Contains(".admin-shell-body .admin-review-table td::before", styles);
        Assert.Contains(".admin-shell-body .admin-review-detail-grid", styles);
        Assert.Contains(".admin-shell-body .admin-review-lightbox", styles);
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
        var site = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "site.js"));
        var teamBoardScript = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "team-board-drawer.js"));

        Assert.Contains("@T[\"Team history\"]", teamBoard);
        Assert.Contains("href=\"@Url.Page(\"/Submissions/Index\", new { eventId = Model.Board.EventId, teamId = Model.Team.TeamId })\"", teamBoard);
        Assert.Contains("data-submission-history-link", teamBoard);
        Assert.Contains("<partial name=\"_EvidenceUpload\" model=\"@(\"Input.Evidence\")\" />", forms);
        Assert.Contains("<partial name=\"/Pages/Captain/_SubmissionDetail.cshtml\" model=\"Model\" />", submission);
        Assert.Contains("<partial name=\"/Pages/Captain/_EvidenceUpload.cshtml\" model=\"@(\"Resubmission.Evidence\")\" />", submissionDetail);
        Assert.Contains("data-submission-drawer", teamBoardScript);
        Assert.Contains("window.history.pushState", teamBoardScript);
        Assert.Contains("Model.Details.SubmittedAt.UtcDateTime.ToString(\"yyyy-MM-dd HH:mm 'UTC'\")", review);
        Assert.Contains("data-requirement-id=\"@drop.RequirementId\"", review);
        Assert.Contains("Model.Drops.Where(x => x.RequirementId == Model.Input.RequirementId)", review);
        Assert.Contains("data-correction-drop-catalogue", review);
        Assert.Contains("<script src=\"~/js/public-evidence.js\" asp-append-version=\"true\"></script>", review);
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
