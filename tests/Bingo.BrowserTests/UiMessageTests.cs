using Bingo.Web.UI;

namespace Bingo.BrowserTests;

public sealed class UiMessageTests
{
    [Theory]
    [InlineData("The item was saved.", UiMessageType.Success)]
    [InlineData("The item could not be saved.", UiMessageType.Warning)]
    [InlineData("A name is required.", UiMessageType.Error)]
    [InlineData("The review queue is ready.", UiMessageType.Information)]
    public void ResolvesCommonFeedbackMessages(string message, UiMessageType expected)
    {
        Assert.Equal(expected, UiMessage.Resolve(message, null));
    }

    [Fact]
    public void ExplicitTypeOverridesFallbackClassification()
    {
        Assert.Equal(UiMessageType.Error, UiMessage.Resolve("Saved.", "Error"));
    }
}
