using System.Globalization;
using Bingo.Domain.Auditing;
using Bingo.Web;
using Bingo.Web.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Bingo.BrowserTests;

/// <summary>T1-8 (a): the Changes table is human-readable; raw values stay in Technical details.</summary>
[Collection(BrowserTestGroup.Name)]
public sealed class AuditReadableChangesTests(BrowserTestApplicationFactory factory)
{
    // The user's example shape: a tile entry with ids, a lease, a raw decimal and empty fields.
    private const string Before = """{"Tile":{"Id":"7f1c","TileTemplateId":"a1","Name":"Vorkath head","EstimatedEhb":"8.34860000000000000000000000","Description":null,"Weight":"1"},"Board":{"Version":"4","EditorLeaseExpiresAt":"2027-05-27T08:00:00.0000000+00:00","UpdatedAt":"2027-05-27T07:45:10.0000000+00:00","ConcurrencyStamp":"x1"}}""";
    private const string After = """{"Tile":{"Id":"7f1c","TileTemplateId":"a2","Name":"Vorkath heads","EstimatedEhb":"9.5000000000","Description":null,"Weight":"1.00"},"Board":{"Version":"5","EditorLeaseExpiresAt":"2027-05-27T09:00:00.0000000+00:00","UpdatedAt":"2027-10-30T23:30:00.0000000+00:00","ConcurrencyStamp":"x2"}}""";

    [Theory]
    [InlineData("en-GB", "Tile · Name", "8.35", "9.5", "Sunday 31 October 2027, 01:30:00 (UTC+02:00)")]
    [InlineData("da-DK", "Tile · Name", "8,35", "9,5", "søndag 31 oktober 2027, 01.30.00 (UTC+02:00)")]
    public void ChangesShowOnlyReadableRealChanges(string culture, string nameField, string beforeEhb, string afterEhb, string updatedAt)
    {
        var (previous, previousUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            using var scope = factory.Services.CreateScope();
            var text = scope.ServiceProvider.GetRequiredService<IStringLocalizer<AuditResource>>();
            var entry = new AuditEntry(Guid.NewGuid(), new DateTimeOffset(2027, 10, 31, 9, 0, 0, TimeSpan.Zero), null, "System", "board.tile_edited", "tile", Guid.NewGuid().ToString(), null, null, Before, After);
            var shown = AuditPresenter.Present(entry, text);
            var fields = shown.Changes.Select(change => change.Field).ToArray();
            // Rule 1: ids, versions, leases and concurrency markers are not changes.
            Assert.DoesNotContain(fields, field => field.EndsWith("Id", StringComparison.Ordinal) || field.Contains("id", StringComparison.Ordinal) && field.EndsWith(" id", StringComparison.Ordinal));
            Assert.DoesNotContain(fields, field => field.Contains("ersion", StringComparison.Ordinal) || field.Contains("ease", StringComparison.Ordinal) || field.Contains("oncurrency", StringComparison.Ordinal));
            // Rule 2: empty → empty and identical (after formatting, "1" vs "1.00") rows are omitted.
            Assert.DoesNotContain(fields, field => field.Contains("Description", StringComparison.Ordinal) || field.Contains("Weight", StringComparison.Ordinal));
            // Rules 3–5: rounded decimals, readable instants, and only the shown rows count.
            Assert.Equal(3, shown.Changes.Count);
            Assert.Contains(shown.Changes, change => change.Field == nameField && change.Before == "Vorkath head" && change.After == "Vorkath heads");
            Assert.Contains(shown.Changes, change => change.Before == beforeEhb && change.After == afterEhb);
            Assert.Contains(shown.Changes, change => change.After == updatedAt && change.Before.Contains("2027", StringComparison.Ordinal) && !System.Text.RegularExpressions.Regex.IsMatch(change.Before, @"\dT\d"));
            // Technical details keep the raw values, ids included.
            Assert.Contains("TileTemplateId", shown.AfterState, StringComparison.Ordinal);
            Assert.Contains("8.34860000000000000000000000", shown.BeforeState, StringComparison.Ordinal);
        }
        finally { CultureInfo.CurrentCulture = previous; CultureInfo.CurrentUICulture = previousUi; }
    }

