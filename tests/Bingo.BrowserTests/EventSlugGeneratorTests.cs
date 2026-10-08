using Bingo.Domain.Events;
using Bingo.Web.Events;

namespace Bingo.BrowserTests;

public sealed class EventSlugGeneratorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(123)]
    public void AllocationCandidatesReserveSpaceForTheSuffix(int sequence)
    {
        var slug = EventSlugGenerator.GenerateCandidate(new string('a', 119) + " - end", sequence);
        Assert.InRange(slug.Length, 1, 120);
        Assert.Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$", slug);
        if (sequence > 1) Assert.EndsWith($"-{sequence}", slug);
    }

    [Theory]
    [InlineData("Danish Summer Bingo 2026", "danish-summer-bingo-2026")]
    [InlineData("Dansk Æ Ø Å Bingo!", "dansk-ae-o-a-bingo")]
    [InlineData("  Clan vs. Clan  ", "clan-vs-clan")]
    public void GeneratesUrlSafeSlugFromEventName(string name, string expected)
    {
        Assert.Equal(expected, EventSlugGenerator.Generate(name));
    }
}
