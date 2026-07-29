using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bingo.Domain.Auditing;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Bingo.Web.Teams;

/// <summary>Small, event-scoped import boundary for invited pre-formed rosters. It deliberately is not a signup importer.</summary>
public sealed class PreformedRosterCsvImportService(ApplicationDbContext db, EventParticipantCharacterService characters, IMemoryCache cache, TimeProvider time)
{
    public const int MaxBytes = 1_048_576;
    public const int MaxRows = 200;
    public const int MaxColumns = 12;
    private const string Prefix = "slice5-preformed-csv:";
    private static readonly ConcurrentDictionary<string, byte> Consumed = new();

    public async Task<Preview> PreviewAsync(Guid actorId, Guid eventId, Guid teamId, Stream content, CancellationToken ct)
    {
        using var memory = new MemoryStream();
        await content.CopyToAsync(memory, ct);
        if (memory.Length > MaxBytes) return Preview.Invalid("The CSV file is larger than 1 MB.");
        var text = new UTF8Encoding(false, true).GetString(memory.ToArray()).TrimStart('\uFEFF');
        var parsed = Parse(text);
        if (parsed.Errors.Count > 0) return new Preview(null, [], parsed.Errors);
        if (parsed.Rows.Count > MaxRows) return Preview.Invalid($"The CSV has more than {MaxRows} roster rows.");
        if (parsed.Headers.Count > MaxColumns) return Preview.Invalid($"The CSV has more than {MaxColumns} columns.");
        if (!await IsEligibleTeamAsync(eventId, teamId, ct)) return Preview.Invalid("Choose an active Pre-formed team before importing a roster.");

        var errors = ValidateShape(parsed);
        var allNames = parsed.Rows.SelectMany(row => row.Accounts).ToList();
        var duplicateNames = allNames.GroupBy(Normalize, StringComparer.Ordinal).Where(group => group.Key.Length == 0 || group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var row in parsed.Rows)
            foreach (var account in row.Accounts)
                if (duplicateNames.Contains(Normalize(account))) errors.Add(new RowError(row.Number, "Each OSRS account may appear only once in this import."));

        var normalized = allNames.Select(Normalize).Where(value => value.Length > 0).Distinct().ToList();
        var reserved = await (from assignment in db.EventParticipantCharacters
                              join character in db.OsrsCharacters on assignment.OsrsCharacterId equals character.Id
                              where assignment.EventId == eventId && assignment.ReleasedAt == null && normalized.Contains(character.NormalizedName)
                              select character.NormalizedName).ToListAsync(ct);
        foreach (var row in parsed.Rows.Where(row => row.Accounts.Any(account => reserved.Contains(Normalize(account)))))
            errors.Add(new RowError(row.Number, "An account is already reserved by a current event participant."));
        if (errors.Count > 0) return new Preview(null, parsed.Rows, errors.Distinct().ToList());

        var state = new State(actorId, eventId, teamId, parsed.Rows, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), time.GetUtcNow().AddMinutes(10));
        cache.Set(Prefix + state.Nonce, state, state.ExpiresAt - time.GetUtcNow());
        return new Preview(state.Nonce, state.Rows, []);
    }

    public async Task<ApplyResult> ApplyAsync(Guid actorId, string actorName, Guid eventId, Guid teamId, string nonce, CancellationToken ct)
    {
        if (!cache.TryGetValue<State>(Prefix + nonce, out var state) || state is null || state.ExpiresAt <= time.GetUtcNow()) return ApplyResult.Failed("This preview has expired. Upload the CSV again.");
        if (state.ActorId != actorId || state.EventId != eventId || state.TeamId != teamId) return ApplyResult.Failed("This preview does not match the selected team or administrator.");
        if (!Consumed.TryAdd(nonce, 0)) return ApplyResult.Failed("This preview was already applied or is being applied.");
        cache.Remove(Prefix + nonce); // single-use before the transaction; a raced/replayed request can never create a second roster.
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            if (!await IsEligibleTeamAsync(eventId, teamId, ct)) return ApplyResult.Failed("The selected Pre-formed team is no longer available for roster import.");
            var all = state.Rows.SelectMany(row => row.Accounts).Select(Normalize).Distinct().ToList();
            var conflict = await (from assignment in db.EventParticipantCharacters
                                  join character in db.OsrsCharacters on assignment.OsrsCharacterId equals character.Id
                                  where assignment.EventId == eventId && assignment.ReleasedAt == null && all.Contains(character.NormalizedName)
                                  select character.NormalizedName).AnyAsync(ct);
            if (conflict) return ApplyResult.Failed("A roster account was reserved by another change. Nothing was imported.");
            var sequence = (await db.EventParticipants.Where(x => x.EventId == eventId).MaxAsync(x => (long?)x.SignupSequence, ct) ?? 0) + 1;
            foreach (var row in state.Rows)
            {
                var participant = new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, sequence++, time.GetUtcNow(), SignupSource.AdminCreated);
                db.EventParticipants.Add(participant);
                await characters.AssignExternalRosterCharactersAsync(participant, row.Accounts[0], row.Ehb, row.Accounts.Skip(1).ToList(), EhbSource.AdminCorrection, actorId, ct);
                var membership = new TeamMembership(Guid.NewGuid(), teamId, participant.Id, TeamMembershipRole.Participant, time.GetUtcNow(), null, "CSV pre-formed roster import");
                membership.SetSource(TeamMembershipSource.PreformedCsv);
                db.TeamMemberships.Add(membership);
            }
            db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), time.GetUtcNow(), actorId, actorName, "team.preformed_roster_csv_imported", "team", teamId.ToString(), $"Imported {state.Rows.Count} roster members.", eventId));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return ApplyResult.Successful(state.Rows.Count);
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
        {
            await transaction.RollbackAsync(ct);
            return ApplyResult.Failed("The roster changed while this import was being applied. Nothing was imported.");
        }
    }

    public static byte[] Template() => new UTF8Encoding(false).GetBytes("Account,EHB\r\n");
    private async Task<bool> IsEligibleTeamAsync(Guid eventId, Guid teamId, CancellationToken ct) => await db.Teams.AnyAsync(team => team.Id == teamId && team.EventId == eventId && team.Active && team.FormationType == TeamFormationType.Preformed, ct) && !await db.Events.AnyAsync(ev => ev.Id == eventId && ev.ActualStartedAt != null, ct);
    private static List<RowError> ValidateShape(Parsed parsed)
    {
        var errors = new List<RowError>();
        if (parsed.Headers.Count < 2 || parsed.Headers[0] != "Account" || parsed.Headers[1] != "EHB" || parsed.Headers.Skip(2).Any(header => header != "Account")) return [new RowError(1, "Headers must be Account,EHB followed only by optional Account columns.")];
        foreach (var row in parsed.Rows)
        {
            if (row.Accounts.Count == 0 || string.IsNullOrWhiteSpace(row.Accounts[0])) errors.Add(new RowError(row.Number, "Primary Account is required."));
            if (row.Ehb < 0 || row.Ehb > 1_000_000m) errors.Add(new RowError(row.Number, "EHB must be a non-negative number no greater than 1,000,000."));
        }
        return errors;
    }
    private static Parsed Parse(string text)
    {
        var delimiter = DetectDelimiter(text, out var delimiterError);
        if (delimiterError is not null) return new Parsed([], [], [new RowError(1, delimiterError)]);
        var records = ReadCsv(text, delimiter, out var error); if (error is not null || records.Count == 0) return new Parsed([], [], [new RowError(1, error ?? "CSV must include a header row.")]);
        var headers = records[0]; var rows = new List<Row>(); var errors = new List<RowError>();
        for (var i = 1; i < records.Count; i++)
        {
            var fields = records[i]; if (fields.All(string.IsNullOrWhiteSpace)) continue;
            if (fields.Count != headers.Count) { errors.Add(new RowError(i + 1, "The row does not match the header column count.")); continue; }
            if (!TryParseEhb(fields.ElementAtOrDefault(1), delimiter, out var ehb)) { errors.Add(new RowError(i + 1, delimiter == ';' ? "EHB must be a valid number using a decimal comma or decimal point." : "EHB must be a valid number using a decimal point.")); continue; }
            rows.Add(new Row(i + 1, fields.Where((_, index) => index != 1).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToList(), ehb));
        }
        return new Parsed(headers, rows, errors);
    }
    private static char DetectDelimiter(string value, out string? error)
    {
        error = null;
        var header = value.Split('\n', 2)[0].TrimEnd('\r');
        var comma = header.Count(c => c == ','); var semicolon = header.Count(c => c == ';');
        if ((comma == 0 && semicolon == 0) || (comma > 0 && semicolon > 0)) { error = "The header must consistently use either commas or semicolons as its delimiter."; return ','; }
        return semicolon > 0 ? ';' : ',';
    }
    private static bool TryParseEhb(string? value, char delimiter, out decimal ehb)
    {
        if (delimiter == ',') return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out ehb);
        return decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out ehb)
            || decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.GetCultureInfo("da-DK"), out ehb);
    }
    private static List<List<string>> ReadCsv(string value, char delimiter, out string? error)
    {
        error = null; var records = new List<List<string>>(); var row = new List<string>(); var field = new StringBuilder(); var quoted = false;
        for (var index = 0; index < value.Length; index++) { var c = value[index]; if (quoted) { if (c == '"' && index + 1 < value.Length && value[index + 1] == '"') { field.Append(c); index++; } else if (c == '"') quoted = false; else field.Append(c); } else if (c == '"') { if (field.Length != 0) { error = "A quote may only begin a CSV field."; return records; } quoted = true; } else if (c == delimiter) { row.Add(field.ToString()); field.Clear(); } else if (c == '\n') { row.Add(field.ToString().TrimEnd('\r')); field.Clear(); records.Add(row); row = []; } else field.Append(c); }
        if (quoted) { error = "The CSV contains an unclosed quoted value."; return records; }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); records.Add(row); }
        return records;
    }
    private static string Normalize(string value) => SignupService.NormalizeAccountName(value);
    public sealed record Row(int Number, IReadOnlyList<string> Accounts, decimal Ehb);
    public sealed record RowError(int Number, string Message);
    public sealed record Preview(string? Nonce, IReadOnlyList<Row> Rows, IReadOnlyList<RowError> Errors) { public bool IsValid => Nonce is not null && Errors.Count == 0; public static Preview Invalid(string message) => new(null, [], [new RowError(1, message)]); }
    public sealed record ApplyResult(bool Succeeded, string Message, int Imported) { public static ApplyResult Failed(string message) => new(false, message, 0); public static ApplyResult Successful(int imported) => new(true, $"Imported {imported} roster members.", imported); }
    private sealed record Parsed(IReadOnlyList<string> Headers, IReadOnlyList<Row> Rows, IReadOnlyList<RowError> Errors);
    private sealed record State(Guid ActorId, Guid EventId, Guid TeamId, IReadOnlyList<Row> Rows, string Nonce, DateTimeOffset ExpiresAt);
}
