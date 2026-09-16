using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Bingo.Application.Announcements;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Announcements;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class DropAnnouncementPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_drop_announcements")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task EligibleParticipantSeesEntryAndOnlyOneConcurrentExpansionClaimWins()
    {
        var seed = await SeedAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            var service = new DropAnnouncementService(db, TimeProvider.System);
            var snapshot = await service.GetAsync(seed.AccountId, seed.EventId);
            Assert.NotNull(snapshot);
            Assert.Single(snapshot.Queue);
            Assert.Equal(1, snapshot.NewCount);
            var paged = await service.GetAsync(seed.AccountId, seed.EventId, queueLimit: 1, queueOffset: 1);
            Assert.NotNull(paged);
            Assert.Empty(paged.Queue);
            var newIds = await service.GetNewSubmissionIdsAsync(seed.AccountId, seed.EventId, [seed.SubmissionId]);
            Assert.Contains(seed.SubmissionId, newIds);
        }

        var claims = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = new ApplicationDbContext(options);
            return await new DropAnnouncementService(db, TimeProvider.System).ClaimAutomaticExpansionAsync(seed.AccountId, seed.EventId);
        }));

        Assert.Single(claims, value => value);
        Assert.Single(claims, value => !value);
        await using var verify = new ApplicationDbContext(options);
        Assert.NotNull(await verify.DropAnnouncementAccountStates.SingleAsync());
    }

    [Fact]
    public async Task AutomaticClaimAdvancesOnlyThroughTheReceivedSnapshotBoundary()
    {
        var seed = await SeedAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            await AddApprovedSubmission(db, seed, DateTimeOffset.UtcNow.AddSeconds(1));
            await db.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var service = new DropAnnouncementService(db, TimeProvider.System);
            Assert.True(await service.ClaimAutomaticExpansionAsync(seed.AccountId, seed.EventId, snapshotSequence: 1));
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var state = await db.DropAnnouncementAccountStates.SingleAsync();
            Assert.Equal(1, state.LastAutomaticExpansionOrdinal);
            state.SetExpansionCooldown(DateTimeOffset.UtcNow.AddSeconds(-1));
            await db.SaveChangesAsync();
            Assert.True(await new DropAnnouncementService(db, TimeProvider.System).ClaimAutomaticExpansionAsync(seed.AccountId, seed.EventId, snapshotSequence: 2));
            Assert.Equal(2, state.LastAutomaticExpansionOrdinal);
        }
    }

    [Fact]
    public async Task ApprovedEntryIsEventWideForAnotherEligibleTeamMemberButNotNonParticipant()
    {
        var seed = await SeedAsync();
        var recipient = Account.CreateWebsite(Guid.NewGuid(), $"recipient-{Guid.NewGuid():N}", $"RECIPIENT-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var outsider = Account.CreateWebsite(Guid.NewGuid(), $"outsider-{Guid.NewGuid():N}", $"OUTSIDER-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        await using (var db = new ApplicationDbContext(options))
        {
            var reviewedAt = await db.Submissions.Where(x => x.Id == seed.SubmissionId).Select(x => x.ReviewedAt).SingleAsync();
            var team = new Team(Guid.NewGuid(), seed.EventId, "Other Team", $"other-team-{Guid.NewGuid():N}", TeamFormationType.Preformed, null, false, reviewedAt);
            var participant = new EventParticipant(Guid.NewGuid(), seed.EventId, SignupStatus.Confirmed, 2, reviewedAt!.Value, SignupSource.AdminCreated);
            participant.AssignOwner(recipient);
            var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, reviewedAt.Value, null, null);
            db.AddRange(recipient, outsider, team, participant, membership);
            await db.SaveChangesAsync();
        }

        await using var read = new ApplicationDbContext(options);
        var recipientSnapshot = await new DropAnnouncementService(read, TimeProvider.System).GetAsync(recipient.Id, seed.EventId);
        Assert.NotNull(recipientSnapshot);
        Assert.Contains(recipientSnapshot.Queue, entry => entry.SubmissionId == seed.SubmissionId && entry.TeamName == "Team");
        Assert.Contains(seed.SubmissionId, recipientSnapshot.NewSubmissionIds);
        Assert.Null(await new DropAnnouncementService(read, TimeProvider.System).GetAsync(outsider.Id, seed.EventId));
    }

    [Fact]
    public async Task ExactAcknowledgementAndGenerationBoundaryKeepLaterApprovalsNew()
    {
        var seed = await SeedAsync();
        await using (var db = new ApplicationDbContext(options))
            await new DropAnnouncementService(db, TimeProvider.System).AcknowledgeBothAsync(seed.AccountId, seed.EventId, seed.SubmissionId);

        await using (var db = new ApplicationDbContext(options))
        {
            var second = await AddApprovedSubmission(db, seed, DateTimeOffset.UtcNow.AddSeconds(1));
            await db.SaveChangesAsync();
            await using var read = new ApplicationDbContext(options);
            var snapshot = await new DropAnnouncementService(read, TimeProvider.System).GetAsync(seed.AccountId, seed.EventId);
            Assert.NotNull(snapshot);
            Assert.DoesNotContain(seed.SubmissionId, snapshot.NewSubmissionIds);
            Assert.Contains(second.Id, snapshot.NewSubmissionIds);

            var eventItem = await db.Events.SingleAsync(x => x.Id == seed.EventId);
            eventItem.ClearAnnouncements();
            await db.SaveChangesAsync();
        }

        await using var cleared = new ApplicationDbContext(options);
        var afterFinalization = await new DropAnnouncementService(cleared, TimeProvider.System).GetAsync(seed.AccountId, seed.EventId);
        Assert.NotNull(afterFinalization);
        Assert.Empty(afterFinalization.Queue);
        Assert.Empty(afterFinalization.NewSubmissionIds);
        Assert.Equal(2, afterFinalization.Generation);
    }

    [Fact]
    public async Task AcknowledgementsStayIndependentAndReversalOrInactiveTeamRemovesEligibility()
    {
        var seed = await SeedAsync();
        Submission second;
        await using (var db = new ApplicationDbContext(options))
        {
            second = await AddApprovedSubmission(db, seed, DateTimeOffset.UtcNow.AddSeconds(1));
            await db.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var snapshot = await new DropAnnouncementService(db, TimeProvider.System).GetAsync(seed.AccountId, seed.EventId);
            Assert.NotNull(snapshot);
            Assert.Equal(2, snapshot.QueueTotalCount);
            Assert.Equal(2, snapshot.NewCount);
        }

        await using (var db = new ApplicationDbContext(options))
            await new DropAnnouncementService(db, TimeProvider.System).DismissAsync(seed.AccountId, seed.EventId, [seed.SubmissionId]);

        await using (var db = new ApplicationDbContext(options))
        {
            var snapshot = await new DropAnnouncementService(db, TimeProvider.System).GetAsync(seed.AccountId, seed.EventId);
            Assert.NotNull(snapshot);
            Assert.NotNull(snapshot.ExpansionCooldownUntil);
            Assert.Single(snapshot.Queue);
            Assert.Equal(2, snapshot.NewCount);
        }

        await using (var db = new ApplicationDbContext(options))
            await new DropAnnouncementService(db, TimeProvider.System).AcknowledgeDropsAsync(seed.AccountId, seed.EventId, [seed.SubmissionId]);

        await using (var db = new ApplicationDbContext(options))
        {
            var snapshot = await new DropAnnouncementService(db, TimeProvider.System).GetAsync(seed.AccountId, seed.EventId);
            Assert.NotNull(snapshot);
            Assert.Single(snapshot.NewSubmissionIds);
            Assert.Equal(second.Id, snapshot.NewSubmissionIds[0]);

            var submission = await db.Submissions.SingleAsync(x => x.Id == second.Id);
            var contribution = await db.SubmissionContributions.SingleAsync(x => x.SubmissionId == second.Id);
            var now = DateTimeOffset.UtcNow;
            submission.Reverse("reversed for announcement test", now);
            contribution.Reverse(now);
            await db.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var snapshot = await new DropAnnouncementService(db, TimeProvider.System).GetAsync(seed.AccountId, seed.EventId);
            Assert.NotNull(snapshot);
            Assert.Empty(snapshot.Queue);
            Assert.Empty(snapshot.NewSubmissionIds);
        }

        await using (var db = new ApplicationDbContext(options))
        {
            (await db.Teams.SingleAsync(x => x.Id == seed.TeamId)).SetActive(false);
            await db.SaveChangesAsync();
            Assert.Null(await new DropAnnouncementService(db, TimeProvider.System).GetAsync(seed.AccountId, seed.EventId));
        }
    }

    [Fact]
    public async Task SnapshotDismissalClearsPagedQueueButPreservesLaterApproval()
    {
        var seed = await SeedAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            var eventItem = await db.Events.SingleAsync(x => x.Id == seed.EventId);
            for (var index = 0; index < 105; index++)
            {
                var at = DateTimeOffset.UtcNow.AddSeconds(index);
                var submission = new Submission(Guid.NewGuid(), seed.EventId, seed.TeamId, seed.TileId, seed.RequirementId, null, seed.ParticipantId, seed.CharacterId, "Player", seed.AccountId, 1, at, null, null);
                submission.Approve(1, at, eventItem.AnnouncementGeneration, false, eventItem.ReserveAnnouncementOrdinal());
                db.Submissions.Add(submission);
                db.SubmissionContributions.Add(new SubmissionContribution(Guid.NewGuid(), submission.Id, seed.TeamId, seed.RequirementId, null, seed.ParticipantId, 1, at));
            }
            await db.SaveChangesAsync();
        }

        long snapshotSequence;
        await using (var db = new ApplicationDbContext(options))
        {
            var snapshot = await new DropAnnouncementService(db, TimeProvider.System).GetAsync(seed.AccountId, seed.EventId, queueLimit: 100);
            Assert.NotNull(snapshot);
            Assert.Equal(106, snapshot.QueueTotalCount);
            Assert.Equal(106, snapshot.SnapshotSequence);
            snapshotSequence = snapshot.SnapshotSequence;
        }

        Submission newer;
        await using (var db = new ApplicationDbContext(options))
        {
            newer = await AddApprovedSubmission(db, seed, DateTimeOffset.UtcNow.AddMinutes(1));
            await db.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
            await new DropAnnouncementService(db, TimeProvider.System).DismissSnapshotAsync(seed.AccountId, seed.EventId, generation: 1, snapshotSequence: snapshotSequence);

        await using (var db = new ApplicationDbContext(options))
        {
            var after = await new DropAnnouncementService(db, TimeProvider.System).GetAsync(seed.AccountId, seed.EventId);
            Assert.NotNull(after);
            Assert.Equal(1, after.QueueTotalCount);
            Assert.Contains(newer.Id, after.Queue.Select(x => x.SubmissionId));
            Assert.NotNull(after.ExpansionCooldownUntil);
        }
    }

    [Fact]
    public async Task HttpEndpointsRequireLoginAndAntiforgeryAndStayAccountScoped()
    {
        var seed = await SeedAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            var account = await db.Accounts.SingleAsync(value => value.Id == seed.AccountId);
            account.SetPasswordHash(new PasswordHasher<Account>().HashPassword(account, "password"), false);
            await db.SaveChangesAsync();
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var participant = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginPage = await participant.GetStringAsync("/Account/Login");
        using var login = await participant.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = seed.LoginName,
            ["Input.Password"] = "password",
            ["__RequestVerificationToken"] = AntiforgeryToken(loginPage)
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        using var unauthorized = await participant.GetAsync($"/api/drop-announcements/{seed.EventId}");
        Assert.Equal(HttpStatusCode.OK, unauthorized.StatusCode);
        using var missingToken = await participant.PostAsJsonAsync("/api/drop-announcements/clear-all-new", new { eventId = seed.EventId });
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);

        var shell = await participant.GetStringAsync("/");
        var token = AntiforgeryToken(shell);
        using var valid = new HttpRequestMessage(HttpMethod.Post, "/api/drop-announcements/clear-all-new")
        {
            Content = JsonContent.Create(new { eventId = seed.EventId })
        };
        valid.Headers.Add("RequestVerificationToken", token);
        using var cleared = await participant.SendAsync(valid);
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        using var after = await participant.GetAsync($"/api/drop-announcements/{seed.EventId}");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.DoesNotContain(seed.SubmissionId.ToString(), await after.Content.ReadAsStringAsync());

        var otherLoginName = $"other-{Guid.NewGuid():N}";
        var other = Account.CreateWebsite(Guid.NewGuid(), otherLoginName, otherLoginName.ToUpperInvariant(), DateTimeOffset.UtcNow);
        other.SetPasswordHash(new PasswordHasher<Account>().HashPassword(other, "password"), false);
        await using (var db = new ApplicationDbContext(options)) { db.Accounts.Add(other); await db.SaveChangesAsync(); }
        using var nonParticipant = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var otherLoginPage = await nonParticipant.GetStringAsync("/Account/Login");
        using var otherLogin = await nonParticipant.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = other.LoginName,
            ["Input.Password"] = "password",
            ["__RequestVerificationToken"] = AntiforgeryToken(otherLoginPage)
        }));
        Assert.Equal(HttpStatusCode.Redirect, otherLogin.StatusCode);
        using var denied = await nonParticipant.GetAsync($"/api/drop-announcements/{seed.EventId}");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    [Fact]
    public async Task PublishedDropWordingAndTargetIgnorePrivateWorkingRows()
    {
        var seed = await SeedAsync(catalogueDrop: true);
        DropAnnouncementEntry original;
        await using (var db = new ApplicationDbContext(options))
        {
            var snapshot = await new DropAnnouncementService(db, TimeProvider.System).GetAsync(seed.AccountId, seed.EventId);
            original = Assert.Single(snapshot!.Queue);
            Assert.Equal("Published boss", original.BossName);
            Assert.Equal("Published item", original.DropName);
            Assert.Equal(1, original.Target);
            var tile = await db.BoardTiles.SingleAsync(x => x.Id == seed.TileId);
            tile.UpdateContent("Private tile", "Private description", "Private instructions", 1, "/private-artwork.png");
            db.Entry(await db.BoardRequirementSnapshots.SingleAsync(x => x.Id == seed.RequirementId)).Property(x => x.TargetContribution).CurrentValue = 99;
            var drop = await db.BoardRequirementDropSnapshots.SingleAsync(x => x.RequirementId == seed.RequirementId);
            db.Entry(drop).Property(x => x.BossName).CurrentValue = "Private boss";
            db.Entry(drop).Property(x => x.ItemName).CurrentValue = "Private item";
            await db.SaveChangesAsync();
        }
        await using var read = new ApplicationDbContext(options);
        var after = await new DropAnnouncementService(read, TimeProvider.System).GetAsync(seed.AccountId, seed.EventId);
        Assert.Equal(original, Assert.Single(after!.Queue));
    }

    private async Task<Seed> SeedAsync(bool catalogueDrop = false)
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-5);
        var loginName = $"drop-{Guid.NewGuid():N}";
        var account = Account.CreateWebsite(Guid.NewGuid(), loginName, loginName.ToUpperInvariant(), now);
        var eventItem = new BingoEvent(Guid.NewGuid(), "Drop event", $"drop-{Guid.NewGuid():N}", "UTC", account.Id, now);
        eventItem.ConfigureInitialSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddMinutes(-4), now.AddDays(1), 10);
        eventItem.OpenSignups(now.AddDays(-2));
        eventItem.CloseSignups(now.AddDays(-1));
        eventItem.StartEvent(now.AddMinutes(-4));
        var team = new Team(Guid.NewGuid(), eventItem.Id, "Team", $"team-{Guid.NewGuid():N}", TeamFormationType.Preformed, null, false, now);
        var board = new Board(Guid.NewGuid(), eventItem.Id, "Board", 1, 1);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Tile", "Description", "Evidence", 1);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 1, true, true, "Objective", !catalogueDrop);
        var item = new CatalogueItem(Guid.NewGuid(), "Published item", "PUBLISHED ITEM");
        var boss = new BossActivity(Guid.NewGuid(), "Published boss", "published-boss", "Boss", 10, now);
        var source = new SourceDrop(Guid.NewGuid(), boss.Id, item.Id, "1/10", .1m, 1, now);
        var drop = catalogueDrop ? new BoardRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, source.Id, item.Id, boss.Name, item.Name, "1/10", .1m, null, 1) : null;
        var participant = new EventParticipant(Guid.NewGuid(), eventItem.Id, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated);
        participant.AssignOwner(account);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now, null, null);
        var character = new OsrsCharacter(Guid.NewGuid(), "Player", "PLAYER", now);
        var submission = new Submission(Guid.NewGuid(), eventItem.Id, team.Id, tile.Id, requirement.Id, drop?.Id, participant.Id, character.Id, character.DisplayName, account.Id, 1, now, null, null);
        submission.Approve(1, now, eventItem.AnnouncementGeneration, false, eventItem.ReserveAnnouncementOrdinal());
        var contribution = new SubmissionContribution(Guid.NewGuid(), submission.Id, team.Id, requirement.Id, drop?.Id, participant.Id, 1, now);
        await using var db = new ApplicationDbContext(options);
        db.AddRange(account, eventItem, team, board, tile, requirement, participant, membership, character, submission, contribution);
        if (drop is not null) db.AddRange(item, boss, source, drop);
        await db.SaveChangesAsync();
        await BoardApprovalFixture.PublishAsync(db, board, now, [tile], [requirement], drop is null ? [] : [drop]);
        account.CompleteOnboarding(character.Id, now);
        await db.SaveChangesAsync();
        return new Seed(account.Id, account.LoginName, eventItem.Id, eventItem.Slug, submission.Id, team.Id, tile.Id, requirement.Id, participant.Id, character.Id);
    }

    private static async Task<Submission> AddApprovedSubmission(ApplicationDbContext db, Seed seed, DateTimeOffset at)
    {
        var submission = new Submission(Guid.NewGuid(), seed.EventId, seed.TeamId, seed.TileId, seed.RequirementId, null, seed.ParticipantId, seed.CharacterId, "Player", seed.AccountId, 1, at, null, null);
        var eventItem = await db.Events.SingleAsync(x => x.Id == seed.EventId);
        submission.Approve(1, at, eventItem.AnnouncementGeneration, false, eventItem.ReserveAnnouncementOrdinal());
        db.Submissions.Add(submission);
        db.SubmissionContributions.Add(new SubmissionContribution(Guid.NewGuid(), submission.Id, seed.TeamId, seed.RequirementId, null, seed.ParticipantId, 1, at));
        return submission;
    }

    private static string AntiforgeryToken(string html) => Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private sealed record Seed(Guid AccountId, string LoginName, Guid EventId, string EventSlug, Guid SubmissionId, Guid TeamId, Guid TileId, Guid RequirementId, Guid ParticipantId, Guid CharacterId);
}
