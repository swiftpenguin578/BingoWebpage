using System.Net;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bingo.IntegrationTests;

// U8 Review server rules (brief 94, item 0a): B-Review-2, RL-1/BR-4, RL-1/BR-12, C-CMP-1.
public sealed partial class SubmissionWorkflowTests
{
    private sealed record U8ReviewState(SubmissionStatus Status, int Version, int ApprovedContribution, Guid? DropId, int ReviewActions, int Audits, int Notifications, long EventVersion);

    private async Task<U8ReviewState> U8StateAsync(Guid submissionId)
    {
        await using var db = new ApplicationDbContext(options);
        var s = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == submissionId);
        var target = submissionId.ToString("D");
        return new(s.Status, s.Version, s.ApprovedContribution, s.DropSnapshotId,
            await db.ReviewActions.CountAsync(x => x.SubmissionId == submissionId),
            await db.AuditEntries.CountAsync(x => x.TargetType == "submission" && x.TargetId == target),
            await db.PersonalNotifications.CountAsync(),
            await db.Events.Where(x => x.Id == s.EventId).Select(x => x.Version).SingleAsync());
    }

    [Fact]
    public async Task U8NoOpCorrectionIsRefusedBeforeAnyWriteAndARealChangeStillSaves()
    {
        var setup = await SeedAsync(3, true, createAlternateWeightDrop: true);
        Guid id; Guid character;
        await using (var db = new ApplicationDbContext(options))
        {
            id = (await Service(db).CreateAsync(Command(setup))).SubmissionId;
            character = await db.Submissions.Where(x => x.Id == id).Select(x => x.CreditedOsrsCharacterId).SingleAsync();
        }
        var before = await U8StateAsync(id);
        await using (var db = new ApplicationDbContext(options))
        {
            var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).EditMetadataAsync(new(id, setup.AdminId, setup.TileId, setup.RequirementId, setup.DropId, character, "Nothing differs", before.Version)));
            Assert.Equal("Change at least one detail, or cancel.", refusal.Message);
        }
        Assert.Equal(before, await U8StateAsync(id));
        await using (var db = new ApplicationDbContext(options))
            await Service(db).EditMetadataAsync(new(id, setup.AdminId, setup.TileId, setup.RequirementId, setup.AlternateDropId, character, "The chat shows the other drop", before.Version));
        var after = await U8StateAsync(id);
        Assert.Equal(setup.AlternateDropId, after.DropId);
        Assert.Equal(before.Version + 1, after.Version);
        Assert.Equal(before.ReviewActions + 1, after.ReviewActions);
        Assert.Equal(before.Audits + 1, after.Audits);
    }

    [Theory]
    [InlineData("Approve", null)]
    [InlineData("Approve", 0)]
    [InlineData("Reject", null)]
    [InlineData("Reject", 0)]
    [InlineData("Reverse", null)]
    [InlineData("Reverse", 0)]
    [InlineData("Edit", null)]
    [InlineData("Edit", 0)]
    public async Task U8AdminReviewActionsRefuseAMissingOrZeroVersionBeforeAnyWrite(string action, int? version)
    {
        var setup = await SeedAsync(3, true, createAlternateWeightDrop: true);
        Guid id; Guid character;
        await using (var db = new ApplicationDbContext(options))
        {
            var service = Service(db);
            id = (await service.CreateAsync(Command(setup))).SubmissionId;
            if (action == "Reverse") await service.ApproveCurrentAsync(id, setup.AdminId);
            character = await db.Submissions.Where(x => x.Id == id).Select(x => x.CreditedOsrsCharacterId).SingleAsync();
        }
        var before = await U8StateAsync(id);
        await using (var db = new ApplicationDbContext(options))
        {
            var service = Service(db);
            Task Run(int? expected) => action switch
            {
                "Approve" => service.ApproveAsync(id, setup.AdminId, default, expected),
                "Reject" => service.RejectAsync(id, setup.AdminId, "Reason", default, expected),
                "Reverse" => service.ReverseAsync(id, setup.AdminId, "Reason", default, expected),
                _ => service.EditMetadataAsync(new(id, setup.AdminId, setup.TileId, setup.RequirementId, setup.AlternateDropId, character, "Reason", expected))
            };
            var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => Run(version));
            Assert.Equal(SubmissionService.MissingReviewVersionMessage, refusal.Message);
            Assert.Equal(before, await U8StateAsync(id));
            // A stale (other) version keeps its existing refusal; the current version still saves.
            var stale = await Assert.ThrowsAsync<InvalidOperationException>(() => Run(before.Version + 1));
            Assert.Contains("changed in another request", stale.Message, StringComparison.Ordinal);
            Assert.Equal(before, await U8StateAsync(id));
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var service = Service(db);
            await (action switch
            {
                "Approve" => service.ApproveAsync(id, setup.AdminId, default, before.Version),
                "Reject" => service.RejectAsync(id, setup.AdminId, "Reason", default, before.Version),
                "Reverse" => service.ReverseAsync(id, setup.AdminId, "Reason", default, before.Version),
                _ => service.EditMetadataAsync(new(id, setup.AdminId, setup.TileId, setup.RequirementId, setup.AlternateDropId, character, "Reason", before.Version))
            });
        }
        Assert.Equal(before.Version + 1, (await U8StateAsync(id)).Version);
    }

    [Fact]
    public async Task U8PlayerWithdrawAndCorrectWithoutAVersionBehaveAsBefore()
    {
        var setup = await SeedAsync(3, true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var corrected = await service.CreateAsync(Command(setup));
        await service.CorrectAsync(new(corrected.SubmissionId, setup.CaptainId, setup.TileId, setup.RequirementId, setup.DropId, setup.ParticipantId, 1, "edited without a version", null));
        await service.WithdrawAsync(corrected.SubmissionId, setup.CaptainId);
        Assert.Equal(SubmissionStatus.Withdrawn, (await U8StateAsync(corrected.SubmissionId)).Status);
    }

    [Theory]
    [InlineData("Reject")]
    [InlineData("Reverse")]
    public async Task U8RejectAndReverseWithoutConfirmationWriteNothingAndWithConfirmationSave(string handler)
    {
        var setup = await SeedAsync(3, true);
        Guid id;
        await using (var db = new ApplicationDbContext(options))
        {
            var service = Service(db);
            id = (await service.CreateAsync(Command(setup))).SubmissionId;
            if (handler == "Reverse") await service.ApproveCurrentAsync(id, setup.AdminId);
        }
        var before = await U8StateAsync(id);
        await using var factory = U8Factory();
        using var client = await U8AdminClientAsync(factory, setup);
        var token = await U8TokenAsync(client, id);
        Dictionary<string, string> Fields(bool confirmed)
        {
            var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["Input.ExpectedVersion"] = before.Version.ToString(System.Globalization.CultureInfo.InvariantCulture), ["Input.Reason"] = "The drop message is not visible." };
            if (confirmed) fields["confirmed"] = "true";
            return fields;
        }
        using (var refused = await client.PostAsync($"/Admin/Review/Details/{id}?handler={handler}", new FormUrlEncodedContent(Fields(false))))
            Assert.NotEqual(HttpStatusCode.InternalServerError, refused.StatusCode);
        Assert.Equal(before, await U8StateAsync(id));
        using (var saved = await client.PostAsync($"/Admin/Review/Details/{id}?handler={handler}", new FormUrlEncodedContent(Fields(true))))
            Assert.NotEqual(HttpStatusCode.InternalServerError, saved.StatusCode);
        var after = await U8StateAsync(id);
        Assert.Equal(handler == "Reject" ? SubmissionStatus.Rejected : SubmissionStatus.Reversed, after.Status);
        Assert.Equal(before.ReviewActions + 1, after.ReviewActions);
    }

    [Fact]
    public async Task U8ReadbackAnswersNotFoundForHiddenEventsAndUnknownSubmissions()
    {
        var setup = await SeedAsync(3, true);
        Guid id;
        await using (var db = new ApplicationDbContext(options))
            id = (await Service(db).CreateAsync(Command(setup))).SubmissionId;
        await using var factory = U8Factory();
        using var client = await U8AdminClientAsync(factory, setup);
        using (var visible = await client.GetAsync($"/Admin/Review/Details/{id}?handler=Readback"))
        {
            Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
            Assert.Contains("\"state\":{", await visible.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        using (var unknown = await client.GetAsync($"/Admin/Review/Details/{Guid.NewGuid()}?handler=Readback"))
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        await using (var db = new ApplicationDbContext(options))
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET hidden_at = {now}, hidden_by_account_id = {setup.AdminId}, hidden_reason = {"U8 fixture"} WHERE id = {setup.EventId}");
        using (var hidden = await client.GetAsync($"/Admin/Review/Details/{id}?handler=Readback"))
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using (var queue = await client.GetAsync($"/Admin/Review?eventId={setup.EventId}"))
            Assert.Equal(HttpStatusCode.NotFound, queue.StatusCode);
        using (var unknownQueue = await client.GetAsync($"/Admin/Review?eventId={Guid.NewGuid()}"))
            Assert.Equal(HttpStatusCode.NotFound, unknownQueue.StatusCode);
    }

    // Item 0b: the queue's per-row checks are projections of existing data, with one after-end boundary
    // (ActualEndedAt ?? EventEndsAt) shared with the workspace.
    [Fact]
    public async Task U8QueueRowsProjectEveryCheckWithOneAfterEndBoundary()
    {
        var setup = await SeedAsync(10, true);
        var ids = new List<Guid>();
        await using (var db = new ApplicationDbContext(options))
            for (var i = 0; i < 4; i++) ids.Add((await Service(db).CreateAsync(Command(setup))).SubmissionId);
        DateTimeOffset earlyEnd;
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(x => x.Id == setup.EventId);
            earlyEnd = now.AddHours(1);
            var pausedAt = now.AddMinutes(10); var resumedAt = now.AddMinutes(20);
            db.EventStateTransitions.AddRange(
                new Bingo.Domain.Events.EventStateTransition(Guid.NewGuid(), setup.EventId, Bingo.Domain.Events.EventState.Live, Bingo.Domain.Events.EventState.AwaitingFinalReview, setup.AdminId, pausedAt, "U8 paused", effectiveAt: pausedAt),
                new Bingo.Domain.Events.EventStateTransition(Guid.NewGuid(), setup.EventId, Bingo.Domain.Events.EventState.AwaitingFinalReview, Bingo.Domain.Events.EventState.Live, setup.AdminId, resumedAt, "U8 resumed", effectiveAt: resumedAt));
            item.EndEvent(earlyEnd);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE evidence_assets SET active = false WHERE submission_id = {ids[0]}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE submissions SET submitted_at = {now.AddMinutes(1)} WHERE id = {ids[0]}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE submissions SET submitted_at = {earlyEnd.AddMinutes(10)} WHERE id = {ids[1]}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE submissions SET submitted_at = {now.AddMinutes(15)} WHERE id = {ids[2]}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE submissions SET submitted_at = {now.AddMinutes(30)} WHERE id = {ids[3]}");
        }
        Bingo.Web.Pages.Admin.Review.ReviewList.Row RowOf(IReadOnlyList<Bingo.Web.Pages.Admin.Review.ReviewList.Row> rows, int index) => rows.Single(x => x.Id == ids[index]);
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.AsNoTracking().SingleAsync(x => x.Id == setup.EventId);
            Assert.True(item.EventEndsAt > earlyEnd.AddMinutes(10)); // the scheduled end alone would not flag it
            var rows = await Bingo.Web.Pages.Admin.Review.ReviewList.RowsAsync(db, item, CancellationToken.None);
            Assert.Equal([ids[1], ids[3], ids[2], ids[0]], rows.Select(x => x.Id));
            Assert.True(RowOf(rows, 0).NoScreenshot); Assert.False(RowOf(rows, 0).SameImage); Assert.False(RowOf(rows, 0).AfterEnd); Assert.False(RowOf(rows, 0).Paused);
            Assert.True(RowOf(rows, 1).AfterEnd); Assert.True(RowOf(rows, 1).SameImage); Assert.False(RowOf(rows, 1).NoScreenshot);
            Assert.True(RowOf(rows, 2).Paused); Assert.False(RowOf(rows, 2).AfterEnd);
            Assert.False(RowOf(rows, 3).HasChecks && !RowOf(rows, 3).SameImage);
            Assert.All(rows, row => Assert.False(row.LeftTeam));
            var page = new Bingo.Web.Pages.Admin.Review.DetailsModel(db, Service(db));
            Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(await page.OnGetAsync(ids[1], CancellationToken.None));
            Assert.Equal(10, page.Details.MinutesAfterEventEnd);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            (await db.TeamMemberships.SingleAsync(x => x.TeamId == setup.TeamId && x.EventParticipantId == setup.ParticipantId)).Leave(now.AddMinutes(40), "U8 left");
            await db.SaveChangesAsync();
            var rows = await Bingo.Web.Pages.Admin.Review.ReviewList.RowsAsync(db, await db.Events.AsNoTracking().SingleAsync(x => x.Id == setup.EventId), CancellationToken.None);
            Assert.All(rows, row => Assert.True(row.LeftTeam));
        }
    }

    [Fact]
    public async Task U8NeighboursFollowTheQueueListOfTheLink()
    {
        var setup = await SeedAsync(10, true);
        var ids = new List<Guid>();
        await using (var db = new ApplicationDbContext(options))
        {
            for (var i = 0; i < 3; i++) ids.Add((await Service(db).CreateAsync(Command(setup))).SubmissionId);
            for (var i = 0; i < 3; i++) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE submissions SET submitted_at = {now.AddMinutes(i)} WHERE id = {ids[i]}");
            await Service(db).RejectCurrentAsync(ids[0], setup.AdminId, "U8 rejected");
        }
        async Task<Bingo.Web.Pages.Admin.Review.DetailsModel> Page(Guid id, string search, SubmissionStatus? status)
        {
            var db = new ApplicationDbContext(options);
            var page = new Bingo.Web.Pages.Admin.Review.DetailsModel(db, Service(db)) { Search = search, Status = status };
            Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(await page.OnGetAsync(id, CancellationToken.None));
            return page;
        }
        // Pending first, newest first: ids[2], ids[1], then the rejected ids[0].
        var middle = await Page(ids[1], string.Empty, null);
        Assert.Equal(new Bingo.Web.Pages.Admin.Review.DetailsModel.NeighbourView(2, 3, ids[2], ids[0]), middle.Neighbours);
        var first = await Page(ids[2], string.Empty, SubmissionStatus.Pending);
        Assert.Equal(new Bingo.Web.Pages.Admin.Review.DetailsModel.NeighbourView(1, 2, null, ids[1]), first.Neighbours);
        Assert.Null((await Page(ids[0], string.Empty, SubmissionStatus.Pending)).Neighbours); // not in the list it came from
        Assert.Null((await Page(ids[1], "no such team", null)).Neighbours);
    }

    [Fact]
    public async Task U8DecisionsAnswerDefiniteJsonOutcomes()
    {
        var setup = await SeedAsync(3, true);
        Guid id; Guid character;
        await using (var db = new ApplicationDbContext(options))
        {
            id = (await Service(db).CreateAsync(Command(setup))).SubmissionId;
            character = await db.Submissions.Where(x => x.Id == id).Select(x => x.CreditedOsrsCharacterId).SingleAsync();
        }
        await using var factory = U8Factory();
        using var client = await U8AdminClientAsync(factory, setup);
        var token = await U8TokenAsync(client, id);
        async Task<System.Text.Json.JsonElement> Post(string handler, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = token;
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/Admin/Review/Details/{id}?handler={handler}&eventId={setup.EventId}") { Content = new FormUrlEncodedContent(fields) };
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            return System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
        }
        var before = await U8StateAsync(id);
        var version = before.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var unconfirmed = await Post("Reject", new() { ["Input.ExpectedVersion"] = version, ["Input.Reason"] = "Reason" });
        Assert.Equal("refused", unconfirmed.GetProperty("outcome").GetString());
        Assert.Equal("Confirm this decision before it is saved. Nothing was changed.", unconfirmed.GetProperty("message").GetString());
        var noop = await Post("Edit", new() { ["Input.ExpectedVersion"] = version, ["Input.Reason"] = "Reason", ["Input.BoardTileId"] = setup.TileId.ToString(), ["Input.RequirementId"] = setup.RequirementId.ToString(), ["Input.DropSnapshotId"] = setup.DropId.ToString()!, ["Input.CreditedOsrsCharacterId"] = character.ToString() });
        Assert.Equal("refused", noop.GetProperty("outcome").GetString());
        Assert.Equal("Change at least one detail, or cancel.", noop.GetProperty("message").GetString());
        var missing = await Post("Approve", new());
        Assert.Equal("refused", missing.GetProperty("outcome").GetString());
        Assert.Equal(SubmissionService.MissingReviewVersionMessage, missing.GetProperty("message").GetString());
        var stale = await Post("Approve", new() { ["Input.ExpectedVersion"] = (before.Version + 5).ToString(System.Globalization.CultureInfo.InvariantCulture) });
        Assert.Equal("stale", stale.GetProperty("outcome").GetString());
        Assert.Equal("Pending", stale.GetProperty("status").GetString());
        Assert.Equal(before, await U8StateAsync(id));
        var saved = await Post("Approve", new() { ["Input.ExpectedVersion"] = version });
        Assert.Equal("saved", saved.GetProperty("outcome").GetString());
        Assert.Equal("approve", saved.GetProperty("kind").GetString());
        Assert.Equal((await U8StateAsync(id)).ApprovedContribution, saved.GetProperty("amount").GetInt32());
        var late = await Post("Approve", new() { ["Input.ExpectedVersion"] = version });
        Assert.Equal("stale", late.GetProperty("outcome").GetString());
        Assert.Equal("Approved", late.GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(late.GetProperty("changedBy").GetString()));
    }

    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    [InlineData("Reverse")]
    [InlineData("Edit")]
    public async Task U8LostSessionOnEveryDecisionRedirectsToLoginWithoutWrites(string handler)
    {
        var setup = await SeedAsync(3, true);
        Guid id;
        await using (var db = new ApplicationDbContext(options))
        {
            id = (await Service(db).CreateAsync(Command(setup))).SubmissionId;
            if (handler == "Reverse") await Service(db).ApproveCurrentAsync(id, setup.AdminId);
        }
        await using var factory = U8Factory();
        using var client = await U8AdminClientAsync(factory, setup);
        var token = await U8TokenAsync(client, id);
        var before = await U8StateAsync(id);
        await using (var db = new ApplicationDbContext(options))
        {
            (await db.Accounts.SingleAsync(x => x.Id == setup.AdminId)).Disable(now);
            await db.SaveChangesAsync();
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Admin/Review/Details/{id}?handler={handler}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["Input.ExpectedVersion"] = before.Version.ToString(System.Globalization.CultureInfo.InvariantCulture), ["Input.Reason"] = "Reason", ["confirmed"] = "true" })
        };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = new Uri(client.BaseAddress!, response.Headers.Location!);
        Assert.Equal("/Account/Login", location.AbsolutePath);
        Assert.Contains("accessChanged=true", location.Query, StringComparison.Ordinal);
        Assert.Equal(before, await U8StateAsync(id));
    }

    private WebApplicationFactory<Program> U8Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing").UseSetting("ConnectionStrings:Database", database.GetConnectionString())
        .ConfigureServices(services => { services.RemoveAll<IHostedService>(); services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FixedTimeProvider(now)); }));

    private async Task<HttpClient> U8AdminClientAsync(WebApplicationFactory<Program> factory, Setup setup)
    {
        await using (var seed = new ApplicationDbContext(options))
        {
            var admin = await seed.Accounts.SingleAsync(x => x.Id == setup.AdminId);
            admin.SetGlobalRole(GlobalRole.SuperAdmin);
            admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "password"), false, now, false);
            await seed.SaveChangesAsync();
        }
        var client = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var login = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(login, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        string loginName;
        await using (var names = new ApplicationDbContext(options))
            loginName = await names.Accounts.Where(x => x.Id == setup.AdminId).Select(x => x.LoginName).SingleAsync();
        using var authenticated = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = loginName, ["Input.Password"] = "password", ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, authenticated.StatusCode);
        return client;
    }

    // 42e Review section 10: an Archived event opens read-only (GET 200) and every decision is refused with no write (D17).
    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    [InlineData("Reverse")]
    [InlineData("Edit")]
    public async Task U8ArchivedEventOpensReadOnlyAndEveryDecisionIsRefusedWithoutAWrite(string handler)
    {
        var setup = await SeedAsync(3, true);
        Guid id;
        await using (var db = new ApplicationDbContext(options))
            id = (await Service(db).CreateAsync(Command(setup))).SubmissionId;
        await using var factory = U8Factory();
        using var client = await U8AdminClientAsync(factory, setup);
        var token = await U8TokenAsync(client, id);
        await using (var db = new ApplicationDbContext(options))
        {
            var ev = await db.Events.SingleAsync(x => x.Id == setup.EventId);
            ev.EndEvent(now.AddHours(1)); ev.FinalizeResults(now.AddHours(1)); ev.Archive(now.AddHours(2));
            await db.SaveChangesAsync();
        }
        var before = await U8StateAsync(id);
        using (var queue = await client.GetAsync($"/Admin/Review?eventId={setup.EventId}"))
            Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
        using (var details = await client.GetAsync($"/Admin/Review/Details/{id}"))
        {
            Assert.Equal(HttpStatusCode.OK, details.StatusCode);
            var html = await details.Content.ReadAsStringAsync();
            Assert.Contains("is archived, so review is read-only.", html, StringComparison.Ordinal);
        }
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Input.ExpectedVersion"] = before.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Input.Reason"] = "Archived events cannot be reviewed.",
            ["confirmed"] = "true"
        };
        using var response = await client.PostAsync($"/Admin/Review/Details/{id}?handler={handler}", new FormUrlEncodedContent(fields));
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(before, await U8StateAsync(id));
    }

    // L1 (report 97): a decision on a submission of a hidden event answers exactly like an unknown id, with no write.
    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    [InlineData("Reverse")]
    [InlineData("Edit")]
    public async Task U8DecisionOnAHiddenEventsSubmissionAnswersLikeAnUnknownId(string handler)
    {
        var setup = await SeedAsync(3, true);
        Guid id;
        await using (var db = new ApplicationDbContext(options))
            id = (await Service(db).CreateAsync(Command(setup))).SubmissionId;
        await using var factory = U8Factory();
        using var client = await U8AdminClientAsync(factory, setup);
        var token = await U8TokenAsync(client, id);
        await using (var db = new ApplicationDbContext(options))
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET hidden_at = {now}, hidden_by_account_id = {setup.AdminId}, hidden_reason = {"U8 fixture"} WHERE id = {setup.EventId}");
        var before = await U8StateAsync(id);
        async Task<string> PostAsync(Guid target, bool confirmed)
        {
            var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["Input.ExpectedVersion"] = before.Version.ToString(System.Globalization.CultureInfo.InvariantCulture), ["Input.Reason"] = "Hidden events cannot be reviewed." };
            if (confirmed) fields["confirmed"] = "true";
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/Admin/Review/Details/{target}?handler={handler}") { Content = new FormUrlEncodedContent(fields) };
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadAsStringAsync();
        }
        foreach (var confirmed in new[] { true, false })
        {
            var hidden = await PostAsync(id, confirmed);
            Assert.Contains("Submission not found.", hidden, StringComparison.Ordinal);
            Assert.Equal(await PostAsync(Guid.NewGuid(), confirmed), hidden);
        }
        Assert.Equal(before, await U8StateAsync(id));
    }

    private static async Task<string> U8TokenAsync(HttpClient client, Guid submissionId) =>
        Regex.Match(await client.GetStringAsync($"/Admin/Review/Details/{submissionId}"), "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
}
