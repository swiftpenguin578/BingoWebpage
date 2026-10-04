using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class SchedulePrecisionIntegrationTests
{
    private static DateTimeOffset At(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    private static DateTimeOffset Microseconds(DateTimeOffset value) => new(value.Ticks - value.Ticks % 10, TimeSpan.Zero);

    [Theory]
    [InlineData("2027-10-31T00:30:17.1234567Z")]
    [InlineData("2027-10-31T01:30:17.1234567Z")]
    public async Task UnrelatedEditPreservesAllOriginalInstantsIncludingEitherRepeatedHourAndPostgresPrecision(string repeatedHour)
    {
        var opens = At("2027-10-10T12:00:11.1234567Z"); var closes = At("2027-10-11T12:05:12.2345678Z");
        var draftAt = At(repeatedHour); var start = At("2027-11-01T12:10:13.3456789Z"); var end = At("2027-11-02T12:15:14.4567891Z");
        await EditAsync(item => { item.UpdateIdentity(item.Name, item.Slug, item.Description, "Europe/Copenhagen"); item.ConfigureSchedule(opens, closes, draftAt, start, end, 20); });
        var before = await ReadAsync();
        Assert.Equal(Microseconds(opens), before.SignupOpensAt); Assert.Equal(Microseconds(closes), before.SignupClosesAt);
        Assert.Equal(Microseconds(draftAt), before.DraftAt); Assert.Equal(Microseconds(start), before.EventStartsAt); Assert.Equal(Microseconds(end), before.EventEndsAt);
        using var client = await ClientAsync("first-admin"); var form = Fields(await client.GetStringAsync(Route));
        Assert.Equal("2027-10-31T02:30", form["Input.DraftLocal"]);
        form["Input.EventEndsLocal"] = "2027-11-03T13:15";
        using var response = await client.PostAsync(Route, new FormUrlEncodedContent(form)); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = await ReadAsync();
        Assert.Equal(before.SignupOpensAt, saved.SignupOpensAt); Assert.Equal(before.SignupClosesAt, saved.SignupClosesAt);
        Assert.Equal(before.DraftAt, saved.DraftAt); Assert.Equal(before.EventStartsAt, saved.EventStartsAt);
        Assert.Equal(At("2027-11-03T12:15:00Z"), saved.EventEndsAt); Assert.Equal(saved.EventEndsAt!.Value.AddMinutes(30), saved.SubmissionCutoffAt);
        Assert.False(saved.ScheduledSignupOpeningEnabled); Assert.Equal(20, saved.ParticipantCap); Assert.Equal(1, await AuditCountAsync());
        var read = await CurrentAsync(client); Assert.Equal(saved.DraftAt, read.GetProperty("values").GetProperty("draftAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task UnrelatedEditRetainsOverdueEnabledOpeningWithoutCloseAndOverduePreLiveStart()
    {
        await EditAsync(item => { item.ConfigureSchedule(Now.AddDays(-3).AddTicks(1234560), null, null, Now.AddDays(-1).AddTicks(2345670), Now.AddDays(3), null); item.ConfigureScheduledSignupOpening(true, []); });
        var before = await ReadAsync(); using var client = await ClientAsync("first-admin"); var form = Fields(await client.GetStringAsync(Route));
        form["Input.EventEndsLocal"] = "2026-10-07T12:00";
        using var response = await client.PostAsync(Route, new FormUrlEncodedContent(form)); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = await ReadAsync(); Assert.Equal(before.SignupOpensAt, saved.SignupOpensAt); Assert.Equal(before.EventStartsAt, saved.EventStartsAt);
        Assert.Null(saved.SignupClosesAt); Assert.True(saved.ScheduledSignupOpeningEnabled); Assert.Equal(1, await AuditCountAsync());
    }

    [Theory]
    [InlineData("2027-03-28T02:30", "does not exist")]
    [InlineData("2027-10-31T02:30", "ambiguous")]
    [InlineData("2027-10-30T02:32", "five-minute")]
    public async Task ChangedLocalTimeStillRejectsDstAndOffStepValues(string changed, string expected)
    {
        await EditAsync(item => item.UpdateIdentity(item.Name, item.Slug, item.Description, "Europe/Copenhagen"));
        using var client = await ClientAsync("first-admin"); var form = Fields(await client.GetStringAsync(Route)); var before = await ReadAsync();
        form["Input.DraftLocal"] = changed; var html = await PostPageAsync(client, form);
        AssertFieldError(html, "DraftLocal", expected); Assert.Equal(changed, Fields(html)["Input.DraftLocal"]);
        Assert.Equal(before.Version, (await ReadAsync()).Version); Assert.Equal(0, await AuditCountAsync());
    }

    [Fact]
    public async Task LifecycleErrorsMapToFieldsWhileOverlapAndStaleRemainFormErrors()
    {
        await EditAsync(item => item.ConfigureSchedule(null, null, null, At("2027-11-01T12:00:00Z"), At("2027-11-02T12:00:00Z"), null));
        using var client = await ClientAsync("first-admin"); var original = Fields(await client.GetStringAsync(Route));
        foreach (var (field, value, error) in new[] {
            ("EventEndsLocal", "2027-11-01T11:00", "Event end must be after event start."),
            ("SignupClosesLocal", "2027-11-01T13:00", "Signup closing must be no later than event start."),
            ("DraftLocal", "2026-10-01T12:00", "A changed draft time must be in the future."),
            ("SignupOpensLocal", "2027-10-30T12:00", "Automatic signup opening requires a signup closing time.") })
        {
            var form = new Dictionary<string, string>(original) { ["Input." + field] = value };
            var html = await PostPageAsync(client, form);
            AssertFieldError(html, field == "SignupOpensLocal" ? "SignupClosesLocal" : field, error);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var owner = await db.Events.SingleAsync(item => item.Id == eventId);
            var other = new BingoEvent(Guid.NewGuid(), "Overlap fixture", "overlap-fixture", "UTC", owner.CreatedByAccountId, Now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
            other.ConfigureSchedule(null, null, null, owner.EventStartsAt, owner.EventEndsAt, null); other.OpenSignups(Now); db.Events.Add(other); await db.SaveChangesAsync();
        }
        var overlap = new Dictionary<string, string>(original) { ["Input.EventEndsLocal"] = "2027-11-03T12:00" };
        var failed = await PostPageAsync(client, overlap); Assert.Contains("overlap", WebUtility.HtmlDecode(failed), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Overlap fixture", WebUtility.HtmlDecode(failed)); Assert.DoesNotContain("field-validation-error", failed);
        await EditAsync(item => item.UpdateIdentity("Concurrent identity", item.Slug, item.Description, item.Timezone));
        failed = await PostPageAsync(client, original); Assert.Contains("This event changed while you were editing", failed);
        Assert.Equal(original["Input.Version"], Fields(failed)["Input.Version"]); Assert.Equal(0, await AuditCountAsync());
    }

    [Theory]
    [InlineData("applied-lost-response")]
    [InlineData("unapplied")]
    [InlineData("other-admin-matching")]
    [InlineData("other-admin-different-microsecond")]
    public async Task ReadbackReturnsFullExactCurrentScheduleAndContextWithoutWritingOrAttributing(string scenario)
    {
        await EditAsync(item => item.ConfigureSchedule(null, null, null, At("2027-11-01T12:00:17.123456Z"), At("2027-11-02T12:00:18.234567Z"), 15));
        using var first = await ClientAsync("first-admin"); using var second = await ClientAsync("second-admin");
        var original = Fields(await first.GetStringAsync(Route)); var baseline = await CurrentAsync(first); var expectedDraft = At("2027-10-31T12:00:00Z");
        original["Input.DraftLocal"] = "2027-10-31T12:00";
        if (scenario != "unapplied")
        {
            var client = scenario.StartsWith("other-admin", StringComparison.Ordinal) ? second : first;
            var form = scenario.StartsWith("other-admin", StringComparison.Ordinal) ? Fields(await second.GetStringAsync(Route)) : original;
            form["Input.DraftLocal"] = original["Input.DraftLocal"];
            using var ignoredResponse = await client.PostAsync(Route, new FormUrlEncodedContent(form)); Assert.Equal(HttpStatusCode.Redirect, ignoredResponse.StatusCode);
            if (scenario == "other-admin-different-microsecond")
                await EditAsync(item => item.ConfigureSchedule(item.SignupOpensAt, item.SignupClosesAt, item.DraftAt, item.EventStartsAt!.Value.AddTicks(10), item.EventEndsAt, item.ParticipantCap));
        }
        var before = await ReadAsync(); var audits = await AuditCountAsync();
        for (var repeat = 0; repeat < 2; repeat++)
        {
            var current = await CurrentAsync(first); var values = current.GetProperty("values");
            Assert.Equal(7, values.EnumerateObject().Count()); Assert.Equal(before.Version.ToString(CultureInfo.InvariantCulture), current.GetProperty("version").GetString());
            Assert.Equal("Draft", current.GetProperty("phase").GetString()); Assert.Equal("UTC", current.GetProperty("timezone").GetString());
            Assert.Equal(JsonValueKind.Null, current.GetProperty("draftState").ValueKind); Assert.True(current.GetProperty("editable").GetProperty("eventStartsAt").GetBoolean());
            Assert.Equal(before.EventStartsAt, values.GetProperty("eventStartsAt").GetDateTimeOffset()); Assert.Equal(before.EventEndsAt, values.GetProperty("eventEndsAt").GetDateTimeOffset());
            Assert.False(values.GetProperty("scheduledSignupOpeningEnabled").GetBoolean()); Assert.Equal(15, values.GetProperty("participantCap").GetInt32());
            var matches = values.GetProperty("draftAt").ValueKind != JsonValueKind.Null && values.GetProperty("draftAt").GetDateTimeOffset() == expectedDraft
                && values.GetProperty("eventStartsAt").GetDateTimeOffset() == baseline.GetProperty("values").GetProperty("eventStartsAt").GetDateTimeOffset();
            Assert.Equal(scenario is "applied-lost-response" or "other-admin-matching", matches);
            if (scenario == "unapplied") Assert.Equal(baseline.GetProperty("version").GetString(), current.GetProperty("version").GetString());
            else Assert.True(long.Parse(current.GetProperty("version").GetString()!, CultureInfo.InvariantCulture) > long.Parse(baseline.GetProperty("version").GetString()!, CultureInfo.InvariantCulture));
        }
        Assert.Equal(before.Version, (await ReadAsync()).Version); Assert.Equal(audits, await AuditCountAsync());
        if (scenario.StartsWith("other-admin", StringComparison.Ordinal))
        {
            var failed = await PostPageAsync(first, original); Assert.Contains("This event changed while you were editing", failed);
            Assert.Equal(original["Input.DraftLocal"], Fields(failed)["Input.DraftLocal"]); Assert.Equal(audits, await AuditCountAsync());
        }
    }

    [Fact]
    public async Task ReadbackRetainsAdminVisibilityAndPhaseBoundaries()
    {
        using var ordinary = await ClientAsync("ordinary-account"); using var denied = await ordinary.GetAsync(Route + "?handler=Current");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode); Assert.Contains("AccessDenied", denied.Headers.Location!.ToString());
        using var admin = await ClientAsync("first-admin");
        await EditAsync(item => { item.ConfigureSchedule(null, null, null, Now.AddDays(1), Now.AddDays(2), null); item.OpenSignups(Now); item.CloseSignups(Now); item.StartEvent(Now); });
        var live = await CurrentAsync(admin); Assert.Equal("Live", live.GetProperty("phase").GetString());
        Assert.False(live.GetProperty("editable").GetProperty("eventStartsAt").GetBoolean()); Assert.True(live.GetProperty("editable").GetProperty("eventEndsAt").GetBoolean());
        var liveForm = Fields(await admin.GetStringAsync(Route)); liveForm["Input.EventEndsLocal"] = "2026-10-05T12:00";
        AssertFieldError(await PostPageAsync(admin, liveForm), "ConfirmChanges", "Confirm the Live event-end change before saving.");
        liveForm["Input.ConfirmChanges"] = "true";
        AssertFieldError(await PostPageAsync(admin, liveForm), "EventEndReason", "Enter a reason for changing the Live event end.");
        await EditAsync(item => item.EndEvent(Now.AddHours(1))); var finalReview = await CurrentAsync(admin);
        Assert.Equal("AwaitingFinalReview", finalReview.GetProperty("phase").GetString()); Assert.All(finalReview.GetProperty("editable").EnumerateObject(), field => Assert.False(field.Value.GetBoolean()));
        await EditAsync(item => item.Hide(item.CreatedByAccountId, Now.AddHours(2), null, "Synthetic visibility test"));
        using var hidden = await admin.GetAsync(Route + "?handler=Current"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode); Assert.Equal(0, await AuditCountAsync());
    }

    private async Task<JsonElement> CurrentAsync(HttpClient client)
    {
        using var response = await client.GetAsync(Route + "?handler=Current"); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        var current = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(eventId, current.GetProperty("eventId").GetGuid()); return current;
    }
    private static void AssertFieldError(string html, string field, string error)
    {
        var span = Regex.Match(html, $"<span[^>]*data-valmsg-for=\"Input.{field}\"[^>]*>(.*?)</span>", RegexOptions.Singleline);
        Assert.True(span.Success); Assert.Contains(error, WebUtility.HtmlDecode(span.Groups[1].Value));
    }
}
