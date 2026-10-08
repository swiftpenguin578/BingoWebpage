using System.Net;
using System.Text.RegularExpressions;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class AdminDesignShellIntegrationTests
{
    [Theory]
    [InlineData("en", "Cancelled", "on 5 Oct")]
    [InlineData("da", "Aflyst", "den 5 okt.")]
    public async Task CancelledSwitcherUsesPersistedCancellationDateInEventTimezone(string language, string stage, string expected)
    {
        var admin = Admin();
        var item = new BingoEvent(Guid.NewGuid(), "Cancelled early", "cancelled-early", "Europe/Copenhagen", admin.Id, Now.AddDays(-10), PlacementRule.LegacyScoreTimeThenEhb);
        item.ConfigureInitialSchedule(null, null, null, Now.AddDays(5), Now.AddDays(15), null);
        var cancellation = new DateTimeOffset(2026, 10, 4, 22, 30, 0, TimeSpan.Zero);
        item.Cancel(admin.Id, cancellation, "Controlled cancellation", true);
        await using (var db = new ApplicationDbContext(options)) { db.AddRange(admin, item); await db.SaveChangesAsync(); }
        await using (var db = new ApplicationDbContext(options))
        {
            var saved = await db.Events.SingleAsync();
            Assert.Equal(cancellation, saved.CancelledAt);
            Assert.True(saved.EventEndsAt > Now);
        }
        await using var factory = IdentityFactory(); using var client = await IdentityClientAsync(factory);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Admin/Events/Identity/{item.Id}"));
        var metadata = Regex.Match(html, "<div class=\"ev-meta-text\">(?<text>.*?)</div>", RegexOptions.Singleline);
        Assert.True(metadata.Success);
        Assert.Equal($"{stage} · {expected}", metadata.Groups["text"].Value);
        if (language == "da") Assert.Contains("Cancelled early blev aflyst, så dets identitet ikke kan ændres.", html);
    }
}
