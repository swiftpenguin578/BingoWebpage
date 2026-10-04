using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Access;
using Bingo.Domain.Auditing;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Audit;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class IndexModel(ApplicationDbContext dbContext) : PageModel
{
    private const int PageSize = 25;
    private static readonly string[] ActionAreas = ["account.", "event.", "catalogue.", "board.", "team.", "draft.", "submission."];

    [BindProperty(SupportsGet = true), StringLength(100)]
    public string? Action { get; set; }

    [BindProperty(SupportsGet = true), StringLength(100)]
    public string? Actor { get; set; }
    [BindProperty(SupportsGet = true), StringLength(100)] public string? Entity { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? EventId { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;

    public string? FromQuery => From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public string? ToQuery => To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public IReadOnlyList<AuditEntry> Entries { get; private set; } = [];
    public bool HasNextPage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        PageNumber = NormalizePageNumber();
        if (PageContext?.HttpContext?.Request.Query.ContainsKey("pageNumber") == true)
            ModelState.Remove(nameof(PageNumber));
        if (!ModelState.IsValid)
        {
            Entries = [];
            HasNextPage = false;
            return;
        }

        var query = dbContext.AuditEntries.AsNoTracking();
        var action = Action?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(action))
        {
            var areaPrefix = action.Length > 0 && action[^1] == '.' ? action : action + ".";
            query = ActionAreas.Contains(areaPrefix, StringComparer.Ordinal)
                ? query.Where(entry => entry.Action.StartsWith(areaPrefix))
                : query.Where(entry => entry.Action == action);
        }

        var actor = Actor?.Trim();
        if (!string.IsNullOrWhiteSpace(actor)) query = query.Where(entry => entry.ActorUsername.Contains(actor));
        var entity = Entity?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(entity)) query = query.Where(entry => entry.TargetType == entity);
        if (EventId is not null) query = query.Where(entry => entry.EventId == EventId);
        if (From is { } from && To is { } to && from > to)
        {
            Entries = [];
            HasNextPage = false;
            return;
        }

        if (From is { } start) query = query.Where(entry => entry.OccurredAt >= DisplayMidnightUtc(start));
        if (To is { } end)
        {
            try
            {
                query = query.Where(entry => entry.OccurredAt < DisplayMidnightUtc(end.AddDays(1)));
            }
            catch (ArgumentOutOfRangeException)
            {
                Entries = [];
                HasNextPage = false;
                return;
            }
        }

        var rows = await query.OrderByDescending(entry => entry.OccurredAt).ThenByDescending(entry => entry.Id)
            .Skip((PageNumber - 1) * PageSize).Take(PageSize + 1).ToListAsync(cancellationToken);
        HasNextPage = rows.Count > PageSize;
        Entries = rows.Take(PageSize).ToList();
    }

    private int NormalizePageNumber()
    {
        const int maxPage = int.MaxValue / PageSize + 1;
        var query = PageContext?.HttpContext?.Request.Query;
        if (query is not null && query.TryGetValue("pageNumber", out var raw))
        {
            var value = raw.ToString();
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                && parsed is >= 1 and <= maxPage
                ? parsed
                : 1;
        }

        return Math.Clamp(PageNumber, 1, maxPage);
    }

    private static DateTimeOffset DisplayMidnightUtc(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        TimeZoneInfo timezone;
        try
        {
            timezone = TimeZoneInfo.FindSystemTimeZoneById(DateTimePresentation.DefaultTimezoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            timezone = TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            timezone = TimeZoneInfo.Utc;
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timezone));
    }
}
