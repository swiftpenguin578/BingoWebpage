using Bingo.Web.Events;

namespace Bingo.BrowserTests;

public sealed class EventSlugGeneratorTests
{
    [Theory]
    [InlineData("Danish Summer Bingo 2026", "danish-summer-bingo-2026")]
    [InlineData("Dansk Æ Ø Å Bingo!", "dansk-ae-o-a-bingo")]
    [InlineData("  Clan vs. Clan  ", "clan-vs-clan")]
    public void GeneratesUrlSafeSlugFromEventName(string name, string expected)
    {
        Assert.Equal(expected, EventSlugGenerator.Generate(name));
    }
}