    [Theory]
    [InlineData("Id", true)]
    [InlineData("Tile · Id", true)]
    [InlineData("TileTemplateId", true)]
    [InlineData("tile_template_id", true)]
    [InlineData("Board · Editor lease expires at", true)]
    [InlineData("Board · EditorLeaseExpiresAt", true)]
    [InlineData("Version", true)]
    [InlineData("CalculationVersion", true)]
    [InlineData("ConcurrencyStamp", true)]
    [InlineData("Members [2] · AccountId", true)]
    [InlineData("Name", false)]
    [InlineData("Valid", false)]
    [InlineData("Paid", false)]
    [InlineData("Video", false)]
    [InlineData("Tile · Description", false)]
    public void TechnicalFieldPolicyIsExplicit(string key, bool technical) => Assert.Equal(technical, AuditPresenter.IsTechnicalField(key));

    [Fact]
    public async Task TechnicalOnlyChangesSayWhereTheyAre()
    {
        using var scope = factory.Services.CreateScope();
        var text = scope.ServiceProvider.GetRequiredService<IStringLocalizer<AuditResource>>();
        var technical = AuditPresenter.Present(new AuditEntry(Guid.NewGuid(), DateTimeOffset.UnixEpoch, null, "Admin", "board.editing_acquired", "board", "id", null, null,
            """{"Version":"4","EditorLeaseExpiresAt":null,"Name":"Same"}""", """{"Version":"5","EditorLeaseExpiresAt":"2027-05-27T09:00:00Z","Name":"Same"}"""), text);
        Assert.Empty(technical.Changes);
        Assert.True(technical.TechnicalOnly);
        var nothing = AuditPresenter.Present(new AuditEntry(Guid.NewGuid(), DateTimeOffset.UnixEpoch, null, "Admin", "board.updated", "board", "id", null, null,
            """{"Name":"Same"}""", """{"Name":"Same"}"""), text);
        Assert.False(nothing.TechnicalOnly);

        // Rendered drawer text (English and Danish) for a technical-only entry on the Audit page.
        var entry = new AuditEntry(Guid.NewGuid(), new DateTimeOffset(2031, 1, 1, 12, 0, 0, TimeSpan.Zero), null, "System", "board.editing_acquired", "board", Guid.NewGuid().ToString(), null, null,
            """{"Version":"1"}""", """{"Version":"2"}""");
        using (var write = factory.Services.CreateScope())
        {
            var db = write.ServiceProvider.GetRequiredService<Bingo.Infrastructure.Persistence.ApplicationDbContext>();
            db.AuditEntries.Add(entry); await db.SaveChangesAsync();
        }
        foreach (var (culture, expected) in new[] { ("en", "Only technical fields changed. See Technical details."), ("da", "Kun tekniske felter blev ændret. Se Tekniske oplysninger.") })
        {
            using var client = await AuditTestLogin.ClientAsync(factory, culture);
            var html = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync($"/Admin/Audit?entry={entry.Id}"));
            var drawer = System.Text.RegularExpressions.Regex.Match(html, $"<template data-audit-detail=\"{entry.Id}\">.*?</template>", System.Text.RegularExpressions.RegexOptions.Singleline).Value;
            Assert.Contains(expected, drawer, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SensitiveActionsStillShowNoChanges()
    {
        using var scope = factory.Services.CreateScope();
        var text = scope.ServiceProvider.GetRequiredService<IStringLocalizer<AuditResource>>();
        var entry = new AuditEntry(Guid.NewGuid(), DateTimeOffset.UnixEpoch, null, "Admin", "event.signup_code_changed", "event", "id", null, null, """{"Required":"false"}""", """{"Required":"true"}""");
        var shown = AuditPresenter.Present(entry, text);
        Assert.True(shown.Sensitive);
        Assert.Empty(shown.Changes);
    }
}

internal static class AuditTestLogin
{
    public static async Task<HttpClient> ClientAsync(BrowserTestApplicationFactory factory, string culture)
    {
        var username = $"audit-l1-{Guid.NewGuid():N}";
        const string password = "Audit-l1-password-123!";
        var at = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var account = Bingo.Domain.Access.Account.CreateWebsite(Guid.NewGuid(), username, username.ToUpperInvariant(), at);
        account.SetGlobalRole(Bingo.Domain.Access.GlobalRole.Admin);
        account.SetPassword(new Microsoft.AspNetCore.Identity.PasswordHasher<Bingo.Domain.Access.Account>().HashPassword(account, password), false, at, incrementVersion: false);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Bingo.Infrastructure.Persistence.ApplicationDbContext>();
            db.Add(account); await db.SaveChangesAsync();
        }
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var login = await client.GetStringAsync("/Account/Login");
        var token = System.Text.RegularExpressions.Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = username,
            ["Input.Password"] = password,
            ["__RequestVerificationToken"] = System.Net.WebUtility.HtmlDecode(token)
        }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
        return client;
    }
}
