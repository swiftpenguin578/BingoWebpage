using System.Net;
using System.Net.Http.Json;
using Bingo.Application.Boards;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bingo.IntegrationTests;

public sealed partial class Slice10Pass102CompetitionSynchronizationTests
{
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
