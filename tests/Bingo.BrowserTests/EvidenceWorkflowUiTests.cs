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
        Assert.DoesNotContain("Apply filters", queue);
        Assert.DoesNotContain("name=\"teamId\"", queue);
        Assert.DoesNotContain("name=\"tileId\"", queue);

        Assert.Contains("admin-review-detail-page", detail);
        Assert.Contains("admin-review-detail-workspace", detail);
        Assert.Contains("admin-review-facts", detail);
        Assert.Contains("admin-review-evidence-trigger", detail);
        Assert.Contains("<dialog id=\"evidence-lightbox\"", detail);
        Assert.Contains("Open original asset", detail);
        Assert.Contains("data-admin-review-confirm", detail);
        Assert.Contains("window.adminConfirmation.open", detail);
        Assert.Contains("maxlength=\"4000\"", confirmation);
        Assert.DoesNotContain("window.confirm", detail);
        Assert.DoesNotContain("data-admin-reject-reveal", detail);
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
