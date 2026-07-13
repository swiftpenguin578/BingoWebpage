using System.Globalization;
using System.Text;
using Bingo.Application.Signups;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.TestData;

public sealed class LegacyTestSignupImporter(HttpClient httpClient, ApplicationDbContext db, ISignupService signupService)
{
    private const string PublishedCsvUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vT5q5hEHuhhgQeXQq3TWl_lNqlLyupO9QoVIx3SsSokisSwqVTUK0BZKu1ck3r_heEh2gLXu9ErJrFj/pub?output=csv";

    public async Task<ImportResult> ImportAsync(Guid eventId, CancellationToken ct)
    {
        var bingoEvent = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId, ct);
        if (bingoEvent is null) throw new InvalidOperationException("Event not found.");
        if (bingoEvent.DraftLocked) throw new InvalidOperationException("Test signups cannot be added after the draft has started.");

        var csv = await httpClient.GetStringAsync(PublishedCsvUrl, ct);
        var records = ParseCsv(csv);
        if (records.Count < 2) throw new InvalidOperationException("The published test signup sheet is empty.");
        var headers = records[0].Select((value, index) => (Name: value.Trim(), Index: index)).ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);
        var requiredAnswers = await db.SignupQuestions.AsNoTracking().Where(x => x.EventId == eventId && x.Active && x.Required).ToDictionaryAsync(x => x.Id, _ => "Imported test data", ct);
        var imported = 0; var waiting = 0; var skipped = new List<string>();

        foreach (var record in records.Skip(1))
        {
            var name = Get(record, headers, "In game Name");
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!TryParseEhb(name, Get(record, headers, "EHB - ses inde på Wiseoldman"), out var ehb)) { skipped.Add($"{name}: invalid EHB"); continue; }
            var result = await signupService.SignUpAsync(new SignupRequest(eventId, name, ehb, NullIfEmpty(Get(record, headers, "Hvad hedder din 2. Account")), null, NullIfEmpty(Get(record, headers, "Kommentarer")), false, null, requiredAnswers, SignupSource.CsvImport, true), ct);
            if (!result.Succeeded || result.ParticipantId is null) { skipped.Add($"{name}: {result.Error}"); continue; }
            imported++; if (result.Status == SignupStatus.WaitingList) waiting++;
            if (string.Equals(Get(record, headers, "Betalt buyin"), "x", StringComparison.OrdinalIgnoreCase))
            {
                var participant = await db.EventParticipants.SingleAsync(x => x.Id == result.ParticipantId, ct); participant.SetPaymentStatus(PaymentStatus.Paid); await db.SaveChangesAsync(ct);
            }
        }
        return new ImportResult(imported, waiting, skipped);
    }

    public static bool TryParseEhb(string name, string rawValue, out decimal ehb)
    {
        var knownValue = name.Trim().ToUpperInvariant() switch { "W OLLES" => 311m, "DETONED" => 527m, "BOTF" => 225m, _ => (decimal?)null };
        if (knownValue is not null) { ehb = knownValue.Value; return true; }
        return decimal.TryParse(rawValue.Replace(",", string.Empty, StringComparison.Ordinal).Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out ehb);
    }

    private static string Get(List<string> row, Dictionary<string, int> headers, string header) => headers.TryGetValue(header, out var index) && index < row.Count ? row[index].Trim() : string.Empty;
    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static List<List<string>> ParseCsv(string text)
    {
        var records = new List<List<string>>(); var row = new List<string>(); var field = new StringBuilder(); var quoted = false;
        for (var i = 0; i < text.Length; i++) { var c = text[i]; if (c == '"') { if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else quoted = !quoted; } else if (c == ',' && !quoted) { row.Add(field.ToString()); field.Clear(); } else if ((c == '\n' || c == '\r') && !quoted) { if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; row.Add(field.ToString()); field.Clear(); if (row.Any(value => value.Length > 0)) records.Add(row); row = []; } else field.Append(c); }
        row.Add(field.ToString()); if (row.Any(value => value.Length > 0)) records.Add(row); return records;
    }
}

public sealed record ImportResult(int Imported, int WaitingListed, IReadOnlyList<string> Skipped);
