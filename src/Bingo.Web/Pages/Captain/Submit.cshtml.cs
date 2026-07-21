using System.ComponentModel.DataAnnotations;
using Bingo.Application.Access;
using Bingo.Application.Evidence;
using Bingo.Domain.Boards;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Captain;

[Authorize(Policy=AuthorizationPolicies.CaptainFullAccess)]
[RequestSizeLimit(11*1024*1024)]
public sealed class SubmitModel(ApplicationDbContext db,ISubmissionService submissions) : PageModel
{
    public TileDetails Tile { get; private set; }=null!;public IReadOnlyList<PlayerView> Players{get;private set;}=[];public IReadOnlyList<RequirementView> Requirements{get;private set;}=[];
    [BindProperty]public SubmissionInput Input{get;set;}=new();
    public async Task<IActionResult> OnGetAsync(Guid tileId,CancellationToken ct)=>await Load(tileId,ct)?Page():NotFound();
    public async Task<IActionResult> OnPostAsync(Guid tileId,CancellationToken ct)
    {
        if(!await Load(tileId,ct))return NotFound();if(Input.Evidence is null)ModelState.AddModelError("Input.Evidence","Paste, drag, or choose one screenshot.");if(!ModelState.IsValid)return Page();
        try{await using var stream=Input.Evidence!.OpenReadStream();var result=await submissions.CreateAsync(new(User.GetAccountId()!.Value,User.GetEventId()!.Value,User.GetTeamId()!.Value,tileId,Input.RequirementId,Input.DropSnapshotId,Input.CreditedParticipantId,Input.ClaimedWeight,Input.Note,Input.Evidence.FileName,stream,Input.RequestPublicPrivacy),ct);TempData["StatusMessage"]="Evidence submitted for review.";return RedirectToPage("Submission",new{id=result.SubmissionId});}
        catch(InvalidOperationException ex){ModelState.AddModelError(string.Empty,ex.Message);return Page();}
    }
    private async Task<bool> Load(Guid tileId,CancellationToken ct)
    {
        var eventId=User.GetEventId()!.Value;var teamId=User.GetTeamId()!.Value;var tile=await(from t in db.BoardTiles.AsNoTracking()join b in db.Boards on t.BoardId equals b.Id where t.Id==tileId&&b.EventId==eventId&&b.State==BoardState.Published select t).SingleOrDefaultAsync(ct);if(tile is null)return false;Tile=new(tile.Id,tile.NameSnapshot,tile.DescriptionSnapshot,tile.EvidenceInstructionsSnapshot);
        Players=await(from m in db.TeamMemberships.AsNoTracking()join p in db.EventParticipants on m.EventParticipantId equals p.Id where m.TeamId==teamId&&m.LeftAt==null orderby p.PrimaryAccountName select new PlayerView(p.Id,p.PrimaryAccountName)).ToListAsync(ct);
        var requirements=await db.BoardRequirementSnapshots.AsNoTracking().Where(x=>x.BoardTileId==tileId).OrderBy(x=>x.Position).ToListAsync(ct);var ids=requirements.Select(x=>x.Id).ToList();var drops=await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x=>ids.Contains(x.RequirementId)).OrderBy(x=>x.BossName).ThenBy(x=>x.ItemName).ToListAsync(ct);var contributions=await db.SubmissionContributions.AsNoTracking().Where(x=>x.TeamId==teamId&&ids.Contains(x.RequirementId)&&x.ReversedAt==null).ToListAsync(ct);var contributed=contributions.GroupBy(x=>x.RequirementId).ToDictionary(x=>x.Key,x=>x.Sum(y=>y.Amount));var contributedByDrop=contributions.Where(x=>x.DropSnapshotId is not null).GroupBy(x=>x.DropSnapshotId!.Value).ToDictionary(x=>x.Key,x=>x.Sum(y=>y.Amount));Requirements=requirements.Select(x=>new RequirementView(x.Id,x.Description,x.TargetContribution,Math.Max(0,x.TargetContribution-contributed.GetValueOrDefault(x.Id)),x.ManualObjective,x.DuplicatesAllowed,drops.Where(d=>d.RequirementId==x.Id&&(d.MaximumContribution??(x.DuplicatesAllowed?int.MaxValue:1))>contributedByDrop.GetValueOrDefault(d.Id)).Select(d=>new DropView(d.Id,d.BossName,d.ItemName,d.DisplayRate,d.CreditedWeight)).ToList())).ToList();return true;
    }
    public sealed class SubmissionInput{[Required]public Guid RequirementId{get;set;}public Guid? DropSnapshotId{get;set;}[Required]public Guid CreditedParticipantId{get;set;}[Range(1,10000)]public int ClaimedWeight{get;set;}=1;[StringLength(4000)]public string? Note{get;set;}public bool RequestPublicPrivacy{get;set;}public IFormFile? Evidence{get;set;}}
    public sealed record TileDetails(Guid Id,string Name,string Description,string EvidenceInstructions);public sealed record PlayerView(Guid Id,string Name);public sealed record RequirementView(Guid Id,string Description,int Target,int Remaining,bool Manual,bool DuplicatesAllowed,IReadOnlyList<DropView>Drops);public sealed record DropView(Guid Id,string Boss,string Item,string Rate,int CreditedWeight);
}
