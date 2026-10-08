using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bingo.Domain.Evidence;
using Bingo.Web.Pages.Admin.Audit;
using Bingo.Web.UI;

namespace Bingo.BrowserTests;

/// <summary>
/// S12 (T1): every recorded action key has a readable English label and a Danish entry, and
/// belongs to exactly one area (S11). "Recorded" keys are found by scanning the application source
/// for string literals with an audit area prefix; the few literals with such a prefix that are not
/// audit actions are listed below with their reason, so a new unlabelled key fails this test.
/// </summary>
public sealed class AuditLabelCompletenessTests
{
    private static readonly string[] AuditPrefixes = ["account", "event", "catalogue", "board", "team", "draft", "submission", "participant", "roster", "signup", "signup_question", "signup_cocaptain", "evidence_code", "historical_import"];

    // Audit actions composed with string interpolation (T1 review M1): the literal scan cannot see
    // them, so every interpolated template and the keys it can produce are listed here.
    private static readonly Dictionary<string, string[]> ComposedKeys = new(StringComparer.Ordinal)
    {
        ["$\"account.discord_{action}\""] = ["account.discord_linked", "account.discord_replaced"],
        ["$\"{action}.wom_sync\""] = ["roster.finalized_added.wom_sync", "roster.finalized_removed.wom_sync"]
    };

    // Required-account validation operation names (WiseOldManAccountValidationRequest), not audit actions.
    private static readonly HashSet<string> NotAuditKeys = ["participant.create", "participant.edit", "participant.replacement", "participant.restore", "participant.rejoin", "signup.create", "signup.edit"];

    [Fact]
    public void EveryRecordedActionKeyHasAnEnglishAndDanishLabelAndOneArea()
    {
        var root = FindRepositoryRoot();
        var pattern = new Regex("\"(?<key>(?:" + string.Join('|', AuditPrefixes) + @")\.[a-z0-9_]*[a-z0-9])""");
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
            foreach (Match match in pattern.Matches(File.ReadAllText(file)))
                keys.Add(match.Groups["key"].Value);
        // Guard: an interpolated string that starts with an audit area, or ends ".suffix" after a
        // placeholder, is a composed audit action and must be listed in ComposedKeys.
        var interpolated = new Regex(@"\$""(?:(?:" + string.Join('|', AuditPrefixes) + @")\.[^""]*\{[^""]*|\{[^}""]+\}\.[a-z_]+)""");
        var unlisted = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
            foreach (Match match in interpolated.Matches(File.ReadAllText(file)))
                if (!ComposedKeys.ContainsKey(match.Value)) unlisted.Add($"{Path.GetFileName(file)}: {match.Value}");
        Assert.True(unlisted.Count == 0, "Interpolated audit action strings not listed in ComposedKeys:\n" + string.Join('\n', unlisted));
        foreach (var composed in ComposedKeys.Values) keys.UnionWith(composed);
        foreach (var action in Enum.GetValues<ReviewActionType>()) keys.Add(AuditPresenter.ActionKey(action));
        keys.Add(AuditPresenter.ActionKey((ReviewActionType)(-1)));
        keys.ExceptWith(NotAuditKeys);
        Assert.True(keys.Count > 150, "The scan found too few keys to be meaningful.");

        var danish = XDocument.Load(Path.Combine(root, "src", "Bingo.Web", "Resources", "AuditResource.da.resx"))
            .Descendants("data").ToDictionary(item => item.Attribute("name")!.Value, item => item.Element("value")?.Value);
        var problems = new List<string>();
        foreach (var key in keys)
        {
            if (!AuditPresenter.ActionLabels.TryGetValue(key, out var label)) { problems.Add($"{key}: no label"); continue; }
            if (!danish.TryGetValue(label, out var value) || string.IsNullOrWhiteSpace(value)) problems.Add($"{key}: no Danish entry for “{label}”");
            var areas = AuditAreas.All.Count(area => area.Contains(key));
            if (areas != 1) problems.Add($"{key}: in {areas} areas");
        }
        foreach (var (key, label) in AuditPresenter.ActionLabels)
            if (!danish.ContainsKey(label)) problems.Add($"{key}: no Danish entry for “{label}”");
        foreach (var label in AuditPresenter.TargetLabels.Values)
            if (!danish.ContainsKey(label)) problems.Add($"record type “{label}”: no Danish entry");
        Assert.True(problems.Count == 0, "Audit label problems:\n" + string.Join('\n', problems));
        Assert.Single(Regex.Matches(File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "UI", "AuditPresenter.cs")), "\"Recorded administrative action\""));
    }

    [Fact]
    public void AreaTokensMatchTheirKeysAndTheQ5Moves()
    {
        Assert.Equal(["account.", "event.", "signup.", "participant.", "team.", "draft.", "board.", "submission.", "catalogue."], AuditAreas.All.Select(area => area.Token));
        Assert.Equal("participant.", AuditAreas.For("team.member_moved")!.Token);
        Assert.Equal("participant.", AuditAreas.For("roster.finalized_removed")!.Token);
        Assert.Equal("team.", AuditAreas.For("team.created")!.Token);
        Assert.Equal("signup.", AuditAreas.For("event.capacity_increased")!.Token);
        Assert.Equal("signup.", AuditAreas.For("signup_cocaptain.enabled")!.Token);
        Assert.Equal("event.", AuditAreas.For("event.started")!.Token);
        Assert.Equal("participant.", AuditAreas.For("roster.finalized_added.wom_sync")!.Token);
        Assert.Equal("participant.", AuditAreas.For("roster.finalized_removed.wom_sync")!.Token);
        Assert.Equal("account.", AuditAreas.For("account.discord_replaced")!.Token);
        Assert.True(Bingo.Web.Pages.Admin.Audit.IndexModel.IsActionKey("roster.finalized_added.wom_sync"));
        Assert.False(Bingo.Web.Pages.Admin.Audit.IndexModel.IsActionKey("a.b.c.d"));
        Assert.False(Bingo.Web.Pages.Admin.Audit.IndexModel.IsActionKey("a..b"));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
