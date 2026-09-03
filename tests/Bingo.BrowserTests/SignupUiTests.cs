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

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
