using Bingo.Application.Evidence;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Catalogue;
using Bingo.Web.TestData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class UiReviewScenarioIntegrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 6, 12, 34, 56, TimeSpan.Zero).AddTicks(1234567);
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_ur_tests").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private readonly string evidence = Path.Combine(Path.GetTempPath(), "bingo-ur-tests-" + Guid.NewGuid());
    public Task InitializeAsync() => database.StartAsync();
    public async Task DisposeAsync()
    {
        await database.DisposeAsync();
        if (Directory.Exists(evidence)) Directory.Delete(evidence, recursive: true);
    }

    [Theory]
    [InlineData("live", EventState.Live)]
    [InlineData("final-review", EventState.AwaitingFinalReview)]
    public async Task ScenariosRespectRealLifecycleAndPublishedEvidenceBoundaries(string profile, EventState currentState)
    {
        await using var factory = Factory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        await scope.ServiceProvider.GetRequiredService<CatalogueSnapshotService>()
            .ApplyAsync(Path.Combine(environment.ContentRootPath, CatalogueSnapshotService.DefaultRelativePath));
        var result = await scope.ServiceProvider.GetRequiredService<UiReviewScenarioSeeder>().SeedAsync(profile);
        db.ChangeTracker.Clear();
        var events = await db.Events.AsNoTracking().ToListAsync();
        var current = Assert.Single(events, value => !value.IsHidden && value.State is EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized);
        Assert.Equal(currentState, current.State);
        Assert.Equal(result.CurrentEventId, current.Id);
        Assert.All(events, value => Assert.False(value.IsDevelopmentFixture));
        Assert.All(events.Where(value => value.ActualSignupOpenedAt is not null), value => Assert.Equal(value.ActualSignupOpenedAt, value.FirstPublicAt));
        Assert.All(events.Where(value => value.IsHidden), value => Assert.Contains(value.State, new[] { EventState.AwaitingFinalReview, EventState.Finalized, EventState.Archived }));
        Assert.Equal(3, events.Count(value => value.IsHidden));
        Assert.Contains(events, value => value.IsHidden && value.State == EventState.Finalized);
        Assert.All(await db.Accounts.ToListAsync(), value => Assert.False(value.MustChangePassword));
        Assert.Equal(10, await db.Accounts.CountAsync());
        Assert.True(events.Count(value => !value.IsHidden && value.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed) > 8);
        var expected = new DateTimeOffset(Now.Ticks - Now.Ticks % 10, TimeSpan.Zero);
        Assert.Equal(expected, result.BuiltAt);
        Assert.Equal(expected.AddDays(-90), current.CreatedAt);
        Assert.Equal(expected, await db.Accounts.Where(value => value.LoginName == "ReviewOwner").Select(value => value.PasswordChangedAt).SingleAsync());
        var closed = events.Single(value => value.Slug == "ur-signups-closed");
        var readiness = await scope.ServiceProvider.GetRequiredService<IEventLifecycleService>().GetStartReadinessAsync(closed.Id);
        Assert.Contains(readiness!.Blockers, value => value.Code == "CURRENT_EVENT_EXISTS");
        var owner = await db.Accounts.SingleAsync(value => value.LoginName == "ReviewOwner");
        var refusal = await scope.ServiceProvider.GetRequiredService<ISubmissionService>().ApproveAsync(result.BlockedSubmissionId, owner.Id);
        Assert.NotNull(refusal.BlockingSubmission);
        Assert.Equal(0, refusal.ApprovedContribution);
        Assert.False(await db.SubmissionContributions.AnyAsync());
        Assert.True((await db.Boards.SingleAsync(value => value.EventId == current.Id)).PublishedCorrectionInProgress);
        Assert.Equal(2, await db.DraftPublicationRosters.Where(value => db.Teams.Any(team => team.Id == value.TeamId && team.EventId == current.Id && team.AffiliationName != null)).Select(value => value.TeamId).Distinct().CountAsync());
        Assert.True(await db.TeamMemberships.AnyAsync(value => value.LeftAt != null));
        Assert.Equal(3, await db.AuditEntries.CountAsync(value => value.Action == "event.hidden"));
        var outcomes = await db.EventCompetitionSynchronizations.Select(value => value.EndUpdateStatus).ToListAsync();
        Assert.Contains(EventCompetitionEndUpdateStatus.Pending, outcomes);
        Assert.Contains(EventCompetitionEndUpdateStatus.Rejected, outcomes);
        Assert.Contains(EventCompetitionEndUpdateStatus.CouldNotUpdate, outcomes);
    }

    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
        .UseEnvironment("Development")
        .UseSetting("ConnectionStrings:Database", database.GetConnectionString())
        .UseSetting("DevelopmentAdminBootstrap:Enabled", "false")
        .UseSetting("EvidenceStorage:Provider", "Local")
        .UseSetting("EvidenceStorage:LocalPath", evidence)
        .UseSetting("WiseOldMan:DevelopmentFake:Enabled", "true")
        .UseSetting("WiseOldMan:BaseUrl", "http://127.0.0.1:1/")
        .ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new ReviewClock());
        }));
    private sealed class ReviewClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
