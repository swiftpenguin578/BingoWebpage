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

    private static async Task<string> U8TokenAsync(HttpClient client, Guid submissionId) =>
        Regex.Match(await client.GetStringAsync($"/Admin/Review/Details/{submissionId}"), "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
}
