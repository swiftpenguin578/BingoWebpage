using Bingo.Web.UI;

namespace Bingo.BrowserTests;

public sealed class UiMessageTests
{
    [Theory]
    [InlineData("The item was saved.", UiMessageType.Information)]
    [InlineData("The item could not be saved.", UiMessageType.Information)]
    [InlineData("A name is required.", UiMessageType.Information)]
    [InlineData("Gemt.", UiMessageType.Information)]
    [InlineData("The review queue is ready.", UiMessageType.Information)]
    public void UndeclaredSeverityIsNeutralRegardlessOfWording(string message, UiMessageType expected)
    {
        Assert.Equal(expected, UiMessage.Resolve(message, null));
    }

    [Fact]
    public void ExplicitTypeOverridesFallbackClassification()
    {
        Assert.Equal(UiMessageType.Error, UiMessage.Resolve("Saved.", "Error"));
    }

    [Theory]
    [InlineData("Error", UiMessageType.Error)]
    [InlineData("Warning", UiMessageType.Warning)]
    [InlineData("Success", UiMessageType.Success)]
    [InlineData("Information", UiMessageType.Information)]
    [InlineData("999", UiMessageType.Information)]
    [InlineData("unknown", UiMessageType.Information)]
    public void ExplicitSeverityDoesNotDependOnEnglish(string declared, UiMessageType expected)
        => Assert.Equal(expected, UiMessage.Resolve("Handlingen kunne ikke gennemføres.", declared));
}
