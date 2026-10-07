using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Bingo.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using System.Security.Cryptography;
using System.Text;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Au12PlacementRuleIntegrationTests
{
    [Theory]
    [InlineData("success", FinalWomRefreshStatus.Succeeded, null)]
    [InlineData("failure", FinalWomRefreshStatus.Failed, null)]
    [InlineData("no-competition", FinalWomRefreshStatus.Skipped, EventCompetitionRefreshSkipReason.NoCompetition)]
    [InlineData("lease", FinalWomRefreshStatus.Skipped, EventCompetitionRefreshSkipReason.RefreshInProgress)]
    [InlineData("retry", FinalWomRefreshStatus.Skipped, EventCompetitionRefreshSkipReason.RetryDelay)]
    [InlineData("not-due", FinalWomRefreshStatus.Skipped, EventCompetitionRefreshSkipReason.NotDue)]
    public async Task Au18PublishedVersionRetainsExactRefreshOutcomeWithoutExtraSkippedFetch(string scenario, FinalWomRefreshStatus expected, EventCompetitionRefreshSkipReason? reason)
    {
        var fixture = await SeedAsync(PlacementRule.CreditedEhbThenScoreTime, false);
        await using var db = new ApplicationDbContext(options);
        var provider = new Au18Client();
        var sync = new EventCompetitionSynchronizationService(db, provider, new Au18Status(), new Clock());
        if (scenario != "no-competition")
        {
            var state = await AddAu18SyncFixtureAsync(db, fixture.EventId);
            if (scenario == "lease") state.AcquireLease("other-fixture-worker", Now.AddMinutes(2));
            if (scenario == "retry") db.Entry(state).Property(x => x.RetryDueAt).CurrentValue = Now.AddMinutes(5);
            if (scenario == "not-due")
            {
                db.Entry(state).Property(x => x.LastAttemptAt).CurrentValue = Now;
                db.Entry(state).Property(x => x.LastSuccessfulAt).CurrentValue = Now;
                db.Entry(state).Property(x => x.NormalDueAt).CurrentValue = Now.AddHours(1);
            }
            await db.SaveChangesAsync();
        }
        provider.Fail = scenario == "failure";
        var callsBefore = provider.Calls;
        var finalization = new EventFinalizationService(db, new PublicBoardService(db, new Clock()), new Clock(), competitionSynchronization: sync);
        var readiness = await finalization.GetReadinessAsync(fixture.EventId);
        var published = await finalization.FinalizeAsync(fixture.EventId, new(fixture.AdminId, "admin"), readiness!.EventVersion);
        Assert.True(published.Published);
        // AU18 / U9 structured outcomes preserve the exact persisted refresh status.
        Assert.Equal(EventState.Archived, published.State);
        Assert.Equal(readiness.EventVersion + 1, published.Version);
        Assert.Equal(expected, published.FinalRefresh!.Status);
        Assert.Equal(reason, published.FinalRefresh.SkipReason);
        Assert.Equal(expected == FinalWomRefreshStatus.Skipped ? 0 : 1, provider.Calls - callsBefore);
        db.ChangeTracker.Clear();
        var history = Assert.Single((await finalization.GetReadinessAsync(fixture.EventId))!.History);
        Assert.NotNull(history.FinalWomRefresh);
        Assert.Equal(expected, history.FinalWomRefresh.Status);
        Assert.Equal(reason, history.FinalWomRefresh.SkipReason);
        Assert.Equal(fixture.AdminId, history.FinalizedByAccountId);
        Assert.Equal("au12-admin", history.FinalizedByUsername);
        Assert.Equal(Now, history.FinalizedAt);
        var inputs = (await db.EventFinalizations.SingleAsync()).CalculationInputsJson;
        Assert.DoesNotContain("SECRET-PROVIDER-DETAIL", inputs);
        Assert.NotNull(inputs);
        using var stored = JsonDocument.Parse(inputs);
        var storedOutcome = stored.RootElement.GetProperty("finalWomRefresh");
        Assert.Equal(expected.ToString(), storedOutcome.GetProperty("Status").GetString());
        Assert.Equal(reason?.ToString(), storedOutcome.GetProperty("SkipReason").GetString());
        var reordered = storedOutcome.Deserialize<ReorderedRefreshOutcome>(ReorderedRefreshJsonOptions)!;
        Assert.Equal(expected.ToString(), reordered.Status.ToString());
        Assert.Equal(reason?.ToString(), reordered.SkipReason?.ToString());
        Assert.NotEqual((int)expected, (int)reordered.Status);
        if (reason is not null) Assert.NotEqual((int)reason, (int)reordered.SkipReason!);

        Assert.NotEqual("Not recorded", FinalizeModel.FinalWomRefreshDescription(history.FinalWomRefresh));
        if (scenario == "not-due")
        {
            Assert.Equal(Now.AddHours(1), history.FinalWomRefresh.NextEligibleAt);
            Assert.Equal("Skipped: the refresh window has not elapsed.", FinalizeModel.FinalWomRefreshDescription(history.FinalWomRefresh));
        }

        // Controlled compatibility fixture for the numeric JSON written by the initial local implementation.
        var numericJson = JsonSerializer.Serialize(new
        {
            finalWomRefresh = new { Status = (int)expected, SkipReason = (int?)reason, history.FinalWomRefresh.NextEligibleAt }
        });
        db.Entry(await db.EventFinalizations.SingleAsync()).Property(x => x.CalculationInputsJson).CurrentValue = numericJson;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var numeric = Assert.Single((await finalization.GetReadinessAsync(fixture.EventId))!.History).FinalWomRefresh;
        Assert.Equal(history.FinalWomRefresh, numeric);
    }

    [Fact]
    public async Task Au18ReopenRepublishKeepsPriorOutcomeAndActorHistoryAndLegacyRemainsUnknown()
    {
        var fixture = await SeedAsync(PlacementRule.CreditedEhbThenScoreTime, false);
        await using var db = new ApplicationDbContext(options);
        var provider = new Au18Client();
        var sync = new EventCompetitionSynchronizationService(db, provider, new Au18Status(), new Clock());
        await AddAu18SyncFixtureAsync(db, fixture.EventId);
        var finalization = new EventFinalizationService(db, new PublicBoardService(db, new Clock()), new Clock(), competitionSynchronization: sync);
        var first = await finalization.GetReadinessAsync(fixture.EventId);
        await finalization.FinalizeAsync(fixture.EventId, new(fixture.AdminId, "admin"), first!.EventVersion);
        var original = await db.EventFinalizations.SingleAsync();
        var originalJson = original.CalculationInputsJson;
        var reopener = Account.CreateWebsite(Guid.NewGuid(), "Reopener", "REOPENER", Now); reopener.SetGlobalRole(GlobalRole.Admin);
        db.Add(reopener); await db.SaveChangesAsync();
        await finalization.UnfinalizeAsync(fixture.EventId, "Recheck evidence", true, new(reopener.Id, "Reopener"), (await db.Events.SingleAsync()).Version);
        var second = await finalization.GetReadinessAsync(fixture.EventId);
        await finalization.FinalizeAsync(fixture.EventId, new(reopener.Id, "Reopener"), second!.EventVersion);
        db.ChangeTracker.Clear();
        var history = (await finalization.GetReadinessAsync(fixture.EventId))!.History;
        Assert.Equal(2, history.Count);
        var old = history.Single(x => x.Version == 1);
        Assert.False(old.Active);
        Assert.Equal(FinalWomRefreshStatus.Succeeded, old.FinalWomRefresh!.Status);
        Assert.Equal(reopener.Id, old.UnfinalizedByAccountId);
        Assert.Equal("Reopener", old.UnfinalizedByUsername);
        Assert.Equal(Now, old.UnfinalizedAt);
        Assert.Equal("Recheck evidence", old.UnfinalizeReason);
        var current = history.Single(x => x.Version == 2);
        Assert.True(current.Active);
        Assert.Equal(reopener.Id, current.FinalizedByAccountId);
        Assert.Equal(FinalWomRefreshStatus.Skipped, current.FinalWomRefresh!.Status);
        Assert.Equal(EventCompetitionRefreshSkipReason.NotDue, current.FinalWomRefresh.SkipReason);
        Assert.Equal(originalJson, (await db.EventFinalizations.SingleAsync(x => x.Id == original.Id)).CalculationInputsJson);
        // Controlled legacy fixture: a pre-AU18 version has no recorded outcome.
        db.Entry(await db.EventFinalizations.SingleAsync(x => x.Id == original.Id)).Property(x => x.CalculationInputsJson).CurrentValue = "{}";
        await db.SaveChangesAsync();
        var legacy = (await finalization.GetReadinessAsync(fixture.EventId))!.History.Single(x => x.Version == 1);
        Assert.Null(legacy.FinalWomRefresh);
        Assert.Equal("Not recorded", FinalizeModel.FinalWomRefreshDescription(legacy.FinalWomRefresh));
        var admin = await db.Accounts.SingleAsync(x => x.Id == fixture.AdminId);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "password"), false, Now, false);
        await db.SaveChangesAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new Clock());
            });
        });
        foreach (var culture in new[] { "en", "da" })
        {
            using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
            var login = await client.GetStringAsync("/Account/Login");
            var token = Regex.Match(login, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);
            using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
                { ["Input.Username"] = "au12-admin", ["Input.Password"] = "password", ["__RequestVerificationToken"] = token }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Admin/Events/Finalize/{fixture.EventId}"));
            // AU18 / A10: only non-success refresh notes appear; legacy unknown has no fabricated note.
            Assert.DoesNotContain(culture == "en" ? "Not recorded" : "Ikke registreret", html);
            Assert.Contains(culture == "en" ? "Reopened by Reopener" : "Genåbnet af Reopener", html);
            Assert.Contains(culture == "en" ? "Skipped: the refresh window has not elapsed." : "Sprunget over: opdateringsvinduet er ikke udløbet.", html);
            Assert.Contains(culture == "en" ? "More credited EHB, then earlier current-score time." : "Mere tildelt EHB, derefter tidligst opnåede aktuelle score.", html);
        }
    }

    private static readonly JsonSerializerOptions ReorderedRefreshJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // Deliberately reordered and renumbered: persisted names must retain their meaning.
    private enum ReorderedRefreshStatus { Skipped = 40, Succeeded = 41, Failed = 42 }
    private enum ReorderedRefreshSkipReason
    {
        ServiceUnavailable = 40, NotDue = 41, RetryDelay = 42, RefreshInProgress = 43,
        NoCompetition = 44, IncompleteEventWindow = 45, EventNotInFinalReview = 46, EventUnavailable = 47
    }
    private sealed record ReorderedRefreshOutcome(ReorderedRefreshStatus Status,
        ReorderedRefreshSkipReason? SkipReason, DateTimeOffset? NextEligibleAt);

    private static async Task<EventCompetitionSynchronization> AddAu18SyncFixtureAsync(ApplicationDbContext db, Guid eventId)
    {
        // Controlled pre-existing link; configuring provider integration after live play is intentionally forbidden.
        var assignments = await db.EventParticipantCharacters.AsNoTracking()
            .Where(x => x.EventId == eventId && x.EventRole == EventCharacterRole.Playing && x.ReleasedAt == null)
            .OrderBy(x => x.Id).Select(x => $"{x.Id:N}:{x.EventParticipantId:N}:{x.OsrsCharacterId:N}").ToListAsync();
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', assignments)))).ToLowerInvariant();
        var state = new EventCompetitionSynchronization(Guid.NewGuid(), eventId, 1, 42, "AU18 fixture",
            Now.AddHours(-4), Now.AddHours(-1), fingerprint, Now);
        db.Add(state); await db.SaveChangesAsync();
        return state;
    }

    private sealed class Au18Client : IWiseOldManCompetitionClient
    {
        public int Calls { get; private set; }
        public bool Fail { get; set; }
        public Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, CancellationToken cancellationToken = default)
            => GetCompetitionAsync(competitionId, [], cancellationToken);
        public Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, IReadOnlyCollection<string> metrics, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Fail ? new WiseOldManCompetitionResult(WiseOldManCompetitionStatus.Unavailable, Message: "SECRET-PROVIDER-DETAIL")
                : new(WiseOldManCompetitionStatus.Success, new WiseOldManCompetition(42, "AU18 fixture", Now.AddHours(-4), Now.AddHours(-1), Now,
                    [new("AU12 Player 0", "REGULAR", 1m), new("AU12 Player 1", "REGULAR", 1m)])));
        }
    }
    private sealed class Au18Status : IWiseOldManStatus
    {
        public WiseOldManRequestStatus GetStatus() => new(20, 17, null, null, null, null, null, null);
    }
}
