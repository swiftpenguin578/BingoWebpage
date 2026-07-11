using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Signups;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class CsvModel(ApplicationDbContext dbContext, ISignupService signupService, IAuditWriter auditWriter) : PageModel
{
    private static readonly string[] RequiredHeaders = ["PrimaryAccountName", "EHB"];
    private static readonly string[] TemplateHeaders = ["PrimaryAccountName", "EHB", "SecondAccountName", "DiscordIdentity", "Comments", "CaptainVolunteer"];
    [BindProperty, Display(Name = "CSV file")] public IFormFile? Upload { get; set; }
    [BindProperty] public string CsvContent { get; set; } = string.Empty;
    public IReadOnlyList<CsvRow> PreviewRows { get; private set; } = []; public IReadOnlyList<string> Warnings { get; private set; } = [];
    public IActionResult OnGetTemplate() { var sample = "PrimaryAccountName,EHB,SecondAccountName,DiscordIdentity,Comments,CaptainVolunteer\nExample Player,1250,,,Available all week,No\n"; return File(Encoding.UTF8.GetBytes(sample), "text/csv", "bingo-signup-template.csv"); }
    public async Task<IActionResult> OnPostPreviewAsync(Guid id, CancellationToken ct)
    {
        if (!await dbContext.Events.AnyAsync(e => e.Id == id, ct)) return NotFound(); if (Upload is null || Upload.Length == 0) { ModelState.AddModelError(string.Empty, "Choose a CSV file."); return Page(); }
        if (Upload.Length > 200_000) { ModelState.AddModelError(string.Empty, "CSV files are limited to 200 KB."); return Page(); }
        using var reader = new StreamReader(Upload.OpenReadStream(), Encoding.UTF8, true); CsvContent = await reader.ReadToEndAsync(ct); ParsePreview(); return Page();
    }
    public async Task<IActionResult> OnPostImportAsync(Guid id, CancellationToken ct)
    {
        ParsePreview(); if (!ModelState.IsValid || PreviewRows.Count == 0) return Page(); var imported = 0; var failures = new List<string>();
        foreach (var row in PreviewRows) { var result = await signupService.SignUpAsync(new SignupRequest(id, row.PrimaryAccountName, row.Ehb, row.SecondAccountName, row.DiscordIdentity, row.Comments, row.CaptainVolunteer, null, new Dictionary<Guid, string>(), SignupSource.CsvImport, true), ct); if (result.Succeeded) imported++; else failures.Add($"{row.PrimaryAccountName}: {result.Error}"); }
        await auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "participants.csv_imported", "event", id.ToString(), $"Imported: {imported}; failed: {failures.Count}", ct); TempData["StatusMessage"] = $"Imported {imported} participant(s)." + (failures.Count > 0 ? $" {failures.Count} failed: {string.Join("; ", failures)}" : string.Empty); return RedirectToPage("Manage", new { id });
    }
    private void ParsePreview()
    {
        if (string.IsNullOrWhiteSpace(CsvContent)) { ModelState.AddModelError(string.Empty, "CSV content is empty."); return; }
        var records = ParseCsv(CsvContent); if (records.Count < 2) { ModelState.AddModelError(string.Empty, "The CSV must contain a header and at least one participant."); return; }
        var headers = records[0]; foreach (var required in RequiredHeaders) if (!headers.Contains(required, StringComparer.OrdinalIgnoreCase)) ModelState.AddModelError(string.Empty, $"Missing required column: {required}."); if (!ModelState.IsValid) return;
        var map = headers.Select((value, index) => (value, index)).ToDictionary(x => x.value, x => x.index, StringComparer.OrdinalIgnoreCase); Warnings = headers.Where(h => !TemplateHeaders.Contains(h, StringComparer.OrdinalIgnoreCase)).Select(h => $"Unknown column '{h}' will be ignored.").ToList(); var rows = new List<CsvRow>();
        for (var i = 1; i < records.Count; i++) { var values = records[i]; var name = Get(values, map, "PrimaryAccountName"); if (string.IsNullOrWhiteSpace(name)) continue; if (!decimal.TryParse(Get(values, map, "EHB"), NumberStyles.Number, CultureInfo.InvariantCulture, out var ehb)) { ModelState.AddModelError(string.Empty, $"Row {i + 1}: EHB must be a number."); continue; } rows.Add(new CsvRow(name, ehb, Get(values, map, "SecondAccountName"), Get(values, map, "DiscordIdentity"), Get(values, map, "Comments"), IsYes(Get(values, map, "CaptainVolunteer")))); }
        PreviewRows = rows;
    }
    private static string? Get(List<string> values, Dictionary<string, int> map, string key) => map.TryGetValue(key, out var index) && index < values.Count ? NullIfEmpty(values[index]) : null;
    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim(); private static bool IsYes(string? value) => string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
    private static List<List<string>> ParseCsv(string text) { var records = new List<List<string>>(); var row = new List<string>(); var field = new StringBuilder(); var quoted = false; for (var i = 0; i < text.Length; i++) { var c = text[i]; if (c == '"') { if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else quoted = !quoted; } else if (c == ',' && !quoted) { row.Add(field.ToString()); field.Clear(); } else if ((c == '\n' || c == '\r') && !quoted) { if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; row.Add(field.ToString()); field.Clear(); if (row.Any(v => v.Length > 0)) records.Add(row); row = []; } else field.Append(c); } row.Add(field.ToString()); if (row.Any(v => v.Length > 0)) records.Add(row); return records; }
    public sealed record CsvRow(string PrimaryAccountName, decimal Ehb, string? SecondAccountName, string? DiscordIdentity, string? Comments, bool CaptainVolunteer);
}
