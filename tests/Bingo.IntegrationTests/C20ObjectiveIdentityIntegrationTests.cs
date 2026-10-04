using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed partial class C20ObjectiveIdentityIntegrationTests(C20Database fixture) : IClassFixture<C20Database>
{
    [Fact]
    public async Task PublishedAutomaticDescriptionIsPreservedInCaptainDrawerProjection()
    {
        var f = await SeedAsync(automaticDescription: true, captain: true);
        using var admin = await ClientAsync(f.Admin);
        using var captain = await ClientAsync(f.Owner);
        await StartCorrectionAsync(admin, f);
        await PublishCorrectionAsync(admin, f);

        var expectedDescription = $"Collect 5 {f.Drop.ItemName}";
        await using (var db = fixture.Db())
        {
            var publication = await db.PublishedObjectivesAsync(f.Event.Id);
            var publishedTile = Assert.Single(publication!.Tiles);
            Assert.Equal(expectedDescription, publishedTile.DescriptionSnapshot);
            Assert.True(publishedTile.DescriptionIsAutomatic);
        }

        var path = $"/Captain/Submit/{f.Tile.Id}?handler=Drawer&eventId={f.Event.Id}&teamId={f.Team.Id}";
        using var response = await captain.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedDescription, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WordingCorrectionPreservesApprovedAndPendingIdentityPublicReadsAndReplacementHistory()
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        using var player = await ClientAsync(f.Owner);
        var approved = await SubmitAsync(player, f);
        var pending = await SubmitAsync(player, f);
        await ReviewAsync(admin, approved, "Approve");
        await using var db = fixture.Db();
        var originalHistory = await db.ReviewActions.AsNoTracking().Where(x => x.SubmissionId == approved || x.SubmissionId == pending).Select(x => new { x.Id, x.BeforeSnapshot, x.AfterSnapshot }).ToListAsync();
        var before = await new PublicBoardService(db, TimeProvider.System).GetTileAsync(f.Event.Slug, f.Team.Slug, f.Tile.Id);
        Assert.Equal(1, before!.Approved);
        await StartCorrectionAsync(admin, f);
        await EditAsync(admin, f, name: "Corrected title", description: "Corrected description");
        Assert.Equal(f.Requirement.Id, (await db.BoardRequirementSnapshots.AsNoTracking().SingleAsync(x => x.BoardTileId == f.Tile.Id)).Id);
        Assert.Equal(f.Drop.Id, (await db.BoardRequirementDropSnapshots.AsNoTracking().SingleAsync(x => x.RequirementId == f.Requirement.Id)).Id);
        await AssertOrdinaryReadsAsync(player, f, pending, f.Tile.NameSnapshot);
        var during = await new PublicBoardService(db, TimeProvider.System).GetTileAsync(f.Event.Slug, f.Team.Slug, f.Tile.Id);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(during));
        var arrived = await SubmitAsync(player, f);
        await PublishCorrectionAsync(admin, f);
        var board = await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id);
        Assert.False(board.PublishedCorrectionInProgress);
        Assert.NotEqual(f.ApprovalId, board.ActiveApprovalSnapshotId);
        Assert.Equal(2, await db.BoardApprovalSnapshots.CountAsync(x => x.BoardId == f.Board.Id));
        var after = await new PublicBoardService(db, TimeProvider.System).GetTileAsync(f.Event.Slug, f.Team.Slug, f.Tile.Id);
        Assert.Equal("Corrected title", after!.TileName);
        Assert.Equal(before.Approved, after.Approved);
        Assert.Equal(before.Target, after.Target);
        Assert.Equal(originalHistory, await db.ReviewActions.AsNoTracking().Where(x => x.SubmissionId == approved || x.SubmissionId == pending).Select(x => new { x.Id, x.BeforeSnapshot, x.AfterSnapshot }).ToListAsync());
        Assert.All(await db.Submissions.AsNoTracking().Where(x => x.EventId == f.Event.Id).ToListAsync(), x => { Assert.Equal(f.Requirement.Id, x.RequirementId); Assert.Equal(f.Drop.Id, x.DropSnapshotId); });
        await AssertOrdinaryReadsAsync(player, f, arrived, "Corrected title");
        await ReviewAsync(admin, pending, "Approve");
        Assert.Equal(2, (await new PublicBoardService(db, TimeProvider.System).GetTileAsync(f.Event.Slug, f.Team.Slug, f.Tile.Id))!.Approved);
        var oldTitle = await db.BoardApprovalTileSnapshots.SingleAsync(x => x.ApprovalSnapshotId == f.ApprovalId);
        Assert.Equal(f.Tile.NameSnapshot, oldTitle.Name);
    }

    [Theory]
    [InlineData(SubmissionStatus.Pending)]
    [InlineData(SubmissionStatus.Approved)]
    [InlineData(SubmissionStatus.Rejected)]
    [InlineData(SubmissionStatus.Reversed)]
    [InlineData(SubmissionStatus.Withdrawn)]
    public async Task EveryRetainedEvidenceStateLocksRequirementsWeightsAndRemoval(SubmissionStatus state)
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        using var player = await ClientAsync(f.Owner);
        var id = await SubmitAsync(player, f);
        await using var db = fixture.Db();
        var service = new SubmissionService(db, fixture.Storage, TimeProvider.System);
        if (state is SubmissionStatus.Approved or SubmissionStatus.Reversed) await service.ApproveAsync(id, f.Admin.Id);
        if (state == SubmissionStatus.Reversed) await service.ReverseAsync(id, f.Admin.Id, "Controlled test reversal");
        if (state == SubmissionStatus.Rejected) await service.RejectAsync(id, f.Admin.Id, "Controlled test rejection");
        if (state == SubmissionStatus.Withdrawn) await service.WithdrawAsync(id, f.Owner.Id);
        await StartCorrectionAsync(admin, f);
        var before = await IntegrityAsync(f);
        await EditAsync(admin, f, target: 7);
        Assert.Equal(before, await IntegrityAsync(f));
        await EditAsync(admin, f, weight: 2);
        Assert.Equal(before, await IntegrityAsync(f));
        await EditAsync(admin, f, duplicate: false);
        Assert.Equal(before, await IntegrityAsync(f));
        await PostBoardAsync(admin, f, "Remove", new() { ["tileId"] = f.Tile.Id.ToString() });
        Assert.Equal(before, await IntegrityAsync(f));
        await EditAsync(admin, f, name: "Allowed wording");
        Assert.Equal("Allowed wording", (await db.BoardTiles.AsNoTracking().SingleAsync(x => x.Id == f.Tile.Id)).NameSnapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoEvidencePrivateReplacementOrRemovalStillAcceptsPublishedTargetThenBlocksPublication(bool removeTile)
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        using var player = await ClientAsync(f.Owner);
        await StartCorrectionAsync(admin, f);
        if (removeTile)
        {
            await PostBoardAsync(admin, f, "Remove", new() { ["tileId"] = f.Tile.Id.ToString() });
            await CreateReplacementAsync(admin, f);
        }
        else await EditAsync(admin, f, target: 7);
        await using var db = fixture.Db();
        Assert.False(await db.BoardRequirementSnapshots.AnyAsync(x => x.Id == f.Requirement.Id));
        Assert.True(await db.BoardRequirementDropSnapshots.AnyAsync(x => x.Id == f.Drop.Id));
        var ordinary = await db.PublishedObjectivesAsync(f.Event.Id);
        Assert.Equal(f.Drop.Id, Assert.Single(ordinary!.Drops).Id);
        var arrived = await SubmitAsync(player, f);
        await AssertOrdinaryReadsAsync(player, f, arrived, f.Tile.NameSnapshot);
        await ReviewAsync(admin, arrived, "Approve");
        var before = await IntegrityAsync(f);
        await PublishCorrectionAsync(admin, f);
        Assert.Equal(before, await IntegrityAsync(f));
        Assert.Equal(1, (await new PublicBoardService(db, TimeProvider.System).GetTileAsync(f.Event.Slug, f.Team.Slug, f.Tile.Id))!.Approved);
    }

    [Fact]
    public async Task UnreferencedSubstantiveReplacementPublishesWithFreshIdentityAndRejectsOldTargets()
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        using var player = await ClientAsync(f.Owner);
        await StartCorrectionAsync(admin, f);
        await EditAsync(admin, f, target: 7);
        await PublishCorrectionAsync(admin, f);
        await using var db = fixture.Db();
        var publication = (await db.PublishedObjectivesAsync(f.Event.Id))!;
        var requirement = Assert.Single(publication.Requirements);
        Assert.NotEqual(f.Requirement.Id, requirement.Id);
        Assert.NotEqual(f.Drop.Id, Assert.Single(publication.Drops).Id);
        Assert.Equal(7, requirement.TargetContribution);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SubmissionService(db, fixture.Storage, TimeProvider.System).CreateAsync(Command(f)));
        Assert.False(await db.Submissions.AnyAsync(x => x.EventId == f.Event.Id));
        var replacement = f with { Requirement = requirement, Drop = Assert.Single(publication.Drops) };
        await SubmitAsync(player, replacement);
    }

    [Fact]
    public async Task StaleForgedUnauthorizedAndInvalidRequestsLeaveNoPartialState()
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        using var player = await ClientAsync(f.Owner);
        await StartCorrectionAsync(admin, f);
        var afterCorrectionStart = await LeaseStateAsync(f);
        var version = await VersionAsync(f);
        await EditAsync(admin, f, name: "First correction");
        var afterCommittedEdit = await LeaseStateAsync(f);
        Assert.True(afterCommittedEdit.Version > afterCorrectionStart.Version);
        Assert.True(afterCommittedEdit.LeaseExpiresAt > afterCorrectionStart.LeaseExpiresAt);
        Assert.True(afterCommittedEdit.BoardAuditCount > afterCorrectionStart.BoardAuditCount);
        var before = await IntegrityAsync(f);
        using (var view = await admin.GetAsync($"/Admin/Events/Board/{f.Event.Id}"))
        {
            Assert.Equal(HttpStatusCode.OK, view.StatusCode);
            Assert.Equal(before, await IntegrityAsync(f));
        }
        await EditAsync(admin, f, name: "Stale", version: version);
        Assert.Equal(before, await IntegrityAsync(f));
        await EditAsync(admin, f, requirementId: Guid.NewGuid());
        Assert.Equal(before, await IntegrityAsync(f));
        await EditAsync(admin, f, target: 0);
        Assert.Equal(before, await IntegrityAsync(f));
        await PostBoardAsync(admin, f, "Approve", new() { ["confirmed"] = "false" });
        Assert.Equal(before, await IntegrityAsync(f));
        var unauthorizedFields = Fields(f, "Unauthorized", null, 7, 1, true, null);
        unauthorizedFields["__RequestVerificationToken"] = Token(await player.GetStringAsync($"/Submissions?eventId={f.Event.Id}&teamId={f.Team.Id}"));
        unauthorizedFields["BoardVersion"] = (await VersionAsync(f)).ToString(CultureInfo.InvariantCulture);
        using var denied = await player.PostAsync($"/Admin/Events/Board/{f.Event.Id}?handler=EditTile", new FormUrlEncodedContent(unauthorizedFields));
        Assert.True(denied.StatusCode == HttpStatusCode.Forbidden || denied.StatusCode == HttpStatusCode.Redirect && denied.Headers.Location!.OriginalString.Contains("AccessDenied", StringComparison.Ordinal));
        Assert.Equal(before, await IntegrityAsync(f));
    }

    [Fact]
    public async Task ExpiredPublishedCorrectionLeaseCanBeExplicitlyAcquiredBeforeEditing()
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        await StartCorrectionAsync(admin, f);

        await using (var db = fixture.Db())
        {
            var board = await db.Boards.SingleAsync(x => x.Id == f.Board.Id);
            db.Entry(board).Property(x => x.EditorAccountId).CurrentValue = f.Owner.Id;
            db.Entry(board).Property(x => x.EditorLeaseExpiresAt).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
        }

        var beforeView = await LeaseStateAsync(f);
        using (var view = await admin.GetAsync($"/Admin/Events/Board/{f.Event.Id}"))
        {
            Assert.Equal(HttpStatusCode.OK, view.StatusCode);
            var html = await view.Content.ReadAsStringAsync();
            Assert.Contains("handler=AcquireEditing", html, StringComparison.Ordinal);
            Assert.Contains("Acquire editing control", html, StringComparison.Ordinal);
        }
        Assert.Equal(beforeView, await LeaseStateAsync(f));

        await PostBoardAsync(admin, f, "AcquireEditing", new());
        var afterAcquire = await LeaseStateAsync(f);
        Assert.True(afterAcquire.LeaseExpiresAt > DateTimeOffset.UtcNow);
        await using (var db = fixture.Db())
        {
            var board = await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id);
            Assert.Equal(f.Admin.Id, board.EditorAccountId);
            Assert.True(await db.AuditEntries.AnyAsync(x => x.TargetId == f.Board.Id.ToString() && x.Action == "board.editing_acquired"));
        }

        await EditAsync(admin, f, name: "Acquired published correction");
        var afterEdit = await LeaseStateAsync(f);
        Assert.True(afterEdit.Version > afterAcquire.Version);
        Assert.True(afterEdit.LeaseExpiresAt > afterAcquire.LeaseExpiresAt);
        await using (var db = fixture.Db())
        {
            var board = await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id);
            Assert.True(board.PublishedCorrectionInProgress);
            Assert.Equal("Acquired published correction", (await db.BoardTiles.AsNoTracking().SingleAsync(x => x.Id == f.Tile.Id)).NameSnapshot);
        }
    }

    private async Task<Fixture> SeedAsync(bool manual = false, bool automaticDescription = false, bool captain = false)
    {
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..12];
        Account Website(string label, bool admin = false)
        {
            var account = Account.CreateWebsite(Guid.NewGuid(), label + suffix, (label + suffix).ToUpperInvariant(), now.AddDays(-3));
            account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "password"), false, now, false);
            if (admin) account.SetGlobalRole(GlobalRole.Admin);
            return account;
        }
        var admin = Website("admin", true); var owner = Website("owner");
        var ev = new BingoEvent(Guid.NewGuid(), "C20 event " + suffix, "c20-" + suffix, "UTC", admin.Id, now.AddDays(-3), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        ev.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddHours(-1), now.AddHours(2), 20);
        ev.OpenSignups(now.AddDays(-2)); ev.CloseSignups(now.AddDays(-1));
        ev.SetDraftRosterPublication(true); ev.SetBoardPublication(true, now.AddHours(-2)); ev.StartEvent(now.AddHours(-1));
        var team = new Team(Guid.NewGuid(), ev.Id, "C20 team", "c20-team", TeamFormationType.Drafted, null, true); team.Finalize(now.AddHours(-2));
        var participant = new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, 1, now.AddDays(-1), SignupSource.Website); participant.AssignOwner(owner);
        var character = new OsrsCharacter(Guid.NewGuid(), "Player " + suffix, "PLAYER " + suffix.ToUpperInvariant(), now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), ev.Id, participant.Id, character.Id, 0, now.AddDays(-1), null, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, captain ? TeamMembershipRole.Captain : TeamMembershipRole.Participant, now.AddDays(-1), null, "fixture");
        var boss = new BossActivity(Guid.NewGuid(), "C20 boss " + suffix, "boss-" + suffix, "Boss", 10m, now);
        var item = new CatalogueItem(Guid.NewGuid(), "C20 drop " + suffix, "DROP " + suffix.ToUpperInvariant());
        item.SetPrice(0, CataloguePriceSource.Manual, now);
        var source = new SourceDrop(Guid.NewGuid(), boss.Id, item.Id, "1/10", .1m, 1m, now);
        var description = automaticDescription ? string.Empty : "Original description";
        var template = new TileTemplate(Guid.NewGuid(), "Original title", description, manual ? ObjectiveType.Manual : ObjectiveType.DropRequirements, "", manual ? 5m : null, descriptionIsAutomatic: automaticDescription);
        var templateRequirement = new TileTemplateRequirement(Guid.NewGuid(), template.Id, 1, 5, true, false, manual ? "Complete five runs" : "Collect 5 eligible drops", manual);
        var board = new Board(Guid.NewGuid(), ev.Id, "C20 board", 1, 1); board.SetTotalEhb(5m);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, template.Id, 0, 0, template.Name, template.Description, "", 5m, descriptionIsAutomatic: automaticDescription);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 5, true, false, templateRequirement.Description, manual);
        var drop = new BoardRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, source.Id, item.Id, boss.Name, item.Name, source.DisplayRate, source.NumericProbability, null, 1m);
        var draft = new DraftSession(Guid.NewGuid(), ev.Id, 1);
        draft.FinalizeDirect(now.AddHours(-2));
        var publication = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, now.AddHours(-2), admin.Id, DraftPublicationMethod.DirectRoster);
        var publishedRoster = new DraftPublicationRoster(Guid.NewGuid(), publication.Id, team.Id, participant.Id,
            membership.Role, null, character.DisplayName);
        await using var db = fixture.Db();
        db.AddRange(admin, owner, ev, team, participant, character, assignment, membership, draft, publication, publishedRoster,
            boss, item, source, template, templateRequirement, board, tile, requirement);
        if (!manual) db.AddRange(drop, new TemplateRequirementBoss(Guid.NewGuid(), templateRequirement.Id, boss.Id), new TemplateRequirementDrop(Guid.NewGuid(), templateRequirement.Id, source.Id, null), new BoardRequirementBossSnapshot(Guid.NewGuid(), requirement.Id, boss.Id, boss.Name, 10m));
        await BoardApprovalFixture.PublishAsync(db, board, now.AddHours(-2), [tile], [requirement], manual ? [] : [drop]);
        var approvalReq = await db.BoardApprovalRequirementSnapshots.SingleAsync(x => x.BoardRequirementSnapshotId == requirement.Id);
        if (!manual) db.BoardApprovalRequirementBossSnapshots.Add(new(Guid.NewGuid(), approvalReq.Id, boss.Id, boss.Name, 10m, boss.Version));
        await db.SaveChangesAsync();
        return new(admin, owner, ev, team, participant, board, tile, requirement, drop, boss, board.ActiveApprovalSnapshotId!.Value);
    }

    private async Task<HttpClient> ClientAsync(Account account)
    {
        var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var html = await client.GetStringAsync("/Account/Login");
        using var result = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = account.LoginName, ["Input.Password"] = "password", ["__RequestVerificationToken"] = Token(html) }));
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        return client;
    }

    private async Task PostBoardAsync(HttpClient client, Fixture f, string handler, Dictionary<string, string> fields, long? version = null)
    {
        var path = $"/Admin/Events/Board/{f.Event.Id}";
        var page = await client.GetStringAsync(path);
        fields["__RequestVerificationToken"] = Token(page);
        if (handler == "Approve") fields["ApprovalCatalogueFingerprint"] = Regex.Match(page, "name=\"ApprovalCatalogueFingerprint\" value=\"([^\"]*)\"").Groups[1].Value;
        fields["BoardVersion"] = (version ?? await VersionAsync(f)).ToString(CultureInfo.InvariantCulture);
        using var response = await client.PostAsync(path + "?handler=" + handler, new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private Task StartCorrectionAsync(HttpClient client, Fixture f) => PostBoardAsync(client, f, "CorrectPublished", new() { ["confirmed"] = "true", ["reason"] = "Correct wording without changing objective meaning" });
    private Task PublishCorrectionAsync(HttpClient client, Fixture f) => PostBoardAsync(client, f, "Approve", new() { ["confirmed"] = "true" });
    private Task EditAsync(HttpClient client, Fixture f, string? name = null, string? description = null, int target = 5, int weight = 1, bool duplicate = true, long? version = null, Guid? requirementId = null)
        => PostBoardAsync(client, f, "EditTile", Fields(f, name, description, target, weight, duplicate, requirementId), version);
    private Task CreateReplacementAsync(HttpClient client, Fixture f) => PostBoardAsync(client, f, "CreateTile", Fields(f, "Replacement", null, 7, 1, true, null));
    private static Dictionary<string, string> Fields(Fixture f, string? name, string? description, int target, int weight, bool duplicate, Guid? requirementId)
    {
        var fields = new Dictionary<string, string>
        {
            ["TileDraft.TileId"] = f.Tile.Id.ToString(),
            ["TileDraft.Position"] = "0",
            ["TileDraft.Name"] = name ?? f.Tile.NameSnapshot,
            ["TileDraft.Description"] = description ?? f.Tile.DescriptionSnapshot,
            ["TileDraft.Requirements[0].RequirementId"] = (requirementId ?? f.Requirement.Id).ToString(),
            ["TileDraft.Requirements[0].Kind"] = "drops",
            ["TileDraft.Requirements[0].Target"] = target.ToString(CultureInfo.InvariantCulture),
            ["TileDraft.Requirements[0].DuplicatesAllowed"] = duplicate.ToString(),
            ["TileDraft.Requirements[0].BossIds[0]"] = f.Boss.Id.ToString(),
            ["TileDraft.Requirements[0].DropIds[0]"] = f.Drop.SourceDropId.ToString(),
            [$"TileDraft.Requirements[0].DropWeights[{f.Drop.SourceDropId}]"] = weight.ToString(CultureInfo.InvariantCulture)
        };
        if (f.Requirement.ManualObjective)
        {
            fields["TileDraft.ManualEhb"] = "5";
            fields["TileDraft.Requirements[0].Kind"] = "challenge";
            fields["TileDraft.Requirements[0].Description"] = f.Requirement.Description;
            foreach (var key in fields.Keys.Where(x => x.Contains("BossIds", StringComparison.Ordinal) || x.Contains("DropIds", StringComparison.Ordinal) || x.Contains("DropWeights", StringComparison.Ordinal)).ToList()) fields.Remove(key);
        }
        return fields;
    }
    private async Task<long> VersionAsync(Fixture f) { await using var db = fixture.Db(); return await db.Boards.Where(x => x.Id == f.Board.Id).Select(x => x.Version).SingleAsync(); }
    private async Task<LeaseState> LeaseStateAsync(Fixture f)
    {
        await using var db = fixture.Db();
        var board = await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id);
        return new(board.Version, board.EditorLeaseExpiresAt, board.EditControlVersion,
            await db.AuditEntries.CountAsync(x => x.TargetId == f.Board.Id.ToString()));
    }
    private static async Task<Guid> SubmitAsync(HttpClient player, Fixture f)
    {
        var path = $"/Captain/Submit/{f.Tile.Id}?handler=Drawer&eventId={f.Event.Id}&teamId={f.Team.Id}";
        var html = await player.GetStringAsync(path);
        Assert.Contains(f.Requirement.ManualObjective ? f.Requirement.Description : f.Drop.Id.ToString(), html);
        using var content = new MultipartFormDataContent();
        foreach (var pair in new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(html), ["Input.TileId"] = f.Tile.Id.ToString(), ["Input.RequirementId"] = f.Requirement.Id.ToString(), ["Input.DropSnapshotId"] = f.Requirement.ManualObjective ? "" : f.Drop.Id.ToString(), ["Input.CreditedParticipantId"] = f.Participant.Id.ToString(), ["Input.ClaimedWeight"] = "1" }) content.Add(new StringContent(pair.Value), pair.Key);
        content.Add(new ByteArrayContent([1, 2, 3]), "Input.Evidence", "controlled.png");
        using var response = await player.PostAsync(path, content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.StartsWith('{'), body);
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        return json.RootElement.GetProperty("submissionId").GetGuid();
    }
    private static CreateSubmissionCommand Command(Fixture f) => new(f.Owner.Id, f.Event.Id, f.Team.Id, f.Tile.Id, f.Requirement.Id, f.Requirement.ManualObjective ? null : f.Drop.Id, f.Participant.Id, 1, null, "fixture.png", new MemoryStream([1, 2, 3]));
    private static async Task ReviewAsync(HttpClient admin, Guid id, string handler)
    {
        var path = $"/Admin/Review/Details/{id}";
        var html = await admin.GetStringAsync(path);
        var version = Regex.Match(html, "name=\"Input.ExpectedVersion\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var result = await admin.PostAsync(path + "?handler=" + handler, new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(html), ["Input.ExpectedVersion"] = version, ["Input.Reason"] = "Controlled fixture" }));
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
    }
    private static async Task AssertOrdinaryReadsAsync(HttpClient player, Fixture f, Guid submissionId, string title)
    {
        foreach (var path in new[] { $"/Submissions/{submissionId}?eventId={f.Event.Id}&teamId={f.Team.Id}", $"/Submissions?eventId={f.Event.Id}&teamId={f.Team.Id}", $"/Events/{f.Event.Slug}/Board/{f.Team.Slug}/Tiles/{f.Tile.Id}" })
        {
            using var response = await player.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(title, await response.Content.ReadAsStringAsync());
        }
    }
    private async Task<string> IntegrityAsync(Fixture f)
    {
        await using var db = fixture.Db();
        return JsonSerializer.Serialize(new
        {
            Board = await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id),
            Tiles = await db.BoardTiles.AsNoTracking().Where(x => x.BoardId == f.Board.Id).OrderBy(x => x.Id).ToListAsync(),
            Requirements = await db.BoardRequirementSnapshots.AsNoTracking().Where(x => x.BoardTileId == f.Tile.Id).OrderBy(x => x.Id).ToListAsync(),
            Drops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => x.RequirementId == f.Requirement.Id).OrderBy(x => x.Id).ToListAsync(),
            Approvals = await db.BoardApprovalSnapshots.AsNoTracking().Where(x => x.BoardId == f.Board.Id).OrderBy(x => x.Id).ToListAsync(),
            Submissions = await db.Submissions.AsNoTracking().Where(x => x.EventId == f.Event.Id).OrderBy(x => x.Id).ToListAsync(),
            Audits = await db.AuditEntries.AsNoTracking().Where(x => x.TargetId == f.Board.Id.ToString() || x.EventId == f.Event.Id).OrderBy(x => x.Id).ToListAsync()
        });
    }
    private static string Token(string html) { var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value; Assert.NotEmpty(token); return token; }
    private sealed record LeaseState(long Version, DateTimeOffset? LeaseExpiresAt, long EditControlVersion, int BoardAuditCount);
    private sealed record Fixture(Account Admin, Account Owner, BingoEvent Event, Team Team, EventParticipant Participant, Board Board, BoardTile Tile, BoardRequirementSnapshot Requirement, BoardRequirementDropSnapshot Drop, BossActivity Boss, Guid ApprovalId);
}

