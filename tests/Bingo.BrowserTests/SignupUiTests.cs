using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Web.Pages.Account;

namespace Bingo.BrowserTests;

public sealed class SignupUiTests
{
    [Fact]
    public void SavedEhbRangesValidateInvariantLimitsBeforeCultureSwitch()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var cultureName in new[] { "da-DK", "en-US" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(cultureName);
                CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

                foreach (var model in new object[]
                {
                    new MyAccountsModel.AddInput(),
                    new MyAccountsModel.EditInput(),
                    new OnboardingModel.InputModel()
                })
                {
                    var property = model.GetType().GetProperty("SavedEhb")!;
                    var errors = new List<ValidationResult>();
                    var valid = Validator.TryValidateProperty(9999999999.99m, new ValidationContext(model) { MemberName = property.Name }, errors);

                    Assert.True(valid, $"{model.GetType().Name} failed under {cultureName}: {string.Join("; ", errors.Select(error => error.ErrorMessage))}");
                    Assert.Empty(errors);
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void AccountSelectionsExposeSavedEhbAndOnlyChangeItAfterASelectionChange()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Events", "Signup.cshtml"));
        var pageModel = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Events", "Signup.cshtml.cs"));

        Assert.Contains("data-saved-ehb", markup);
        Assert.Contains("CultureInfo.InvariantCulture", markup);
        Assert.Contains("data-ehb-input", markup);
        Assert.Contains("input.addEventListener(\"change\"", markup);
        Assert.Contains("ehb.value = input.dataset.savedEhb ?? \"\";", markup);
        Assert.Contains("name=\"Input.AccountAnswers[@question.Id].Ehb\"", markup);
        Assert.Contains("Html.ValidationMessage($\"Input.AccountAnswers[{question.Id}].OsrsCharacterId\")", markup);
        Assert.Contains("asp-validation-summary=\"All\"", markup);
        Assert.Contains("data-feedback-target", markup);
        Assert.Contains("<form method=\"post\"", markup);
        Assert.Contains("View signup table", markup);
        Assert.Contains("class=\"signup-sheet\"", markup);
        Assert.Contains("signup-sheet__controls--accounts", markup);
        Assert.Contains("class=\"signup-sheet__field signup-sheet__field--wide\"", markup);
        Assert.Contains("Fetch EHB from WOM", markup);
        Assert.DoesNotContain("Fetch from WOM", markup);
        Assert.Contains("data-ehb-input=\"ehb-@question.Id\"", markup);
        Assert.Contains("data-lookup-token-input=\"lookup-@question.Id\"", markup);
        Assert.DoesNotContain("@if (question.AccountRole == EventCharacterRole.Playing)", markup);
        Assert.Contains("data-account-input", markup);
        Assert.Contains("name=\"Input.FetchQuestionId\" value=\"@question.Id\"", markup);
        Assert.Contains("<svg aria-hidden=\"true\" focusable=\"false\"", markup);
        Assert.Contains("stroke=\"currentColor\"", markup);
        Assert.Contains("signup-sheet__link signup-sheet__link--small", markup);
        Assert.Contains("class=\"signup-sheet__check\"", markup);
        Assert.Contains("signup-sheet__number", markup);
        Assert.Equal(1, markup.Split("Model.MyAccountsUrl").Length - 1);
        Assert.Contains("signup-sheet__field", markup);
        Assert.Contains("signup-sheet__input", markup);
        Assert.Contains("Model.EventView.ConfirmedCount", markup);
        Assert.Contains("Model.EventView.ParticipantCap", markup);
        Assert.Contains("Model.EventView.WaitingCount", markup);
        Assert.Contains("signup-sheet__account-option", markup);
        Assert.Contains("signup-sheet__next", markup);
        Assert.Contains("signup-sheet__capacity", markup);
        Assert.Contains("@if (Model.IsEditing)", markup);
        Assert.Contains("@T[\"Your signup\"]", markup);
        Assert.DoesNotContain("signup-sheet__link\" asp-page=\"/Events/Signups\"", markup);
        Assert.DoesNotContain("signup-sheet__account-ehb-field", markup);
        Assert.DoesNotContain("EHB available", markup);
        Assert.Equal(2, markup.Split("signup-sheet__rail-action").Length - 1);
        Assert.Contains("SignupStatus.Confirmed", pageModel);
        Assert.Contains("SignupStatus.WaitingList", pageModel);
        Assert.Contains("item.ParticipantCap", pageModel);
        Assert.DoesNotContain("public-ui-page-masthead", markup);
        Assert.DoesNotContain("public-ui-component-header", markup);
        Assert.DoesNotContain("class=\"form-select\"", markup);
        Assert.DoesNotContain("class=\"form-control\"", markup);
    }

    [Fact]
    public void SignupFamilyUsesPublicUiSurfacesForFormConfirmationOnboardingAndErrors()
    {
        var root = FindRepositoryRoot();
        foreach (var relative in new[] { "Pages/Events/Signup.cshtml", "Pages/Events/Confirmation.cshtml", "Pages/Events/Signups.cshtml", "Pages/Account/Onboarding.cshtml", "Pages/Errors/StatusCode.cshtml" })
        {
            var page = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", relative));
            Assert.Contains(relative switch
            {
                "Pages/Events/Signup.cshtml" => "signup-sheet",
                "Pages/Events/Confirmation.cshtml" => "event-confirmation",
                "Pages/Account/Onboarding.cshtml" => "identity-page",
                "Pages/Errors/StatusCode.cshtml" => "status-editorial",
                _ => "public-ui-"
            }, page);
            Assert.DoesNotContain("class=\"panel", page);
            Assert.DoesNotContain("class=\"form-page", page);
        }

        var signups = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages/Events/Signups.cshtml"));
        Assert.Contains("public-signups-directory", signups);
        Assert.Contains("public-signups-directory__table", signups);
        Assert.Contains("Model.ParticipantCap", signups);
        Assert.Contains("Model.WaitingCount", signups);
        Assert.DoesNotContain("table-page", signups);

        var confirmation = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages/Events/Confirmation.cshtml"));
        Assert.DoesNotContain("ConfirmLifecycleAction", confirmation);
        Assert.DoesNotContain("I understand that", confirmation);
        Assert.Contains("data-confirm-message=", confirmation);
        Assert.Contains("window.confirm(this.dataset.confirmMessage)", confirmation);
    }

    [Fact]
    public void TeamsUsesFrozenPublicationRosterAndLandingMastheadCorrections()
    {
        var root = FindRepositoryRoot();
        var teams = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages/Events/Teams.cshtml"));
        var teamsModel = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages/Events/Teams.cshtml.cs"));
        var publicCss = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot/css/site.public-ui.css"));
        Assert.Contains("public-teams-directory", teams);
        Assert.Contains("public-teams-directory__picks", teams);
        Assert.Contains("public-teams-directory__mark--dark", teams);
        Assert.Contains("public-teams-directory__meta", teams);
        Assert.Contains("@T[\"Event starts\"]", teams);
        Assert.Contains("@T[\"Event ends\"]", teams);
        Assert.Contains("@T[\"Players\"]", teams);
        Assert.Contains("?? T[\"Not set\"]", teams);
        Assert.Contains("public-teams-directory__masthead-rule", teams);
        Assert.DoesNotContain("ImageUrl", teams);
        Assert.DoesNotContain("public-ui-table", teams);
        Assert.DoesNotContain("PublicTeamImageService", teamsModel);
        Assert.DoesNotContain("ImageUrl", teamsModel);
        Assert.Contains("DraftPublicationRosters", teamsModel);
        Assert.Contains("m.EffectivePickNumber.HasValue ? 0 : 1", teamsModel);
        Assert.Contains("m.EffectivePickNumber", teamsModel);
        Assert.Contains("m.PublicCharacterName, StringComparer.Ordinal", teamsModel);
        Assert.Contains("TeamMembershipRole.Captain => 0, TeamMembershipRole.CoCaptain => 1, _ => 2", teamsModel);
        Assert.Contains("PlayerCount = rosterEntries.Count(x => displayedTeamIds.Contains(x.TeamId))", teamsModel);
        Assert.Contains("public-teams-directory__meta { display: flex; flex-wrap: wrap;", publicCss);
        Assert.Contains("public-teams-directory__meta div + div { padding-left: 1.35rem; border-left: 1px solid var(--public-directory-rule); }", publicCss);
        Assert.Contains("right: calc(100% + var(--public-page-gutter) - 100vw)", publicCss);
        Assert.Contains("public-teams-directory__masthead-rule { width: calc(100vw - var(--public-page-gutter));", publicCss);
        Assert.Contains("public-teams-directory__section-heading::after", publicCss);
        Assert.Contains("public-teams-directory__masthead) { grid-template-columns: minmax(0, 1fr);", publicCss);
        Assert.Contains("public-teams-directory__art { display: none; }", publicCss);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
