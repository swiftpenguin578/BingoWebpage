using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Web;
using Bingo.Web.UI;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bingo.BrowserTests;

/// <summary>
/// Brief 147 item 1: every audit entry reads as one sentence ("who did what to whom") with names
/// resolved at display time, falling back to the stored name or label, else the id, in English and
/// Danish. Payload shapes mirror the producers listed in artifacts/audit-overhaul/inventory.md.
/// </summary>
public sealed class AuditSentenceTests
{
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Participant = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Other = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Owner = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Membership = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Team = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid Tile = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid Question = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly StringLocalizer<AuditResource> Text = new(
        new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance));

    private static AuditNames Names()
    {
        var names = new AuditNames();
        names.Events[EventId] = "Summer Bingo";
        names.Accounts[Owner] = "Lena";
        names.Participants[Participant] = new(Owner, "Zezima", EventId);
        names.Participants[Other] = new(null, "Lynx Titan", EventId);
        names.Memberships[Membership] = new(Participant, Team);
        names.Teams[Team] = "Red Dragons";
        names.Teams[Other] = "Blue Dragons";
        names.TeamEvents[Team] = EventId;
        names.Tiles[Tile] = "Vorkath head";
        names.Tiles[Other] = "Zulrah uniques";
        names.Questions[Question] = "Discord name";
        names.Bosses[Other] = "Zulrah";
        names.Items[Tile] = "Tanzanite fang";
        names.Drops[Question] = new("Zulrah", "Tanzanite fang");
        return names;
    }

    private static AuditEntry Entry(string action, string type, Guid? target, string? details = null, string? before = null, string? after = null, Guid? eventId = null, bool system = false) =>
        new(Guid.NewGuid(), new DateTimeOffset(2026, 10, 10, 18, 0, 0, TimeSpan.Zero), system ? null : Guid.NewGuid(), system ? "System" : "chris",
            action, type, target?.ToString(), details, eventId, before, after);

    private static T InCulture<T>(string culture, Func<T> action)
    {
        var (previous, previousUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try { return action(); }
        finally { CultureInfo.CurrentCulture = previous; CultureInfo.CurrentUICulture = previousUi; }
    }

    private static string? Sentence(AuditEntry entry, string culture = "en-GB", AuditNames? names = null) =>
        InCulture(culture, () => AuditPresenter.Present(entry, Text, names ?? Names()).Summary);

    private static string J(object value) => JsonSerializer.Serialize(value);

    public static TheoryData<string, AuditEntry, string> OneEntryPerArea() => new()
    {
        { "accounts", Entry("account.admin_granted", "account", Owner, "Role/state changed from User to Admin.", J(new { roleOrState = "User" }), J(new { roleOrState = "Admin" })), "chris gave Lena Admin access." },
        { "events", Entry("event.started", "event", EventId, "Ready", J(new { state = EventState.SignupClosed }), J(new { state = EventState.Live }), EventId), "chris started Summer Bingo." },
        { "signups", Entry("signup_question.created", "signup_question", Question, null, null, J(new { Label = "Discord" }), EventId), "chris added the signup question “Discord name”." },
        { "participants", Entry("participant.payment_updated", "participant", Participant, "Payment changed.", "{\"payment\":\"Unpaid\"}", "{\"payment\":\"Paid\"}", EventId), "chris marked Zezima as paid." },
        { "teams", Entry("team.member_moved", "membership", Membership, "Red Dragons → Blue Dragons: Balance"), "chris moved Zezima from Red Dragons to Blue Dragons." },
        { "draft", Entry("draft.pick_recorded", "pick", Guid.NewGuid(), J(new { before = new { }, after = new { pick = new { TeamId = Team, EventParticipantId = Participant, PickNumber = 7 } } }), eventId: EventId), "chris recorded pick 7: Red Dragons picked Zezima." },
        { "board", Entry("board.tile_moved", "board", Guid.NewGuid(), J(new { before = new { source = new { Id = Tile, RowIndex = 0, ColumnIndex = 2 } }, after = new { source = new { Id = Tile, RowIndex = 1, ColumnIndex = 3 } } }), eventId: EventId), "chris moved the tile “Vorkath head” from row 1, column 3 to row 2, column 4." },
        { "evidence", Entry("submission.approved", "submission", Guid.NewGuid(), "Approved contribution: 1.", J(new { BoardTileId = Tile, CreditedCharacterName = "Zezima", Status = SubmissionStatus.Pending }), J(new { BoardTileId = Tile, CreditedCharacterName = "Zezima", Status = SubmissionStatus.Approved }), EventId), "chris approved the evidence for “Vorkath head” by Zezima." },
        { "catalogue", Entry("catalogue.drop_created", "source_drop", Question, "Zulrah: Tanzanite fang", "{}", J(new { Drop = new { Id = Question }, Item = new { Name = "Tanzanite fang" } })), "chris added Tanzanite fang as a drop from Zulrah." },
        { "automatic", Entry("event.started_automatically", "event", EventId, null, J(new { state = EventState.SignupClosed }), J(new { state = EventState.Live }), EventId, system: true), "Summer Bingo started automatically at its scheduled time." },
        { "sign-out", Entry("logout", "account", Owner), "chris signed out." }
    };

    [Theory]
    [MemberData(nameof(OneEntryPerArea))]
    public void EachAreaReadsAsOneSentenceWithResolvedNames(string area, AuditEntry entry, string expected)
    {
        Assert.False(string.IsNullOrEmpty(area));
        Assert.Equal(expected, Sentence(entry));
    }

    [Fact]
    public void DeletedTargetsFallBackToTheStoredNameOrLabelElseTheId()
    {
        var none = AuditNames.Empty;
        // Stored name/label inside the entry.
        Assert.Equal("chris updated the team Blue Dragons.", Sentence(Entry("team.updated", "team", Team, J(new { before = new { Name = "Red Dragons" }, after = new { Name = "Blue Dragons" } })), names: none));
        Assert.Equal("chris added the signup question “Discord”.", Sentence(Entry("signup_question.created", "signup_question", Question, null, null, J(new { Label = "Discord" })), names: none));
        Assert.Equal($"chris replaced {Other} with Mira on — before the event.",
            Sentence(Entry("participant.prelive_replaced", "membership", Membership, null, J(new { departedParticipantId = Other }), J(new { replacementParticipantId = Participant, replacementName = "Mira" })), names: none));
        // No stored name: the id.
        Assert.Equal($"chris gave {Owner} Admin access.", Sentence(Entry("account.admin_granted", "account", Owner), names: none));
        Assert.Equal($"chris marked {Participant} as unpaid.", Sentence(Entry("participant.payment_updated", "participant", Participant, null, "{\"payment\":\"Paid\"}", "{\"payment\":\"Unpaid\"}"), names: none));
        // Malformed history never fails the page.
        var broken = AuditPresenter.Present(Entry("draft.pick_recorded", "pick", Guid.NewGuid(), "{not json"), Text, none);
        Assert.Equal("chris recorded pick —: — picked —.", broken.Summary);
    }

    [Fact]
    public void DanishRendersTheSentencesLabelsAndReadableValues()
    {
        var payment = Entry("participant.payment_updated", "participant", Participant, "Payment changed.", "{\"payment\":\"Unpaid\"}", "{\"payment\":\"Paid\"}", EventId);
        Assert.Equal("chris markerede Zezima som betalt.", Sentence(payment, "da-DK"));
        var shown = InCulture("da-DK", () => AuditPresenter.Present(payment, Text, Names()));
        Assert.Equal("Betalingsstatus opdateret", shown.Action);
        Assert.Equal("Deltager · Zezima", shown.Target);
        Assert.Equal(new AuditFieldChange("Betaling", "Ikke betalt", "Betalt"), Assert.Single(shown.Changes));

        // Status producers store bare values: shown as a Status change in both languages.
        var confirmed = Entry("participant.admin_confirmed", "participant", Participant, "Selected confirmation used an existing capacity place.", "WaitingList", "Confirmed", EventId);
        Assert.Equal(new AuditFieldChange("Status", "Waiting list", "Confirmed"), Assert.Single(InCulture("en-GB", () => AuditPresenter.Present(confirmed, Text, Names())).Changes));
        Assert.Equal(new AuditFieldChange("Status", "Venteliste", "Bekræftet"), Assert.Single(InCulture("da-DK", () => AuditPresenter.Present(confirmed, Text, Names())).Changes));

        // Event states are stored as numbers and shown by their page labels.
        var started = Entry("event.started", "event", EventId, null, J(new { state = EventState.SignupClosed }), J(new { state = EventState.Live }), EventId);
        Assert.Contains(InCulture("da-DK", () => AuditPresenter.Present(started, Text, Names())).Changes, change => change.Before == "Tilmelding lukket" && change.After == "Live");
        Assert.Equal("chris startede Summer Bingo.", Sentence(started, "da-DK"));

        // A1: Danish audit labels use event / board / bevis; finalized has its own label.
        Assert.Equal("Event færdiggjort", InCulture("da-DK", () => Text["Event finalized"].Value));
        Assert.NotEqual(InCulture("da-DK", () => Text["Event ended"].Value), InCulture("da-DK", () => Text["Event finalized"].Value));
        Assert.Equal("Bevis godkendt", InCulture("da-DK", () => Text["Evidence approved"].Value));
        Assert.Equal("Board offentliggjort", InCulture("da-DK", () => Text["Board published"].Value));
    }

    [Fact]
    public void EveryLabelledActionHasASentenceAndEverySentenceHasADanishEntry()
    {
        var notificationOnly = new[] { "participant.accounts_changed", "participant.prelive_withdrawn", "participant.restored", "participant.waiting" };
        foreach (var key in AuditPresenter.ActionLabels.Keys.Except(notificationOnly))
            Assert.False(string.IsNullOrEmpty(Sentence(Entry(key, "event", EventId, eventId: EventId), names: AuditNames.Empty)), key + ": no sentence");

        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "UI", "AuditSentences.cs"));
        var formats = Regex.Matches(source, @"\bL\(""(?<format>(?:[^""\\]|\\.)*)""").Select(match => match.Groups["format"].Value).Distinct().ToList();
        Assert.True(formats.Count > 150, "the sentence scan found too few formats");
        var danish = XDocument.Load(Path.Combine(root, "src", "Bingo.Web", "Resources", "AuditResource.da.resx"))
            .Descendants("data").ToDictionary(item => item.Attribute("name")!.Value, item => item.Element("value")?.Value);
        var missing = formats.Where(format => !danish.TryGetValue(format, out var value) || string.IsNullOrWhiteSpace(value)).ToList();
        Assert.True(missing.Count == 0, "Sentences without a Danish entry:\n" + string.Join('\n', missing));
        foreach (var format in formats)
            Assert.Equal(Regex.Count(format, @"\{\d\}"), Regex.Count(danish[format]!, @"\{\d\}"));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
