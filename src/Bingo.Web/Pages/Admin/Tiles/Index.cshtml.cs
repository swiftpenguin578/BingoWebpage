using System.ComponentModel.DataAnnotations;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Domain.Boards;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
namespace Bingo.Web.Pages.Admin.Tiles;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class IndexModel(ApplicationDbContext dbContext, IAuditWriter auditWriter) : PageModel
{
    [BindProperty] public TileInput Input { get; set; } = new(); public IReadOnlyList<TileRow> Tiles { get; private set; } = []; public Task OnGetAsync(CancellationToken ct) => Load(ct);
    public async Task<IActionResult> OnPostAsync(CancellationToken ct) { if (!ModelState.IsValid) { await Load(ct); return Page(); } var tile = new TileTemplate(Guid.NewGuid(), Input.Name.Trim(), Input.Description.Trim(), Input.ObjectiveType, Input.EvidenceInstructions.Trim(), Input.ManualEhb); dbContext.TileTemplates.Add(tile); await dbContext.SaveChangesAsync(ct); await auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "tile.created", "tile_template", tile.Id.ToString(), tile.Name, ct); return RedirectToPage("Requirements", new { id = tile.Id }); }
    public async Task<IActionResult> OnPostToggleAsync(Guid tileId, CancellationToken ct) { var tile = await dbContext.TileTemplates.SingleOrDefaultAsync(x => x.Id == tileId, ct); if (tile is null) return NotFound(); tile.SetActive(!tile.Active); await dbContext.SaveChangesAsync(ct); await auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "tile.toggled", "tile_template", tile.Id.ToString(), tile.Active.ToString(), ct); return RedirectToPage(); }
    private async Task Load(CancellationToken ct) => Tiles = await dbContext.TileTemplates.AsNoTracking().OrderByDescending(x => x.Active).ThenBy(x => x.Name).Select(x => new TileRow(x.Id, x.Name, x.ObjectiveType, x.ManualEhbOverride, x.Active)).ToListAsync(ct);
    public sealed class TileInput { [Required, StringLength(200)] public string Name { get; set; } = string.Empty; [Required, StringLength(4000)] public string Description { get; set; } = string.Empty; [Display(Name = "Objective type")] public ObjectiveType ObjectiveType { get; set; } [Required, StringLength(4000), Display(Name = "Evidence instructions")] public string EvidenceInstructions { get; set; } = string.Empty; [Range(0, 100000), Display(Name = "Manual EHB override (optional)")] public decimal? ManualEhb { get; set; } }
    public sealed record TileRow(Guid Id, string Name, ObjectiveType Type, decimal? Ehb, bool Active);
}
