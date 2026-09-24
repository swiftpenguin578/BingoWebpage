using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bingo.Application.Boards;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bingo.IntegrationTests;

public sealed partial class Slice10Pass102CompetitionSynchronizationTests
{
    [Fact]
    public async Task PublicRecentDropsUseSubmittedAtForOrderingAndLast24HoursAcrossProjectionAndHttp()
    {
        var wallClock = DateTimeOffset.UtcNow;
        var clockNow = new DateTimeOffset(wallClock.Year, wallClock.Month, wallClock.Day, wallClock.Hour, 0, 0, TimeSpan.Zero)
            .AddMinutes(wallClock.Minute >= 30 ? 60 : 0);
        var f = await FullStatsFixtureAsync(target: 200, actualStartedHoursAgo: 48, eventDurationHours: 72, clockNow: clockNow);
        var now = f.Clock.GetUtcNow();
        var olderId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var dayOldId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var twoHoursOldId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var recentId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var rows = new[]
        {
            (Id: olderId, SubmittedAt: now.AddHours(-30)),
            (Id: dayOldId, SubmittedAt: now.AddHours(-23)),
            (Id: twoHoursOldId, SubmittedAt: now.AddHours(-2)),
            (Id: recentId, SubmittedAt: now.AddMinutes(-10))
        };
        await using (var setup = new ApplicationDbContext(options))
        {
            var ev = await setup.Events.SingleAsync(value => value.Id == f.Event.Id);
            foreach (var row in rows)
            {
                var submission = new Submission(row.Id, f.Event.Id, f.Team.Id, f.Tiles[0].Id, f.Requirements[0].Id,
                    f.Drops[0].Id, f.Players[0].Id, f.Characters[0].Id, f.Characters[0].DisplayName,
                    f.Admin.Id, 1, row.SubmittedAt, null, null);
                submission.Approve(1, now, ev.AnnouncementGeneration, false, ev.ReserveAnnouncementOrdinal());
                setup.Add(submission);
                setup.Add(new SubmissionContribution(Guid.NewGuid(), submission.Id, f.Team.Id, f.Requirements[0].Id,
                    f.Drops[0].Id, f.Players[0].Id, 1, now));
            }
            await setup.SaveChangesAsync();
        }

        var expected = new[] { recentId, twoHoursOldId, dayOldId };
        await using (var read = new ApplicationDbContext(options))
        {
            var boards = new Bingo.Infrastructure.Boards.PublicBoardService(read, f.Clock);
            var board = await boards.GetEventBoardAsync(f.Event.Slug, 3);
            Assert.NotNull(board);
            Assert.Equal(expected, board.RecentDrops.Take(3).Select(drop => drop.SubmissionId));
            Assert.Equal(3, board.RecentDropSummary!.DropsLast24Hours);
            Assert.Equal(now.AddMinutes(-10), board.RecentDrops[0].SubmittedAt);
            Assert.All(board.RecentDrops, drop => Assert.Equal(now, drop.ReviewedAt));

            var feed = await boards.GetRecentDropsAsync(f.Event.Slug, 3);
            Assert.NotNull(feed);
            Assert.Equal(expected, feed.Drops.Select(drop => drop.SubmissionId));
            var deepLink = await boards.GetRecentDropAsync(f.Event.Slug, olderId);
            Assert.NotNull(deepLink);
            Assert.Equal(now.AddHours(-30), deepLink.SubmittedAt);
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(f.Clock);
            });
        });
        using var client = factory.CreateClient();
        var api = await client.GetFromJsonAsync<PublicRecentDropFeed>($"/api/public/events/{f.Event.Slug}/recent-drops?limit=3");
        Assert.NotNull(api);
        Assert.Equal(expected, api.Drops.Select(drop => drop.SubmissionId));
        Assert.Contains("\"submittedAt\"", await client.GetStringAsync($"/api/public/events/{f.Event.Slug}/recent-drops?limit=3"));
        var apiPage = await client.GetFromJsonAsync<PublicRecentDropFeed>($"/api/public/events/{f.Event.Slug}/recent-drops?limit=2&offset=1");
        Assert.NotNull(apiPage);
        Assert.Equal(expected.Skip(1).Take(2), apiPage.Drops.Select(drop => drop.SubmissionId));

        var missionHtml = await client.GetStringAsync($"/Events/{f.Event.Slug}/Board");
        var missionPositions = expected.Select(id => missionHtml.IndexOf($"data-public-board-activity-submission-id=\"{id}\"", StringComparison.Ordinal)).ToArray();
        Assert.All(missionPositions, position => Assert.True(position >= 0));
        Assert.True(missionPositions.SequenceEqual(missionPositions.OrderBy(position => position)), "Board Recent Activity uses the submission-time top three.");

        var dropsHtml = await client.GetStringAsync($"/Events/{f.Event.Slug}/Board?view=drops&dropCount=4");
        var feedPositions = new[] { recentId, twoHoursOldId, dayOldId, olderId }
            .Select(id => dropsHtml.IndexOf($"data-public-recent-drop-id=\"{id}\"", StringComparison.Ordinal)).ToArray();
        Assert.All(feedPositions, position => Assert.True(position >= 0));
        Assert.True(feedPositions.SequenceEqual(feedPositions.OrderBy(position => position)), "Drops is ordered by SubmittedAt even when every approval time is equal.");
        var recentCardStart = dropsHtml.LastIndexOf("<article", feedPositions[0], StringComparison.Ordinal);
        var recentCardEnd = dropsHtml.IndexOf('>', feedPositions[0]);
        var recentCardHtml = dropsHtml[recentCardStart..(recentCardEnd + 1)];
        Assert.Equal(now.AddMinutes(-10), ReadTimestamp(recentCardHtml, "data-public-recent-drop-submitted-at"));
        Assert.Equal(now, ReadTimestamp(recentCardHtml, "data-public-recent-drop-reviewed-at"));
        Assert.Contains("data-public-recent-drop-group=\"last-hour\"", dropsHtml);
        Assert.Contains("data-public-recent-drop-group=\"last-24-hours\"", dropsHtml);
        Assert.Contains("data-public-recent-drop-group=\"older\"", dropsHtml);

        static DateTimeOffset ReadTimestamp(string html, string attribute)
        {
            var prefix = attribute + "=\"";
            var start = html.IndexOf(prefix, StringComparison.Ordinal);
            Assert.True(start >= 0, $"Expected {attribute} on the rendered drop card.");
            start += prefix.Length;
            var end = html.IndexOf('"', start);
            return DateTimeOffset.Parse(WebUtility.HtmlDecode(html[start..end]), CultureInfo.InvariantCulture);
        }
    }

    [Fact]
    public async Task PublicRecentDropValidityHttpRetainsOlderApprovalsAndRejectsReversedPendingAndHiddenRows()
    {
        var f = await FullStatsFixtureAsync(target: 200);
        var older = await PendingStatsAsync(f, 0, 0, 1);
        var reversed = await PendingStatsAsync(f, 0, 0, 2);
        var pending = await PendingStatsAsync(f, 0, 0, 3);
        await ApproveStatsAsync(f, older);
        await ApproveStatsAsync(f, reversed);
        await using (var setup = new ApplicationDbContext(options))
        {
            var ev = await setup.Events.SingleAsync(x => x.Id == f.Event.Id);
            for (var index = 0; index < 100; index++)
            {
                var at = f.Clock.GetUtcNow().AddSeconds(index + 1);
                var submission = new Submission(Guid.NewGuid(), f.Event.Id, f.Team.Id, f.Tiles[0].Id, f.Requirements[0].Id,
                    f.Drops[0].Id, f.Players[0].Id, f.Characters[0].Id, f.Characters[0].DisplayName, f.Admin.Id, 1, at, null, null);
                submission.Approve(1, at, ev.AnnouncementGeneration, false, ev.ReserveAnnouncementOrdinal());
                setup.Add(submission);
                setup.Add(new SubmissionContribution(Guid.NewGuid(), submission.Id, f.Team.Id, f.Requirements[0].Id,
                    f.Drops[0].Id, f.Players[0].Id, 1, at));
            }
            await setup.SaveChangesAsync();
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.ConfigureServices(services => services.RemoveAll<IHostedService>());
        });
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var route = $"/api/public/events/{f.Event.Slug}/recent-drops?limit=100&loadedSubmissionIds={older},{reversed},{pending},{Guid.NewGuid()}";
        var before = await anonymous.GetFromJsonAsync<PublicRecentDropFeed>(route);
        Assert.NotNull(before);
        Assert.Equal(100, before.Drops.Count);
        Assert.DoesNotContain(before.Drops, drop => drop.SubmissionId == older || drop.SubmissionId == reversed);
        Assert.True(before.ValidSubmissionIds!.ToHashSet().SetEquals([older, reversed]));

        await ReverseStatsAsync(f, reversed);
        var after = await anonymous.GetFromJsonAsync<PublicRecentDropFeed>(route);
        Assert.NotNull(after);
        Assert.Equal(101, after.Total);
        Assert.Equal(older, Assert.Single(after.ValidSubmissionIds!));
        var filtered = await anonymous.GetFromJsonAsync<PublicRecentDropFeed>(route + "&dropSearch=no-matching-drop&dropTeam=no-matching-team");
        Assert.NotNull(filtered);
        Assert.Empty(filtered.Drops);
        Assert.Equal(older, Assert.Single(filtered.ValidSubmissionIds!));

        var overLimit = string.Join(',', Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).Append(older));
        var bounded = await anonymous.GetFromJsonAsync<PublicRecentDropFeed>($"/api/public/events/{f.Event.Slug}/recent-drops?loadedSubmissionIds={overLimit}");
        Assert.NotNull(bounded);
        Assert.Empty(bounded.ValidSubmissionIds!);

        await using (var setup = new ApplicationDbContext(options))
        {
            var ev = await setup.Events.SingleAsync(x => x.Id == f.Event.Id);
            ev.EndEvent(f.Clock.GetUtcNow());
            ev.Hide(f.Admin.Id, f.Clock.GetUtcNow(), ev.Name, "Controlled hidden feed fixture");
            await setup.SaveChangesAsync();
        }
        using var hidden = await anonymous.GetAsync(route);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.DoesNotContain(older.ToString(), await hidden.Content.ReadAsStringAsync());
    }
}