public sealed class C20Database : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("c20_disposable").WithUsername("bingo").WithPassword("c20_fixture_only").Build();
    public C20Storage Storage { get; } = new();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options);
    public async Task InitializeAsync()
    {
        await database.StartAsync();
        await using var db = Db(); await db.Database.MigrateAsync();
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services => { services.RemoveAll<IEvidenceStorage>(); services.AddSingleton<IEvidenceStorage>(Storage); }));
    }
    public async Task DisposeAsync() { await Factory.DisposeAsync(); await database.DisposeAsync(); }
}
public sealed class C20Storage : IEvidenceStorage
{
    public Func<Guid, Task>? BeforeStore { get; set; }
    public HashSet<string> MissingKeys { get; } = [];
    public HashSet<string> DeletedKeys { get; } = [];
    public Dictionary<string, Exception> ReadFailures { get; } = [];
    public async Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default)
    {
        if (BeforeStore is not null) await BeforeStore(eventId);
        return new StoredEvidence($"c20/{eventId}/{submissionId}", originalFilename, "image/png", 3, 1, 1, submissionId.ToString("N").PadRight(64, '0'));
    }
    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => ReadFailures.TryGetValue(storageKey, out var failure) ? Task.FromException<Stream>(failure) : MissingKeys.Contains(storageKey) ? Task.FromException<Stream>(new FileNotFoundException("Controlled missing artwork")) : Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) { DeletedKeys.Add(storageKey); return Task.CompletedTask; }
}
