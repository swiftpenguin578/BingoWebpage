using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

/// <summary>
/// T2 item 1 (brief 82): Catalogue server contracts for the in-place page. S10 deactivation impact
/// (T2-Q3 (a) hidden events, T2-Q3b (a) correction copies), the deactivate confirmation contract,
/// D7/D9 named-activity confirmation carried in the response (exact current set only),
/// C-CAT-2 / T2-Q4 (a) category and item-name checks, item 13 roll-group wording and C-CMP-2 JSON.
/// </summary>
public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Fact]
    public async Task T2DeactivationImpactNamesDraftBoardsAndCountsHiddenEventsForOrdinaryAdmins()
    {
        var seed = await ImpactFixtureAsync();
        await using var db = new ApplicationDbContext(options);

        // T2-Q3 (a): an ordinary Admin sees visible events by name and only a count for hidden ones.
        var admin = JsonCatalogue(db, seed.Owner.Id, superAdmin: false);
        var dropImpact = await ImpactAsync(admin, "drop", seed.FirstDrop.Id, seed.FirstDrop.Version);
        Assert.Equal(["A visible draft"], Names(dropImpact));
        Assert.Equal(1, dropImpact.GetProperty("hiddenCount").GetInt32());
        Assert.Equal($"/Admin/Events/Board/{seed.Visible.Id}", dropImpact.GetProperty("boards")[0].GetProperty("url").GetString());
        Assert.DoesNotContain("Z hidden draft", dropImpact.ToString(), StringComparison.Ordinal);

        // The Super Admin sees the hidden event by name, marked hidden, with its Board link.
        var owner = JsonCatalogue(db, seed.Owner.Id, superAdmin: true);
        var ownerImpact = await ImpactAsync(owner, "drop", seed.FirstDrop.Id, seed.FirstDrop.Version);
        Assert.Equal(["A visible draft", "Z hidden draft"], Names(ownerImpact));
        Assert.True(ownerImpact.GetProperty("boards")[1].GetProperty("hidden").GetBoolean());
        // T2-1 (a): the hidden event links to Overview's limited view, not its (Not Found) Board page.
        Assert.Matches("^/Admin/Events/Manage/[0-9a-f-]{36}\\?hidden=true$", ownerImpact.GetProperty("boards")[1].GetProperty("url").GetString()!);
        Assert.Equal(0, ownerImpact.GetProperty("hiddenCount").GetInt32());

        // T2-Q3b (a): a correction copy is listed only for an objective outside the active approval
        // (CreateApprovalSnapshotAsync checks only those); the approved objective's drop is not.
        var secondImpact = await ImpactAsync(admin, "drop", seed.SecondDrop.Id, seed.SecondDrop.Version);
        Assert.Equal(["B correction"], Names(secondImpact));
        Assert.True(secondImpact.GetProperty("boards")[0].GetProperty("correction").GetBoolean());

        // An activity covers its own eligibility and every drop of it; finalized events and
        // approved (Validated) boards are not listed.
        var bossImpact = await ImpactAsync(admin, "boss", seed.Boss.Id, seed.Boss.Version);
        Assert.Equal(["A visible draft", "B correction", "C activity only"], Names(bossImpact));
        Assert.Equal(1, bossImpact.GetProperty("hiddenCount").GetInt32());

        var stale = Assert.IsType<JsonResult>(await admin.OnGetDeactivationImpactAsync("drop", seed.FirstDrop.Id, seed.FirstDrop.Version + 7, default));
        Assert.Equal("stale", Json(stale).GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task T2DeactivationNeedsTheConfirmationAndReactivationDoesNot()
    {
        var seed = await ImpactFixtureAsync();
        foreach (var (type, id) in new[] { ("drop", seed.FirstDrop.Id), ("boss", seed.Boss.Id) })
        {
            long Version(ApplicationDbContext db) => type == "drop" ? db.SourceDrops.Single(x => x.Id == id).Version : db.BossActivities.Single(x => x.Id == id).Version;
            bool Active(ApplicationDbContext db) => type == "drop" ? db.SourceDrops.Single(x => x.Id == id).Active : db.BossActivities.Single(x => x.Id == id).Active;
            Task<IActionResult> Toggle(IndexModel page, long version, bool confirmed) => type == "drop"
                ? page.OnPostToggleDropAsync(id, version, default, confirmed)
                : page.OnPostToggleBossAsync(id, version, default, confirmed);

            int audits;
            await using (var db = new ApplicationDbContext(options))
            {
                audits = await db.AuditEntries.CountAsync();
                // S10: without the confirmation nothing changes; the response carries the impact.
                var unconfirmed = Json(Assert.IsType<JsonResult>(await Toggle(JsonCatalogue(db, seed.Owner.Id, false), Version(db), false)));
                Assert.Equal("confirm", unconfirmed.GetProperty("outcome").GetString());
                Assert.Contains("A visible draft", Names(unconfirmed.GetProperty("impact")));
            }
            await using (var db = new ApplicationDbContext(options))
            {
                Assert.True(Active(db));
                Assert.Equal(audits, await db.AuditEntries.CountAsync());
                var confirmed = Json(Assert.IsType<JsonResult>(await Toggle(JsonCatalogue(db, seed.Owner.Id, false), Version(db), true)));
                Assert.Equal("completed", confirmed.GetProperty("outcome").GetString());
                Assert.False(confirmed.GetProperty("active").GetBoolean());
            }
            await using (var db = new ApplicationDbContext(options))
            {
                Assert.False(Active(db));
                var reactivated = Json(Assert.IsType<JsonResult>(await Toggle(JsonCatalogue(db, seed.Owner.Id, false), Version(db), false)));
                Assert.Equal("completed", reactivated.GetProperty("outcome").GetString());
            }
            await using (var verify = new ApplicationDbContext(options)) Assert.True(Active(verify));
        }
    }

    [Fact]
    public async Task T2SharedItemConfirmationTravelsInTheResponseAndOnlyTheExactCurrentSetIsAccepted()
    {
        var (actor, _, item, drop) = await PriceFixtureAsync();
        var second = await AddSharedActivityAsync(item.Id, "t2-second");
        var third = await AddSharedActivityAsync(item.Id, "t2-third");
        Guid[][] wrongSets = [[], [second.Boss.Id], [second.Boss.Id, third.Boss.Id, Guid.NewGuid()]];
        foreach (var wrong in wrongSets)
        {
            await using var db = new ApplicationDbContext(options);
            var current = await db.SourceDrops.SingleAsync(x => x.Id == drop.Id);
            var shared = await db.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            var page = JsonCatalogue(db, actor.Id, true);
            var refused = Json(Assert.IsType<JsonResult>(await UpdateDropJsonAsync(page, current, shared, "Renamed shared item", wrong)));
            // D7 (a): named-activity confirmation in the response (brief 82 transport), never TempData.
            Assert.Equal("confirm-shared", refused.GetProperty("outcome").GetString());
            Assert.Equal(new[] { second.Boss.Id, third.Boss.Id }.Order(), refused.GetProperty("activities").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Order());
            Assert.Empty(page.TempData);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            Assert.Equal("Synthetic item", (await db.CatalogueItems.SingleAsync(x => x.Id == item.Id)).Name);
            var current = await db.SourceDrops.SingleAsync(x => x.Id == drop.Id);
            var shared = await db.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            var accepted = Json(Assert.IsType<JsonResult>(await UpdateDropJsonAsync(JsonCatalogue(db, actor.Id, true), current, shared, "Renamed shared item", [third.Boss.Id, second.Boss.Id])));
            Assert.Equal("completed", accepted.GetProperty("outcome").GetString());
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal("Renamed shared item", (await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id)).Name);
    }

    [Fact]
    public async Task T2CategoryAndItemNameLimitsAreCheckedOnTheServer()
    {
        var (actor, boss, item, drop) = await PriceFixtureAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            var page = JsonCatalogue(db, actor.Id, false);
            page.Boss = new IndexModel.BossInput { Name = "Off-list category", Category = "Raid", EfficientRate = 5, TeamSize = 1 };
            var added = Json(Assert.IsType<JsonResult>(await page.OnPostBossAsync(default)));
            // C-CAT-2 / T2-Q4 (a): the fixed category list on add.
            Assert.Equal("invalid", added.GetProperty("outcome").GetString());
            Assert.Equal("category", added.GetProperty("field").GetString());
            Assert.Equal(IndexModel.CategoryInvalid, added.GetProperty("message").GetString());
        }
        await using (var db = new ApplicationDbContext(options))
        {
            Assert.False(await db.BossActivities.AnyAsync(x => x.Name == "Off-list category"));
            var current = await db.BossActivities.SingleAsync(x => x.Id == boss.Id);
            var edited = Json(Assert.IsType<JsonResult>(await JsonCatalogue(db, actor.Id, false).OnPostUpdateBossAsync(current.Id, current.Version, "Renamed boss", "Raid", 10, null, null, default)));
            // ... and on edit.
            Assert.Equal("category", edited.GetProperty("field").GetString());
        }
        await using (var db = new ApplicationDbContext(options))
        {
            Assert.Equal("Synthetic boss", (await db.BossActivities.SingleAsync(x => x.Id == boss.Id)).Name);
            var current = await db.SourceDrops.SingleAsync(x => x.Id == drop.Id);
            var shared = await db.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            var tooLong = Json(Assert.IsType<JsonResult>(await UpdateDropJsonAsync(JsonCatalogue(db, actor.Id, true), current, shared, new string('x', 201), null)));
            // C-CAT-2 / T2-Q4 (a): the 200-character item name limit on drop edit.
            Assert.Equal("name", tooLong.GetProperty("field").GetString());
            Assert.Equal(IndexModel.ItemNameTooLong, tooLong.GetProperty("message").GetString());
            // A plain form post is refused the same way (status message, nothing saved).
            var form = CataloguePage(db, actor.Id, true);
            Assert.IsType<RedirectToPageResult>(await form.OnPostUpdateDropAsync(current.Id, current.Version, shared.Version, new string('y', 201), current.DisplayRate, current.DisplayRate,
                null, null, default, false, null, 0, 0, null, null, null, false, CancellationToken.None));
            Assert.Equal(IndexModel.ItemNameTooLong, form.TempData["StatusMessage"]?.ToString());
        }
        await using (var db = new ApplicationDbContext(options))
        {
            Assert.Equal("Synthetic item", (await db.CatalogueItems.SingleAsync(x => x.Id == item.Id)).Name);
            var current = await db.SourceDrops.SingleAsync(x => x.Id == drop.Id);
            var shared = await db.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            var exact = Json(Assert.IsType<JsonResult>(await UpdateDropJsonAsync(JsonCatalogue(db, actor.Id, true), current, shared, new string('z', 200), null)));
            Assert.Equal("completed", exact.GetProperty("outcome").GetString());
        }
    }

    [Fact]
    public async Task T2JsonOutcomesOverHttpKeepAuthorizationAndDetectALostSession()
    {
        var now = DateTimeOffset.UtcNow;
        var (owner, boss, item, drop) = await PriceFixtureAsync();
        var admin = Website($"t2-admin-{Guid.NewGuid():N}", now); admin.SetGlobalRole(GlobalRole.Admin); SetPassword(admin, now);
        var spare = new SourceDrop(Guid.NewGuid(), boss.Id, Guid.NewGuid(), "1/50", .02m, 5m, now);
        var spareItem = new CatalogueItem(spare.ItemId, "T2 spare", $"T2 SPARE {Guid.NewGuid():N}");
        await using (var setup = new ApplicationDbContext(options))
        {
            SetPassword(setup.Accounts.Single(x => x.Id == owner.Id), now);
            setup.AddRange(admin, spareItem, spare);
            await setup.SaveChangesAsync();
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var ownerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(adminClient, admin.LoginName);
        await LoginAsync(ownerClient, owner.LoginName);
        var adminToken = AntiforgeryToken(await adminClient.GetStringAsync("/Admin/Catalogue"));
        var ownerToken = AntiforgeryToken(await ownerClient.GetStringAsync("/Admin/Catalogue"));
        Dictionary<string, string> RollGroup(SourceDrop current, CatalogueItem shared, string group) => new()
        {
            ["recordId"] = current.Id.ToString(),
            ["expectedVersion"] = current.Version.ToString(CultureInfo.InvariantCulture),
            ["expectedItemVersion"] = shared.Version.ToString(CultureInfo.InvariantCulture),
            ["itemName"] = shared.Name,
            ["displayRate"] = current.DisplayRate,
            ["originalDisplayRate"] = current.DisplayRate,
            ["rollGroup"] = group
        };

        // Item 13: the ordinary Admin's roll-group change is refused with the decided wording.
        await using (var db = new ApplicationDbContext(options))
        {
            var refused = await PostJsonAsync(adminClient, "/Admin/Catalogue?handler=UpdateDrop", adminToken, RollGroup(await db.SourceDrops.SingleAsync(x => x.Id == drop.Id), await db.CatalogueItems.SingleAsync(x => x.Id == item.Id), "admin-change"));
            Assert.Equal("refused", refused.GetProperty("outcome").GetString());
            Assert.Equal(IndexModel.RollGroupSuperAdminOnly, refused.GetProperty("message").GetString());
        }
        await using (var db = new ApplicationDbContext(options))
        {
            Assert.Equal("synthetic", (await db.SourceDrops.SingleAsync(x => x.Id == drop.Id)).RollGroup);
            var allowed = await PostJsonAsync(ownerClient, "/Admin/Catalogue?handler=UpdateDrop", ownerToken, RollGroup(await db.SourceDrops.SingleAsync(x => x.Id == drop.Id), await db.CatalogueItems.SingleAsync(x => x.Id == item.Id), "owner-change"));
            Assert.Equal("completed", allowed.GetProperty("outcome").GetString());
        }
        await using (var db = new ApplicationDbContext(options))
            Assert.Equal("owner-change", (await db.SourceDrops.SingleAsync(x => x.Id == drop.Id)).RollGroup);

        // Delete stays Super Admin only; the script gets a refusal it can show (not a bare 403).
        var deleteForm = new Dictionary<string, string> { ["recordType"] = "drop", ["recordId"] = spare.Id.ToString(), ["expectedVersion"] = spare.Version.ToString(CultureInfo.InvariantCulture), ["confirmed"] = "true" };
        Assert.Equal("refused", (await PostJsonAsync(adminClient, "/Admin/Catalogue?handler=Delete", adminToken, deleteForm)).GetProperty("outcome").GetString());
        Assert.Equal("refused", (await GetJsonAsync(adminClient, $"/Admin/Catalogue?handler=DeletionImpact&recordType=drop&recordId={spare.Id}&expectedVersion={spare.Version}")).GetProperty("outcome").GetString());
        await using (var db = new ApplicationDbContext(options)) Assert.True(await db.SourceDrops.AnyAsync(x => x.Id == spare.Id));
        Assert.Equal("deletable", (await GetJsonAsync(ownerClient, $"/Admin/Catalogue?handler=DeletionImpact&recordType=drop&recordId={spare.Id}&expectedVersion={spare.Version}")).GetProperty("outcome").GetString());
        Assert.Equal("completed", (await PostJsonAsync(ownerClient, "/Admin/Catalogue?handler=Delete", ownerToken, deleteForm)).GetProperty("outcome").GetString());
        await using (var db = new ApplicationDbContext(options)) Assert.False(await db.SourceDrops.AnyAsync(x => x.Id == spare.Id));

        // S10 read over HTTP for any Admin.
        Assert.Equal("impact", (await GetJsonAsync(adminClient, $"/Admin/Catalogue?handler=DeactivationImpact&recordType=boss&recordId={boss.Id}&expectedVersion={boss.Version}")).GetProperty("outcome").GetString());

        // C-CMP-2: without a session every fetch endpoint answers with the sign-in redirect, and nothing changes.
        foreach (var url in new[] { $"/Admin/Catalogue?handler=DeactivationImpact&recordType=boss&recordId={boss.Id}&expectedVersion={boss.Version}", $"/Admin/Catalogue?handler=DeletionImpact&recordType=boss&recordId={boss.Id}&expectedVersion={boss.Version}" })
        {
            using var lost = await anonymous.SendAsync(JsonRequest(HttpMethod.Get, url, null, null));
            Assert.Equal(HttpStatusCode.Redirect, lost.StatusCode);
            Assert.Contains("/Account/Login", lost.Headers.Location?.OriginalString, StringComparison.Ordinal);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var current = await db.BossActivities.SingleAsync(x => x.Id == boss.Id);
            using var lostPost = await anonymous.SendAsync(JsonRequest(HttpMethod.Post, "/Admin/Catalogue?handler=ToggleBoss", ownerToken,
                new() { ["recordId"] = boss.Id.ToString(), ["expectedVersion"] = current.Version.ToString(CultureInfo.InvariantCulture), ["confirmed"] = "true" }));
            Assert.Equal(HttpStatusCode.Redirect, lostPost.StatusCode);
            Assert.Contains("/Account/Login", lostPost.Headers.Location?.OriginalString, StringComparison.Ordinal);
        }
        await using (var verify = new ApplicationDbContext(options)) Assert.True((await verify.BossActivities.SingleAsync(x => x.Id == boss.Id)).Active);
    }

    // T2 item 2 (planner default: reference query names, ids not slugs, query string only).
    [Fact]
    public async Task T2DirectoryUrlStateBindsFromTheQueryStringAndOpensTheDrawer()
    {
        var now = DateTimeOffset.UtcNow;
        var (owner, boss, _, drop) = await PriceFixtureAsync();
        await using (var setup = new ApplicationDbContext(options)) { SetPassword(setup.Accounts.Single(x => x.Id == owner.Id), now); await setup.SaveChangesAsync(); }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, owner.LoginName);
        static string Canonical(string html) => WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Match(html, "data-directory-canonical=\"([^\"]*)\"").Groups[1].Value);

        var plain = await client.GetStringAsync("/Admin/Catalogue");
        Assert.Equal("/Admin/Catalogue", Canonical(plain));
        Assert.Contains("data-page-family=\"catalogue\"", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("data-catalogue-drawer ", plain, StringComparison.Ordinal);

        var full = await client.GetStringAsync($"/Admin/Catalogue?q=Synthetic&cat=Boss&status=inactive&activity={boss.Id}&drop={drop.Id}&page=junk&handler=");
        Assert.Equal($"/Admin/Catalogue?q=Synthetic&cat=Boss&status=inactive&activity={boss.Id}&drop={drop.Id}", Canonical(full));
        Assert.Contains($"data-catalogue-drawer data-activity-id=\"{boss.Id}\"", full, StringComparison.Ordinal);
        Assert.Contains($"data-open-drop=\"{drop.Id}\"", full, StringComparison.Ordinal);

        // Invalid parts are dropped; retired names (bossId, dropId, addBoss) are not mapped.
        Assert.Equal("/Admin/Catalogue", Canonical(await client.GetStringAsync($"/Admin/Catalogue?cat=Raid&status=gone&activity=nope&drop={drop.Id}&bossId={boss.Id}&addBoss=true")));
        Assert.Equal("/Admin/Catalogue?new=1", Canonical(await client.GetStringAsync("/Admin/Catalogue?new=1")));
        var missing = WebUtility.HtmlDecode(await client.GetStringAsync($"/Admin/Catalogue?activity={Guid.NewGuid()}"));
        Assert.Contains("data-missing=\"true\"", missing, StringComparison.Ordinal);
        Assert.Contains("This activity isn’t available", missing, StringComparison.Ordinal);
        // The repair link (EventLifecycleService) and Board editor link stay valid.
        using var index = await client.GetAsync("/Admin/Catalogue/Index");
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
    }

    private sealed record ImpactSeed(Account Owner, BossActivity Boss, SourceDrop FirstDrop, SourceDrop SecondDrop, BingoEvent Visible);

    private async Task<ImpactSeed> ImpactFixtureAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var owner = Website($"t2-impact-{Guid.NewGuid():N}", now); owner.SetGlobalRole(GlobalRole.SuperAdmin);
        var boss = new BossActivity(Guid.NewGuid(), "T2 impact boss", $"t2-impact-{Guid.NewGuid():N}", "Boss", 10m, now);
        var firstItem = new CatalogueItem(Guid.NewGuid(), "T2 first item", $"T2 FIRST {Guid.NewGuid():N}");
        var secondItem = new CatalogueItem(Guid.NewGuid(), "T2 second item", $"T2 SECOND {Guid.NewGuid():N}");
        var firstDrop = new SourceDrop(Guid.NewGuid(), boss.Id, firstItem.Id, "1/10", .1m, 1m, now);
        var secondDrop = new SourceDrop(Guid.NewGuid(), boss.Id, secondItem.Id, "1/20", .05m, 2m, now);
        var entities = new List<object> { owner, boss, firstItem, secondItem, firstDrop, secondDrop };
        var approvals = new List<(Guid Board, Guid Approval)>();
        BingoEvent Event(string name)
        {
            var value = new BingoEvent(Guid.NewGuid(), name, $"t2-{Guid.NewGuid():N}", "UTC", owner.Id, now, PlacementRule.LegacyScoreTimeThenEhb);
            entities.Add(value);
            return value;
        }
        (Board Board, BoardRequirementSnapshot Requirement, BoardTile Tile, TileTemplate Template) Board(BingoEvent bingoEvent, SourceDrop? drop, bool bossOnly = false, int column = 0, Board? existing = null)
        {
            var board = existing ?? new Board(Guid.NewGuid(), bingoEvent.Id, bingoEvent.Name, 1, 2);
            if (existing is null) entities.Add(board);
            var template = new TileTemplate(Guid.NewGuid(), $"T2 tile {Guid.NewGuid():N}", string.Empty, ObjectiveType.DropRequirements, string.Empty, null);
            var tile = new BoardTile(Guid.NewGuid(), board.Id, template.Id, 0, column, "T2 tile", string.Empty, string.Empty, 1m);
            var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 1, true, false, "T2 objective", false);
            entities.AddRange([template, tile, requirement]);
            if (bossOnly) entities.Add(new BoardRequirementBossSnapshot(Guid.NewGuid(), requirement.Id, boss.Id, boss.Name, boss.EfficientCompletionsPerHour));
            else
            {
                entities.Add(new BoardRequirementBossSnapshot(Guid.NewGuid(), requirement.Id, boss.Id, boss.Name, boss.EfficientCompletionsPerHour));
                entities.Add(new BoardRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, drop!.Id, drop.ItemId, boss.Name, "T2 item", drop.DisplayRate, drop.NumericProbability, null, null));
            }
            return (board, requirement, tile, template);
        }

        var visible = Event("A visible draft"); Board(visible, firstDrop);
        var hidden = Event("Z hidden draft"); Board(hidden, firstDrop);
        var finalized = Event("Y finalized draft"); Board(finalized, firstDrop);
        var validated = Event("X validated board"); var validatedBoard = Board(validated, firstDrop).Board;
        var correction = Event("B correction");
        var approvedObjective = Board(correction, firstDrop);
        var newObjective = Board(correction, secondDrop, column: 1, existing: approvedObjective.Board);
        var activityOnly = Event("C activity only"); Board(activityOnly, null, bossOnly: true);
        var approval = new BoardApprovalSnapshot(Guid.NewGuid(), approvedObjective.Board.Id, 1, now, owner.Id, null, "B correction", 1, 2, 1m, 1, 1, BoardState.Published);
        var approvalTile = new BoardApprovalTileSnapshot(Guid.NewGuid(), approval.Id, approvedObjective.Tile.Id, approvedObjective.Template.Id, 0, 0, "T2 tile", string.Empty, string.Empty, 1m, null);
        var approvalRequirement = new BoardApprovalRequirementSnapshot(Guid.NewGuid(), approvalTile.Id, approvedObjective.Requirement.Id, 1, 1, true, false, 1, "T2 objective", false);
        entities.AddRange([approval, approvalTile, approvalRequirement]);

        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(entities);
            await setup.SaveChangesAsync();
            await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET hidden_at = {now}, hidden_by_account_id = {owner.Id}, hidden_reason = {"T2 fixture"} WHERE id = {hidden.Id}");
            await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET state = {nameof(EventState.Finalized)} WHERE id = {finalized.Id}");
            await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE boards SET state = {nameof(BoardState.Validated)} WHERE id = {validatedBoard.Id}");
            await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE boards SET state = {nameof(BoardState.Published)}, published_at = {now}, published_correction_in_progress = TRUE, active_approval_snapshot_id = {approval.Id} WHERE id = {approvedObjective.Board.Id}");
        }
        return new ImpactSeed(owner, boss, firstDrop, secondDrop, visible);
    }

    private static IndexModel JsonCatalogue(ApplicationDbContext db, Guid accountId, bool superAdmin)
    {
        var page = CataloguePage(db, accountId, superAdmin);
        page.HttpContext.Request.Headers.Accept = "application/json";
        page.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";
        return page;
    }

    private static Task<IActionResult> UpdateDropJsonAsync(IndexModel page, SourceDrop drop, CatalogueItem item, string name, Guid[]? confirmation) =>
        page.OnPostUpdateDropAsync(drop.Id, drop.Version, item.Version, name, drop.DisplayRate, drop.DisplayRate,
            null, null, default, false, null, 0, 0, null, null, null, false, CancellationToken.None, confirmation);

    private static async Task<JsonElement> ImpactAsync(IndexModel page, string type, Guid id, long version)
    {
        var json = Json(Assert.IsType<JsonResult>(await page.OnGetDeactivationImpactAsync(type, id, version, default)));
        Assert.Equal("impact", json.GetProperty("outcome").GetString());
        return json.GetProperty("impact");
    }

    private static string[] Names(JsonElement impact) => impact.GetProperty("boards").EnumerateArray().Select(x => x.GetProperty("eventName").GetString()!).ToArray();
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private static JsonElement Json(JsonResult result) => JsonDocument.Parse(JsonSerializer.Serialize(result.Value, WebJson)).RootElement.Clone();

    private static HttpRequestMessage JsonRequest(HttpMethod method, string url, string? token, Dictionary<string, string>? form)
    {
        var request = new HttpRequestMessage(method, url);
        if (form is not null) request.Content = new FormUrlEncodedContent(form);
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        if (token is not null) request.Headers.Add("RequestVerificationToken", token);
        return request;
    }
    private static async Task<JsonElement> PostJsonAsync(HttpClient client, string url, string token, Dictionary<string, string> form)
    {
        using var response = await client.SendAsync(JsonRequest(HttpMethod.Post, url, token, form));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        using var response = await client.SendAsync(JsonRequest(HttpMethod.Get, url, null, null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
}
