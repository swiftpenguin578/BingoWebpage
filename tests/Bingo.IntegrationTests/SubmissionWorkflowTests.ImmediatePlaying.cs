using System.Security.Claims;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bingo.IntegrationTests;

public sealed partial class SubmissionWorkflowTests
{
    [Theory]
    [InlineData(TeamMembershipRole.Captain)]
    [InlineData(TeamMembershipRole.CoCaptain)]
    public async Task LeadershipSubmissionDrawerDefaultsToSelf(TeamMembershipRole role)
    {
        var setup = await SeedAsync(3, true);
        var (actor, _) = await AddWebsiteCaptainAsync(setup, role, true);
        await using var db = new ApplicationDbContext(options);
        var page = new Bingo.Web.Pages.Captain.SubmitModel(db, Service(db), new EvidenceAuthority(db), new FixedTimeProvider(now),
            new PassthroughLocalizer(), NullLogger<Bingo.Web.Pages.Captain.SubmitModel>.Instance)
        {
            MetadataProvider = new EmptyModelMetadataProvider(),
            PageContext = new PageContext
            {
                ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString())], "test"))
                }
            }
        };
        Assert.IsType<PartialViewResult>(await page.OnGetDrawerAsync(setup.TileId, setup.EventId, setup.TeamId, CancellationToken.None));
        Assert.True(page.CanChooseCreditedParticipant);
        Assert.Equal(setup.ParticipantId, page.DefaultParticipantId);
        Assert.Equal(setup.ParticipantId, page.Input.CreditedParticipantId);
    }

    [Fact]
    public async Task FormerTeamMemberIsNotASubmissionCandidateOrEligibleCredit()
    {
        var setup = await SeedAsync(3, true);
        await using var db = new ApplicationDbContext(options);
        (await db.TeamMemberships.SingleAsync(x => x.EventParticipantId == setup.ParticipantId)).Leave(now, "left team");
        await db.SaveChangesAsync();
        var authority = new EvidenceAuthority(db);
        var scope = await authority.ResolveActorAsync(setup.CaptainId, setup.EventId, setup.TeamId, now);
        Assert.DoesNotContain(await authority.GetCurrentTeamCandidatesAsync(scope, now), x => x.ParticipantId == setup.ParticipantId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => authority.AuthorizeAsync(setup.CaptainId, setup.EventId, setup.TeamId, setup.ParticipantId, now));
    }

    [Theory]
    [InlineData(GlobalRole.User, TeamMembershipRole.Participant)]
    [InlineData(GlobalRole.Admin, TeamMembershipRole.Participant)]
    [InlineData(GlobalRole.SuperAdmin, TeamMembershipRole.Participant)]
    [InlineData(GlobalRole.User, TeamMembershipRole.Captain)]
    [InlineData(GlobalRole.Admin, TeamMembershipRole.Captain)]
    [InlineData(GlobalRole.Admin, TeamMembershipRole.CoCaptain)]
    public async Task SubmissionAuthorityUsesOnlyCurrentParticipantRole(GlobalRole globalRole, TeamMembershipRole teamRole)
    {
        var setup = await SeedAsync(3, true);
        var (actorId, membershipId) = await AddWebsiteCaptainAsync(setup, teamRole, false);
        await using var db = new ApplicationDbContext(options);
        var actor = await db.Accounts.SingleAsync(x => x.Id == actorId);
        actor.SetGlobalRole(globalRole);
        await db.SaveChangesAsync();
        var authority = new EvidenceAuthority(db);
        var scope = await authority.ResolveActorAsync(actorId, setup.EventId, setup.TeamId, now);
        await authority.AuthorizeAsync(actorId, setup.EventId, setup.TeamId, scope.CreditedParticipantId, now);
        if (teamRole == TeamMembershipRole.Participant)
            await Assert.ThrowsAsync<InvalidOperationException>(() => authority.AuthorizeAsync(actorId, setup.EventId, setup.TeamId, setup.ParticipantId, now));
        else
            await authority.AuthorizeAsync(actorId, setup.EventId, setup.TeamId, setup.ParticipantId, now);
        await Assert.ThrowsAsync<InvalidOperationException>(() => authority.AuthorizeAsync(actorId, setup.EventId, setup.TeamId, Guid.NewGuid(), now));

        (await db.TeamMemberships.SingleAsync(x => x.Id == membershipId)).ChangeRole(TeamMembershipRole.Participant);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => authority.AuthorizeAsync(actorId, setup.EventId, setup.TeamId, setup.ParticipantId, now));
        actor.Disable(now);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => authority.AuthorizeAsync(actorId, setup.EventId, setup.TeamId, scope.CreditedParticipantId, now));
    }

    [Fact]
    public async Task NonparticipantAdminHasPrivateReviewAccessButNoDirectSubmissionAuthority()
    {
        var setup = await SeedAsync(3, true);
        await using var db = new ApplicationDbContext(options);
        var authority = new EvidenceAuthority(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => authority.AuthorizeAsync(setup.AdminId, setup.EventId, setup.TeamId, setup.ParticipantId, now));
        Assert.True(await authority.CanViewPrivateEvidenceAsync(setup.AdminId, setup.EventId, setup.TeamId, setup.ParticipantId, now));
        var scope = await authority.ResolveActorAsync(setup.AdminId, setup.EventId, setup.TeamId, now);
        await Assert.ThrowsAsync<InvalidOperationException>(() => authority.GetCurrentTeamCandidatesAsync(scope, now));
    }

    [Fact]
    public async Task ImmediateSwitchesAreSelfOnlyAndRapidSwitchesPreserveSubmittedSnapshots()
    {
        var setup = await SeedAsync(3, true);
        await using (var accountlessDb = new ApplicationDbContext(options))
        {
            var accountless = new ParticipantLiveService(accountlessDb, new FixedTimeProvider(now));
            Assert.Null(await accountless.GetContextAsync(setup.EventId, setup.ParticipantId, setup.CaptainId));
        }
        var (owner, _) = await AddWebsiteCaptainAsync(setup, TeamMembershipRole.Participant, true);
        var (first, second) = await AddPlayingSwitchFixtureAsync(setup);
        var instant = now.AddTicks(17);
        var normalized = now.AddTicks(10);
        await using var db = new ApplicationDbContext(options);
        var service = new ParticipantLiveService(db, new FixedTimeProvider(instant));
        foreach (var outsider in new[] { setup.CaptainId, setup.AdminId })
        {
            Assert.Null(await service.GetContextAsync(setup.EventId, setup.ParticipantId, outsider));
            Assert.False((await service.SwapAsync(new(setup.EventId, setup.ParticipantId, first, second, outsider, "outsider"))).Succeeded);
        }
        var auditBefore = await db.AuditEntries.CountAsync();
        var switched = await service.SwapAsync(new(setup.EventId, setup.ParticipantId, first, second, owner, "owner"));
        Assert.True(switched.Succeeded, switched.Error);
        Assert.Equal(normalized, switched.EffectiveAtUtc);
        Assert.Equal("Second Playing", switched.CharacterName);
        Assert.Equal(auditBefore, await db.AuditEntries.CountAsync());
        var submitted = await Service(db, new FixedTimeProvider(instant)).CreateAsync(Command(setup) with { ActorAccountId = owner });
        for (var index = 0; index < 6; index++)
        {
            var expected = index % 2 == 0 ? second : first;
            var next = index % 2 == 0 ? first : second;
            Assert.True((await service.SwapAsync(new(setup.EventId, setup.ParticipantId, expected, next, owner, "owner"))).Succeeded);
            Assert.Equal(next, (await db.ActiveCharacterAtAsync(setup.EventId, setup.ParticipantId, normalized))!.OsrsCharacterId);
        }
        db.ChangeTracker.Clear();
        var saved = await db.Submissions.SingleAsync(x => x.Id == submitted.SubmissionId);
        Assert.Equal(setup.ParticipantId, saved.CreditedParticipantId);
        Assert.Equal(second, saved.CreditedOsrsCharacterId);
        Assert.Equal(normalized, saved.SubmittedAt);
        var transitions = await db.EventParticipantCharacterSwaps.Where(x => x.EventParticipantId == setup.ParticipantId).ToListAsync();
        Assert.Equal(8, transitions.Count);
        Assert.Equal(8, transitions.Select(x => x.Sequence).Distinct().Count());
        Assert.DoesNotContain(transitions, x => x.EffectiveAtUtc > normalized);

        (await db.Accounts.SingleAsync(x => x.Id == owner)).Disable(now);
        await db.SaveChangesAsync();
        Assert.False((await service.SwapAsync(new(setup.EventId, setup.ParticipantId, second, first, owner, "owner"))).Succeeded);
    }

    [Fact]
    public async Task RetainedPendingSwitchDrainsWithoutRewritingHistory()
    {
        var setup = await SeedAsync(3, true);
        var (owner, _) = await AddWebsiteCaptainAsync(setup, TeamMembershipRole.Participant, true);
        var (first, second) = await AddPlayingSwitchFixtureAsync(setup);
        await using var db = new ApplicationDbContext(options);
        var pending = new EventParticipantCharacterSwap(Guid.NewGuid(), setup.EventId, setup.ParticipantId, first, second, now.AddMinutes(1), now.AddMinutes(-1), owner, "retained pending");
        db.Add(pending);
        await db.SaveChangesAsync();
        var service = new ParticipantLiveService(db, new FixedTimeProvider(now));
        Assert.False((await service.GetContextAsync(setup.EventId, setup.ParticipantId, owner))!.CanSwap);
        var refused = await service.SwapAsync(new(setup.EventId, setup.ParticipantId, first, second, owner, "owner"));
        Assert.False(refused.Succeeded);
        Assert.Contains("previously scheduled", refused.Error);
        var after = new ParticipantLiveService(db, new FixedTimeProvider(now.AddMinutes(1)));
        Assert.True((await after.SwapAsync(new(setup.EventId, setup.ParticipantId, second, first, owner, "owner"))).Succeeded);
        Assert.Equal(first, (await db.ActiveCharacterAtAsync(setup.EventId, setup.ParticipantId, now.AddMinutes(1)))!.OsrsCharacterId);
        db.ChangeTracker.Clear();
        var retained = await db.EventParticipantCharacterSwaps.SingleAsync(x => x.Id == pending.Id);
        Assert.Equal(now.AddMinutes(1), retained.EffectiveAtUtc);
        Assert.Equal(now.AddMinutes(-1), retained.RecordedAtUtc);
        Assert.Equal("retained pending", retained.Reason);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentSwitchAndSubmissionCaptureTheSerializedPlayingAccount(bool switchFirst)
    {
        var setup = await SeedAsync(3, true);
        var (owner, _) = await AddWebsiteCaptainAsync(setup, TeamMembershipRole.Participant, true);
        var (first, second) = await AddPlayingSwitchFixtureAsync(setup);
        var gate = new PauseAttributionSave();
        var blockedOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(gate).Options;
        await using var switchDb = new ApplicationDbContext(switchFirst ? blockedOptions : options);
        await using var submitDb = new ApplicationDbContext(switchFirst ? options : blockedOptions);
        await switchDb.Database.OpenConnectionAsync();
        await submitDb.Database.OpenConnectionAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Task<ParticipantCharacterSwapResult> Switch() => new ParticipantLiveService(switchDb, new FixedTimeProvider(now.AddTicks(19)))
            .SwapAsync(new(setup.EventId, setup.ParticipantId, first, second, owner, "owner"), timeout.Token);
        Task<Bingo.Application.Evidence.SubmissionResult> Submit() => Service(submitDb, new FixedTimeProvider(now.AddTicks(19)))
            .CreateAsync(Command(setup) with { ActorAccountId = owner }, timeout.Token);
        var switchTask = switchFirst ? Switch() : null;
        var submitTask = switchFirst ? null : Submit();
        await gate.Ready.Task.WaitAsync(timeout.Token);
        switchTask ??= Switch();
        submitTask ??= Submit();
        var waiting = switchFirst ? (Task)submitTask : switchTask;
        try { Assert.True(await WaitForDatabaseBlockAsync(waiting, switchFirst ? submitDb : switchDb, switchFirst ? switchDb : submitDb, timeout.Token)); }
        finally { gate.Release.TrySetResult(); }
        Assert.True((await switchTask).Succeeded);
        var submitted = await submitTask;
        await using var verify = new ApplicationDbContext(options);
        var saved = await verify.Submissions.SingleAsync(x => x.Id == submitted.SubmissionId);
        Assert.Equal(switchFirst ? second : first, saved.CreditedOsrsCharacterId);
        Assert.Equal(now.AddTicks(10), saved.SubmittedAt);
        Assert.Equal(second, (await verify.ActiveCharacterAtAsync(setup.EventId, setup.ParticipantId, now.AddTicks(10)))!.OsrsCharacterId);
    }

    // Bug report (8 Oct): the submit drawer's Player field named the primary
    // account after a Live switch, while crediting used the active account.
    [Fact]
    public async Task SubmitDrawerNamesTheActivePlayingAccountThatWouldBeCredited()
    {
        var setup = await SeedAsync(3, true);
        await using (var noHistory = new ApplicationDbContext(options))
        {
            // No switch history: the drawer and crediting both fall back to the only Playing account.
            var fallbackAuthority = new EvidenceAuthority(noHistory);
            Assert.False(await noHistory.EventParticipantCharacterSwaps.AnyAsync(x => x.EventParticipantId == setup.ParticipantId));
            var fallbackScope = await fallbackAuthority.ResolveActorAsync(setup.CaptainId, setup.EventId, setup.TeamId, now);
            Assert.Equal("Player One", (await fallbackAuthority.GetCurrentTeamCandidatesAsync(fallbackScope, now)).Single(x => x.ParticipantId == setup.ParticipantId).CharacterName);
            Assert.Equal("Player One", (await fallbackAuthority.ResolveCreditedCharacterAsync(setup.EventId, setup.ParticipantId, now)).Name);
        }

        var (first, second) = await AddPlayingSwitchFixtureAsync(setup);
        var switchAt = now.AddMinutes(10);
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.Add(new EventParticipantCharacterSwap(Guid.NewGuid(), setup.EventId, setup.ParticipantId, first, second, switchAt, now, setup.CaptainId, "switch"));
            await seed.SaveChangesAsync();
        }

        await using var db = new ApplicationDbContext(options);
        var authority = new EvidenceAuthority(db);
        var scope = await authority.ResolveActorAsync(setup.CaptainId, setup.EventId, setup.TeamId, now);
        // Non-microsecond-aligned instants on both sides of the switch use the persisted precision.
        foreach (var (instant, expected) in new[] { (switchAt.AddTicks(-1), "Player One"), (switchAt.AddTicks(7), "Second Playing") })
        {
            var candidate = (await authority.GetCurrentTeamCandidatesAsync(scope, instant)).Single(x => x.ParticipantId == setup.ParticipantId);
            Assert.Equal(expected, candidate.CharacterName);
            Assert.Equal(expected, (await authority.ResolveCreditedCharacterAsync(setup.EventId, setup.ParticipantId, new DateTimeOffset(instant.UtcTicks - instant.UtcTicks % 10, TimeSpan.Zero))).Name);
        }

        var page = DrawerPage(db, setup.CaptainId, NullLogger<Bingo.Web.Pages.Captain.SubmitModel>.Instance, new FixedTimeProvider(switchAt.AddSeconds(1)));
        Assert.IsType<PartialViewResult>(await page.OnGetDrawerAsync(setup.TileId, setup.EventId, setup.TeamId, CancellationToken.None));
        Assert.Equal("Second Playing", page.Players.Single(x => x.Id == setup.ParticipantId).Name);
    }

    private async Task<(Guid First, Guid Second)> AddPlayingSwitchFixtureAsync(Setup setup)
    {
        await using var db = new ApplicationDbContext(options);
        var first = await db.EventParticipantCharacters.SingleAsync(x => x.EventParticipantId == setup.ParticipantId);
        var second = new OsrsCharacter(Guid.NewGuid(), "Second Playing", "SECOND PLAYING", now);
        db.AddRange(second,
            new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, setup.ParticipantId, second.Id, 1, now, setup.AdminId, null, EventCharacterRole.Playing, 1, EhbSource.Manual, null),
            new EventParticipantCharacterSwap(Guid.NewGuid(), setup.EventId, setup.ParticipantId, null, first.OsrsCharacterId, now.AddHours(-1), now.AddHours(-1), null, null));
        await db.SaveChangesAsync();
        return (first.OsrsCharacterId, second.Id);
    }

    private sealed class PauseAttributionSave : SaveChangesInterceptor
    {
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Ready.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }
}
