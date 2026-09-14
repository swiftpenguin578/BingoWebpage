using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Domain.Boards;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class C20ObjectiveIdentityIntegrationTests
{
    [Theory]
    [InlineData("append")]
    [InlineData("remove")]
    [InlineData("target")]
    [InlineData("neutral")]
    public async Task C20ReviewEvidencedTileProtectsSiblingScoringInputsAndAllowsNeutralReplacement(string change)
    {
        var f = await SeedAsync(manual: true);
        using var admin = await ClientAsync(f.Admin);
        using var player = await ClientAsync(f.Owner);
        Guid? siblingId = null;
        if (change != "append") siblingId = await PublishSiblingAsync(admin, f);
        var submission = await SubmitAsync(player, f);
        await ReviewAsync(admin, submission, "Approve");
        var earned = await PublicEhbAsync(f);
        Assert.Equal(change == "append" ? 1m : .5m, earned);
        await StartCorrectionAsync(admin, f);
        var fields = SiblingFields(f, siblingId);
        if (change == "remove") foreach (var key in fields.Keys.Where(x => x.Contains("Requirements[1]", StringComparison.Ordinal)).ToList()) fields.Remove(key);
        if (change == "target") fields["TileDraft.Requirements[1].Target"] = "7";
        if (change == "neutral") fields["TileDraft.Requirements[1].DuplicatesAllowed"] = "false";
        var before = await RecoveryIntegrityAsync(f);
        var publicationBefore = await PublishedIntegrityAsync(f);
        await PostBoardAsync(admin, f, "EditTile", fields);
        if (change != "neutral")
        {
            Assert.Equal(before, await RecoveryIntegrityAsync(f));
            Assert.Equal(publicationBefore, await PublishedIntegrityAsync(f));
        }
        else
        {
            await using var db = fixture.Db();
            var replacement = await db.BoardRequirementSnapshots.AsNoTracking().SingleAsync(x => x.BoardTileId == f.Tile.Id && x.Id != f.Requirement.Id);
            Assert.NotEqual(siblingId, replacement.Id);
            Assert.Equal(5, replacement.TargetContribution);
            Assert.False(replacement.DuplicatesAllowed);
            var approvalBeforeReplacement = (await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id)).ActiveApprovalSnapshotId;
            await PublishCorrectionAsync(admin, f);
            Assert.NotEqual(approvalBeforeReplacement, (await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id)).ActiveApprovalSnapshotId);
            Assert.Equal(f.Requirement.Id, (await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == submission)).RequirementId);
            Assert.Equal(1, (await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == submission)).ApprovedContribution);
        }
        Assert.Equal(earned, await PublicEhbAsync(f));
    }

    [Fact]
    public async Task C20ReviewLateEvidenceRejectsSiblingDenominatorChangeAtPublication()
    {
        var f = await SeedAsync(manual: true);
        using var admin = await ClientAsync(f.Admin);
        using var player = await ClientAsync(f.Owner);
        var siblingId = await PublishSiblingAsync(admin, f);
        await StartCorrectionAsync(admin, f);
        var fields = SiblingFields(f, siblingId);
        fields["TileDraft.Requirements[1].Target"] = "7";
        await PostBoardAsync(admin, f, "EditTile", fields);
        await using var db = fixture.Db();
        Assert.Equal(12, await db.BoardRequirementSnapshots.Where(x => x.BoardTileId == f.Tile.Id).SumAsync(x => x.TargetContribution));
        var submission = await SubmitAsync(player, f);
        await ReviewAsync(admin, submission, "Approve");
        Assert.Equal(.5m, await PublicEhbAsync(f));
        var before = await RecoveryIntegrityAsync(f);
        var publicationBefore = await PublishedIntegrityAsync(f);
        await PublishCorrectionAsync(admin, f);
        Assert.Equal(before, await RecoveryIntegrityAsync(f));
        Assert.Equal(publicationBefore, await PublishedIntegrityAsync(f));
        Assert.Equal(.5m, await PublicEhbAsync(f));
        Assert.Contains("cannot change its scoring", await admin.GetStringAsync($"/Admin/Events/Board/{f.Event.Id}"));
    }

    [Fact]
    public async Task C20ReviewPrivateDimensionsPreservePublicCompletionAndCaptainFocusBounds()
    {
        var f = await SeedAsync(manual: true);
        await using var db = fixture.Db();
        (await db.TeamMemberships.SingleAsync(x => x.TeamId == f.Team.Id && x.EventParticipantId == f.Participant.Id)).ChangeRole(TeamMembershipRole.Captain);
        await db.SaveChangesAsync();
        using var admin = await ClientAsync(f.Admin);
        using var captain = await ClientAsync(f.Owner);
        for (var i = 0; i < 5; i++) await ReviewAsync(admin, await SubmitAsync(captain, f), "Approve");
        var publicBefore = (await new PublicBoardService(db, TimeProvider.System).GetEventBoardAsync(f.Event.Slug))!;
        Assert.True(Assert.Single(publicBefore.Teams).Progress.BoardComplete);
        Assert.Equal(0, Assert.Single(Assert.Single(publicBefore.Teams).Progress.CompletedRows));
        Assert.Equal(0, Assert.Single(Assert.Single(publicBefore.Teams).Progress.CompletedColumns));
        await StartCorrectionAsync(admin, f);
        await PostBoardAsync(admin, f, "Resize", new() { ["Rows"] = "2", ["Columns"] = "3" });
        Assert.Equal(2, (await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id)).Rows);
        var publicAfter = (await new PublicBoardService(db, TimeProvider.System).GetEventBoardAsync(f.Event.Slug))!;
        Assert.Equal(1, publicAfter.Rows); Assert.Equal(1, publicAfter.Columns);
        Assert.Equal(JsonSerializer.Serialize(Assert.Single(publicBefore.Teams).Progress), JsonSerializer.Serialize(Assert.Single(publicAfter.Teams).Progress));
        var path = $"/Submissions?eventId={f.Event.Id}&teamId={f.Team.Id}";
        var html = await captain.GetStringAsync(path);
        Assert.Single(Regex.Matches(html, "data-captain-focus-target=\"Row\""));
        Assert.Single(Regex.Matches(html, "data-captain-focus-target=\"Column\""));
        foreach (var kind in new[] { "Row", "Column" })
        {
            using var denied = await captain.PostAsync(path + "&handler=AddFocus", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(html),
                ["AddFocusInput.TargetKind"] = kind,
                ["AddFocusInput.TargetValue"] = "1|0"
            }));
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.False(await db.TeamFocusMarkers.AnyAsync(x => x.EventId == f.Event.Id));
        }
        foreach (var kind in new[] { "Row", "Column" })
        {
            using var accepted = await captain.PostAsync(path + "&handler=AddFocus", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(html),
                ["AddFocusInput.TargetKind"] = kind,
                ["AddFocusInput.TargetValue"] = "0|0"
            }));
            Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        }
        Assert.Equal(2, await db.TeamFocusMarkers.CountAsync(x => x.EventId == f.Event.Id && x.Focused));
        Assert.Equal(JsonSerializer.Serialize(Assert.Single(publicBefore.Teams).Progress), JsonSerializer.Serialize(Assert.Single((await new PublicBoardService(db, TimeProvider.System).GetEventBoardAsync(f.Event.Slug))!.Teams).Progress));
    }

    private static Dictionary<string, string> SiblingFields(Fixture f, Guid? siblingId)
    {
        var fields = Fields(f, null, null, 5, 1, true, null);
        foreach (var pair in fields.Where(x => x.Key.Contains("Requirements[0]", StringComparison.Ordinal)).ToList())
            fields[pair.Key.Replace("Requirements[0]", "Requirements[1]", StringComparison.Ordinal)] = pair.Value;
        fields["TileDraft.Requirements[1].RequirementId"] = siblingId?.ToString() ?? "";
        fields["TileDraft.Requirements[1].Description"] = "Separate unevidenced objective";
        return fields;
    }

    private async Task<Guid> PublishSiblingAsync(HttpClient admin, Fixture f)
    {
        await StartCorrectionAsync(admin, f);
        await PostBoardAsync(admin, f, "EditTile", SiblingFields(f, null));
        await PublishCorrectionAsync(admin, f);
        await using var db = fixture.Db();
        return (await db.BoardRequirementSnapshots.AsNoTracking().SingleAsync(x => x.BoardTileId == f.Tile.Id && x.Id != f.Requirement.Id)).Id;
    }

    private async Task<decimal> PublicEhbAsync(Fixture f)
    {
        await using var db = fixture.Db();
        return Assert.Single((await new PublicBoardService(db, TimeProvider.System).GetEventBoardAsync(f.Event.Slug))!.Teams).Progress.EhbTiebreak;
    }
}
