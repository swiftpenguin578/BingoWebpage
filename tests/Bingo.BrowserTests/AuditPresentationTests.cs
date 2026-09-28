using System.Net;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Bingo.BrowserTests;

[Collection(BrowserTestGroup.Name)]
public sealed class AuditPresentationTests(BrowserTestApplicationFactory factory)
{
    [Theory]
    [InlineData("en", "Account disabled", "Technical details", "Before", "No")]
    [InlineData("da", "Konto deaktiveret", "Tekniske oplysninger", "Før", "Nej")]
    public async Task FullAndRecentAuditRenderTheSameLocalizedEntry(string culture, string action, string technical, string before, string inactive)
    {
        var now = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            now = (await db.AuditEntries.MaxAsync(entry => (DateTimeOffset?)entry.OccurredAt) ?? now).AddMinutes(1);
        }
        var username = $"audit-{culture}-{Guid.NewGuid():N}";
        const string password = "Audit-test-password-123!";
        var accountCreatedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var account = Account.CreateWebsite(Guid.NewGuid(), username, username.ToUpperInvariant(), accountCreatedAt);
        account.SetGlobalRole(GlobalRole.Admin);
        account.SetPassword(new PasswordHasher<Account>().HashPassword(account, password), false, accountCreatedAt, incrementVersion: false);
        var entry = new AuditEntry(Guid.NewGuid(), now, account.Id, username, "account.disabled", "account", account.Id.ToString(),
            "{\"reason\":\"Organizer request <script>\"}", beforeState: "{\"Active\":true,\"PasswordHash\":\"never-display\"}", afterState: "{\"Active\":false}");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AddRange(account, entry);
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var login = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = username,
            ["Input.Password"] = password,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
        var full = await client.GetStringAsync("/Admin/Audit");
        var recent = await client.GetStringAsync("/Admin");
        var pattern = $"<div class=\"admin-audit-presentation\" data-audit-entry=\"{entry.Id}\">.*?</div>";
        var fullEntry = Regex.Match(full, pattern, RegexOptions.Singleline).Value;
        var recentEntry = Regex.Match(recent, pattern, RegexOptions.Singleline).Value;
        Assert.NotEmpty(fullEntry);
        Assert.Equal(fullEntry, recentEntry);
        var decoded = WebUtility.HtmlDecode(fullEntry);
        Assert.Contains(action, decoded);
        Assert.Contains(technical, decoded);
        Assert.Contains(before, decoded);
        Assert.Contains(inactive, decoded);
        Assert.Contains(username, decoded);
        Assert.Contains(account.Id.ToString(), decoded);
        Assert.Contains("Organizer request &lt;script&gt;", fullEntry);
        Assert.DoesNotContain("never-display", fullEntry);
        Assert.True(fullEntry.IndexOf("<details", StringComparison.Ordinal) < fullEntry.IndexOf("account.disabled", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{not json", "[broken", "null")]
    [InlineData("[]", "42", "\"legacy\"")]
    [InlineData("old plain text", null, null)]
    public void UnknownMalformedLegacyHistoryIsReadableWithoutThrowing(string details, string? before, string? after)
    {
        using var scope = factory.Services.CreateScope();
        var text = scope.ServiceProvider.GetRequiredService<IStringLocalizer<AuditResource>>();
        var entry = new AuditEntry(Guid.NewGuid(), DateTimeOffset.UnixEpoch, null, "Legacy actor", "legacy.unknown", "legacy-target", "old-id", details, beforeState: before, afterState: after);
        var shown = AuditPresenter.Present(entry, text);
        Assert.Equal("Recorded administrative action", shown.Action);
        Assert.Equal("Legacy actor", shown.Actor);
        Assert.Contains("old-id", shown.Target);
        Assert.Empty(shown.Changes);
    }

    [Fact]
    public void EmbeddedLegacyChangesAreDecodedAndSensitiveValuesAreNotPresented()
    {
        using var scope = factory.Services.CreateScope();
        var text = scope.ServiceProvider.GetRequiredService<IStringLocalizer<AuditResource>>();
        var entry = new AuditEntry(Guid.NewGuid(), DateTimeOffset.UnixEpoch, null, "Admin", "board.updated", "board", "board-id",
            "{\"before\":{\"Name\":\"Old\",\"Token\":\"private\"},\"after\":{\"Name\":\"New\"},\"reason\":\"Correction\"}");
        var shown = AuditPresenter.Present(entry, text);
        Assert.Equal(new AuditFieldChange("Name", "Old", "New"), Assert.Single(shown.Changes));
        Assert.Equal("Correction", shown.Reason);
        Assert.DoesNotContain("private", shown.Details);
        var code = new AuditEntry(Guid.NewGuid(), DateTimeOffset.UnixEpoch, null, "Admin", "evidence_code.created", "event", "id", "private-code; activates later");
        Assert.DoesNotContain("private-code", AuditPresenter.Present(code, text).Details);
    }

    [Theory]
    [InlineData("en", "Reason", "Technical details")]
    [InlineData("da", "Begrundelse", "Tekniske oplysninger")]
    public async Task ProductionReasonPayloadsRenderOutsideTechnicalDetailsInBothConsumers(string culture, string reasonLabel, string technicalLabel)
    {
        var accountAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var username = $"reason-{Guid.NewGuid():N}";
        const string password = "Audit-reason-password-123!";
        var admin = Account.CreateWebsite(Guid.NewGuid(), username, username.ToUpperInvariant(), accountAt);
        admin.SetGlobalRole(GlobalRole.Admin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, password), false, accountAt, incrementVersion: false);
        DateTimeOffset auditAt;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Accounts.Add(admin);
            await db.SaveChangesAsync();
            // Keep this fixture newer than other tests' entries without relying on wall-clock precision.
            auditAt = (await db.AuditEntries.MaxAsync(entry => (DateTimeOffset?)entry.OccurredAt) ?? accountAt).AddMinutes(1);
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var login = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = username,
            ["Input.Password"] = password,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
        // Shapes copied from SubmissionService, EventLifecycleService, EventFinalizationService,
        // Draft.OnPostReopenAsync and Manage.OnPostReopenSubmissionsAsync.
        (string Action, string Details, string Reason)[] payloads =
        [
            ("submission.rejected", "Image is unreadable", "Image is unreadable"),
            ("submission.reversed", "Duplicate screenshot", "Duplicate screenshot"),
            ("event.started", "Players agreed to start early", "Players agreed to start early"),
            ("event.ended", "Organizer requested an early finish", "Organizer requested an early finish"),
            ("event.resumed", "{Mistaken early end}; resume play", "{Mistaken early end}; resume play"),
            ("event.final_review_overridden", "{\"blockerKey\":\"review-check\",\"reason\":\"Organizer verified the result\",\"reviewCycleId\":\"00000000-0000-0000-0000-000000000001\"}", "Organizer verified the result"),
            ("draft.reopened", "Publication cycle 3; Correct the roster; preserve picks", "Correct the roster; preserve picks"),
            ("event.submissions_reopened", "Until 2026-09-27T12:00:00.0000000+00:00; Allow the missing screenshot", "Allow the missing screenshot")
        ];
        foreach (var payload in payloads)
        {
            var entry = new AuditEntry(Guid.NewGuid(), auditAt, admin.Id, username, payload.Action,
                payload.Action.StartsWith("submission.", StringComparison.Ordinal) ? "submission" : payload.Action.StartsWith("draft.", StringComparison.Ordinal) ? "draft" : "event",
                Guid.NewGuid().ToString(), payload.Details);
            auditAt = auditAt.AddMinutes(1);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.AuditEntries.Add(entry);
                await db.SaveChangesAsync();
            }
            var full = await client.GetStringAsync("/Admin/Audit");
            var recent = await client.GetStringAsync("/Admin");
            var pattern = $"<div class=\"admin-audit-presentation\" data-audit-entry=\"{entry.Id}\">.*?</div>";
            var rendered = Regex.Match(full, pattern, RegexOptions.Singleline).Value;
            Assert.NotEmpty(rendered);
            Assert.Equal(rendered, Regex.Match(recent, pattern, RegexOptions.Singleline).Value);
            var technicalIndex = rendered.IndexOf("<details", StringComparison.Ordinal);
            Assert.True(technicalIndex > 0);
            var readable = WebUtility.HtmlDecode(rendered[..technicalIndex]);
            Assert.Contains($"{reasonLabel}: {payload.Reason}", readable);
            Assert.DoesNotContain("Publication cycle", readable);
            Assert.DoesNotContain("blockerKey", readable);
            Assert.Contains(technicalLabel, WebUtility.HtmlDecode(rendered[technicalIndex..]));
            Assert.Contains(payload.Action, rendered[technicalIndex..]);
            if (payload.Action == "draft.reopened")
                Assert.Contains(payload.Details, WebUtility.HtmlDecode(rendered[technicalIndex..]));
        }
    }
}
