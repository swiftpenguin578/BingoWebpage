using System.Data.Common;
using System.Net;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bingo.IntegrationTests;

// U3 / DP:966–971 / C4: these existing operations now belong to SignupSetup;
// terminal mutation/refusal, replay, version and data-integrity assertions are retained.

public sealed partial class SignupQuestionCreationRetryIntegrationTests
{
    [Fact]
    public async Task FirstAcceptedSignupBetweenRenderedEditorAndAddNormalizesAndKeepsStructuralGuards()
    {
        var seed = await SeedAsync();
        var existing = await AddAsync(Request(seed));
        var signup = await SignupRequestAsync(seed);
        await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        await LoginAsync(client, seed.Admin.LoginName);
        var route = $"/Admin/Events/SignupSetup/{seed.EventId}";
        var page = await client.GetStringAsync(route);
        var fields = new Dictionary<string, string> {
            ["Input.Label"] = "Arriving question", ["Input.Type"] = "Text", ["Input.Required"] = "true",
            ["expectedFormVersion"] = existing.FormVersion!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["addRequestId"] = Guid.NewGuid().ToString(), ["__RequestVerificationToken"] = Token(page) };
        var gate = new CommitGate();
        await using var signing = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetConnectionString()).AddInterceptors(gate).Options);
        var accepted = SignupService(signing).SignUpAuthenticatedAsync(signup);
        await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        var adding = PostAsync(client, route, fields);
        try { await AssertLockWaitersAsync(1); }
        finally { gate.Release.TrySetResult(); }
        Assert.True((await accepted).Succeeded);
        var added = await adding;
        Assert.Equal(SignupQuestionCreationOutcome.CompletedAsOptional, added.Outcome);
        Assert.False(added.OriginalDefinition!.Required); Assert.True(added.RequiredNormalizedToOptional);
        Assert.Equal(existing.FormVersion, added.SubmittedFormVersion);
        Assert.Equal(existing.FormVersion + 1, added.FormVersion);
        await using (var db = new ApplicationDbContext(options))
        {
            Assert.Equal(Now, (await db.SignupForms.SingleAsync(x => x.EventId == seed.EventId)).FirstResponseAt);
            Assert.False((await db.SignupQuestions.SingleAsync(x => x.Id == added.QuestionId)).Required);
            Assert.False(await db.SignupAnswers.AnyAsync(x => x.SignupQuestionId == added.QuestionId));
            Assert.Single(await db.EventParticipants.Where(x => x.EventId == seed.EventId).ToListAsync());
        }
        var replay = await PostAsync(client, route, fields);
        Assert.Equal(added.OriginalDefinition, replay.OriginalDefinition); Assert.Equal(added.Outcome, replay.Outcome);
        client.DefaultRequestHeaders.Accept.Clear();
        using (var feedback = await client.PostAsync(route, new FormUrlEncodedContent(fields)))
        {
            Assert.Equal(HttpStatusCode.Redirect, feedback.StatusCode);
            Assert.Contains(added.Message!, await client.GetStringAsync(feedback.Headers.Location));
        }
        // A pre-response editor cannot use the unchanged response-only baseline to rewrite shape.
        // The newly added field has since changed the baseline, so use a second rendered editor
        // from before the first response on another event to reach the structural guard directly.
        var guardedSeed = await SeedAsync(); var guarded = await AddAsync(Request(guardedSeed));
        var guardedSignup = await SignupRequestAsync(guardedSeed);
        var guardedRoute = $"/Admin/Events/SignupSetup/{guardedSeed.EventId}";
        using var guardedClient = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        await LoginAsync(guardedClient, guardedSeed.Admin.LoginName);
        var oldEditor = await guardedClient.GetStringAsync(guardedRoute);
        await using (var db = new ApplicationDbContext(options)) Assert.True((await SignupService(db).SignUpAuthenticatedAsync(guardedSignup)).Succeeded);
        var before = await SnapshotAsync();
        foreach (var structural in new[] { new KeyValuePair<string,string>("Edit.Required", "true"), new("Edit.Type", "Number") })
        {
            var edit = new Dictionary<string,string> { ["questionId"] = guarded.QuestionId!.Value.ToString(),
                ["expectedFormVersion"] = guarded.FormVersion!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Edit.Label"] = "Same label", [structural.Key] = structural.Value, ["__RequestVerificationToken"] = Token(oldEditor) };
            using var response = await guardedClient.PostAsync(guardedRoute + "?handler=Edit", new FormUrlEncodedContent(edit));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("Answer format is locked", await guardedClient.GetStringAsync(response.Headers.Location));
        }
        Assert.Equal(before, await SnapshotAsync());
    }

    [Fact]
    public async Task LostNormalizedResultUsesOriginalAuditAfterEditsAndNonAlignedTimestampRoundtrip()
    {
        var seed = await SeedAsync(); var signup = await SignupRequestAsync(seed);
        await using (var db = new ApplicationDbContext(options)) Assert.True((await SignupService(db).SignUpAuthenticatedAsync(signup)).Succeeded);
        var request = Request(seed) with { Required = true, HelpText = "Original help" };
        var nonAligned = Now.AddTicks(7);
        await using (var lost = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetConnectionString()).AddInterceptors(new LostCommitResponse()).Options))
            await Assert.ThrowsAsync<OperationCanceledException>(() => new SignupService(lost, new SecretHasher(), new AtClock(nonAligned)).AddQuestionAsync(request));
        var recovered = await AddAsync(request);
        Assert.Equal(SignupQuestionCreationOutcome.CompletedAsOptional, recovered.Outcome);
        Assert.True(recovered.Replayed); Assert.False(recovered.OriginalDefinition!.Required);
        await using (var db = new ApplicationDbContext(options))
        {
            var audit = await db.AuditEntries.SingleAsync(x => x.TargetId == recovered.QuestionId.ToString());
            Assert.Equal(Now, audit.OccurredAt); // Real PostgreSQL truncation of 100 ns precision.
            Assert.NotEqual(nonAligned, audit.OccurredAt);
            var question = await db.SignupQuestions.SingleAsync(x => x.Id == recovered.QuestionId);
            question.UpdatePresentation("Later label", "Later help"); await db.SaveChangesAsync();
        }
        var other = await AddAsync(request with { RequestId = Guid.NewGuid(), ExpectedFormVersion = recovered.FormVersion, Required = false });
        Assert.NotEqual(recovered.QuestionId, other.QuestionId);
        var before = await SnapshotAsync(); var replay = await AddAsync(request);
        Assert.Equal(recovered.OriginalDefinition, replay.OriginalDefinition); Assert.Equal(recovered.Outcome, replay.Outcome);
        Assert.Equal(recovered.Message, replay.Message); Assert.Equal(before, await SnapshotAsync());
    }

    [Theory]
    [InlineData("only", 0)]
    [InlineData("settings", 1)]
    [InlineData("definition", 1)]
    [InlineData("explicit", 1)]
    [InlineData("advance", 2)]
    public async Task MarkerOnlyPreservesBaselineButOtherChangesStillInvalidateIt(string change, int advance)
    {
        var seed = await SeedAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            var form = await db.SignupForms.SingleAsync(x => x.EventId == seed.EventId);
            form.RecordAcceptedResponse(Now);
            if (change == "settings") form.Publish(Now);
            if (change == "definition") db.SignupQuestions.Add(new(Guid.NewGuid(), form.Id, seed.EventId, "another", "Another", SignupQuestionType.Text, false, 3, null));
            if (change == "explicit") db.Entry(form).Property(x => x.Version).IsModified = true;
            if (change == "advance") form.AdvanceVersion();
            await db.SaveChangesAsync();
        }
        Assert.Equal(seed.Version + advance, await FormVersionAsync(seed.EventId));
        if (advance > 0)
        {
            var before = await SnapshotAsync();
            Assert.Equal(SignupQuestionCreationOutcome.Stale, (await AddAsync(Request(seed) with { Required = true })).Outcome);
            Assert.Equal(before, await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("invalid json")]
    public async Task MissingOrCorruptCreationAuditNeverGuessesOrCreatesAReplacement(string? snapshot)
    {
        var seed = await SeedAsync(); var request = Request(seed); var created = await AddAsync(request);
        await using (var db = new ApplicationDbContext(options))
            await db.AuditEntries.Where(x => x.TargetId == created.QuestionId.ToString()).ExecuteUpdateAsync(set => set.SetProperty(x => x.AfterState, snapshot));
        var before = await SnapshotAsync(); var replay = await AddAsync(request);
        Assert.Equal(SignupQuestionCreationOutcome.Unavailable, replay.Outcome); Assert.Null(replay.OriginalDefinition); Assert.Null(replay.QuestionId);
        Assert.Equal(before, await SnapshotAsync());
    }

    [Fact]
    public async Task SimultaneousFirstSignupsRetainFirstMarkerAndDefinitionVersion()
    {
        var seed = await SeedAsync(); var first = await SignupRequestAsync(seed); var second = await SignupRequestAsync(seed);
        var gate = new CommitGate();
        await using var db1 = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetConnectionString()).AddInterceptors(gate).Options);
        await using var db2 = new ApplicationDbContext(options);
        var firstTask = SignupService(db1).SignUpAuthenticatedAsync(first);
        await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var secondTask = SignupService(db2, Now.AddMinutes(1)).SignUpAuthenticatedAsync(second);
        try { await AssertLockWaitersAsync(1); }
        finally { gate.Release.TrySetResult(); }
        Assert.True((await firstTask).Succeeded);
        SignupResult? secondResult = null;
        var contention = await Record.ExceptionAsync(async () => secondResult = await secondTask);
        if (contention is not null)
            Assert.Equal(Npgsql.PostgresErrorCodes.SerializationFailure, Assert.IsType<Npgsql.PostgresException>(contention.GetBaseException()).SqlState);
        if (contention is not null || !secondResult!.Succeeded)
        {
            // Serializable contention is an existing signup retry boundary, not a timestamp rewrite.
            await using var retry = new ApplicationDbContext(options);
            Assert.True((await SignupService(retry, Now.AddMinutes(1)).SignUpAuthenticatedAsync(second)).Succeeded);
        }
        await using var verify = new ApplicationDbContext(options);
        var form = await verify.SignupForms.SingleAsync(x => x.EventId == seed.EventId);
        Assert.Equal(Now, form.FirstResponseAt); Assert.Equal(seed.Version, form.Version);
        Assert.Equal(2, await verify.EventParticipants.CountAsync(x => x.EventId == seed.EventId));
    }

    [Fact]
    public async Task ConcurrentDefinitionAddStillRejectsOldBaselineWithoutPartialEffects()
    {
        var seed = await SeedAsync(); var request = Request(seed) with { Required = true };
        var gate = new CommitGate();
        await using var winningDb = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetConnectionString()).AddInterceptors(gate).Options);
        var winning = Service(winningDb).AddQuestionAsync(request);
        await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var stale = AddAsync(request with { RequestId = Guid.NewGuid(), Label = "Stale definition" });
        try { await AssertLockWaitersAsync(1); }
        finally { gate.Release.TrySetResult(); }
        Assert.True((await winning).Succeeded);
        Assert.Equal(SignupQuestionCreationOutcome.Stale, (await stale).Outcome);
        await AssertCountsAsync(seed, 1);
    }

    private async Task<AuthenticatedSignupRequest> SignupRequestAsync(Seed seed)
    {
        await using var db = new ApplicationDbContext(options);
        var ev = await db.Events.SingleAsync(x => x.Id == seed.EventId);
        if (ev.State == Bingo.Domain.Events.EventState.Draft)
        {
            ev.ConfigureSchedule(Now.AddHours(-1), Now.AddHours(1), null, Now.AddHours(2), Now.AddHours(3), 10);
            ev.OpenSignups(Now.AddHours(-1));
        }
        var player = Account.CreateWebsite(Guid.NewGuid(), $"p-{Guid.NewGuid():N}", $"P-{Guid.NewGuid():N}", Now);
        var character = new OsrsCharacter(Guid.NewGuid(), $"char-{Guid.NewGuid():N}", $"CHAR-{Guid.NewGuid():N}", Now);
        db.AddRange(player, character, new AccountOsrsCharacter(Guid.NewGuid(), player.Id, character.Id, true, 0, Now));
        await db.SaveChangesAsync();
        var primary = await db.SignupQuestions.SingleAsync(x => x.EventId == seed.EventId && x.SystemField == SignupSystemField.PrimaryRegularAccount);
        return new(seed.EventId, player.Id, new Dictionary<Guid,AuthenticatedAccountAnswer> { [primary.Id] = new(character.Id, 10) }, new Dictionary<Guid,string>(), null);
    }
    private static SignupService SignupService(ApplicationDbContext db, DateTimeOffset? instant = null) =>
        new(db, new SecretHasher(), new AtClock(instant ?? Now), accountValidation: new SuccessfulWiseOldManAccountValidation());
    private sealed class AtClock(DateTimeOffset instant) : TimeProvider { public override DateTimeOffset GetUtcNow() => instant; }
    private sealed class CommitGate : DbTransactionInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            Reached.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken); return result;
        }
    }
}
