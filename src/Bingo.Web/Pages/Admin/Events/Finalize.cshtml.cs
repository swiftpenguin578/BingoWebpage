using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Auditing;
using Bingo.Application.Events;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

public sealed class FinalizeModel(IEventFinalizationService finalization,ApplicationDbContext db,IAuditWriter audit,TimeProvider time):PageModel
{
    public FinalReviewReadiness Readiness{get;private set;}=null!;
    [BindProperty,StringLength(2000)]public string?Reason{get;set;}
    public async Task<IActionResult>OnGetAsync(Guid id,CancellationToken ct){var value=await finalization.GetReadinessAsync(id,ct);if(value is null)return NotFound();Readiness=value;return Page();}
    public async Task<IActionResult>OnPostResolveAsync(Guid id,string blockerKey,CancellationToken ct)=>await Run(id,async()=>{await finalization.ResolveBlockerAsync(id,blockerKey,Reason??string.Empty,AdminId,ct);await Audit("event.final_review_overridden",id,$"{blockerKey}: {Reason}",ct);},ct);
    public async Task<IActionResult>OnPostCorrectCompletionAsync(Guid id,Guid teamId,string?correctedAtLocal,CancellationToken ct)=>await Run(id,async()=>
    {
        if(!DateTime.TryParseExact(correctedAtLocal,["yyyy-MM-dd HH:mm","yyyy-MM-dd HH:mm:ss"],CultureInfo.InvariantCulture,DateTimeStyles.None,out var entered))
            throw new InvalidOperationException("Choose a valid completion date and time.");
        var timezoneId=await db.Events.AsNoTracking().Where(x=>x.Id==id).Select(x=>x.Timezone).SingleAsync(ct);
        var timezone=TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        var local=DateTime.SpecifyKind(entered,DateTimeKind.Unspecified);
        if(timezone.IsInvalidTime(local))throw new InvalidOperationException("That local time does not exist because the clocks change at that time.");
        var correctedAt=new DateTimeOffset(local,timezone.GetUtcOffset(local));
        await finalization.CorrectCompletionAsync(id,teamId,correctedAt,Reason??string.Empty,AdminId,ct);
        await Audit("event.completion_time_corrected",id,$"{teamId}: {correctedAt:O}; {Reason}",ct);
        TempData["StatusMessage"]=$"Completion time corrected to {correctedAt.ToLocalTime():g}.";
    },ct);
    public async Task<IActionResult>OnPostFinalizeAsync(Guid id,CancellationToken ct)=>await Run(id,async()=>{var from=await State(id,ct);await finalization.FinalizeAsync(id,AdminId,ct);await Transition(id,from,EventState.Finalized,"Official results finalized",ct);await Audit("event.finalized",id,"Official placements snapshotted and published",ct);},ct);
    public async Task<IActionResult>OnPostUnfinalizeAsync(Guid id,CancellationToken ct)=>await Run(id,async()=>{if(string.IsNullOrWhiteSpace(Reason))throw new InvalidOperationException("A reason is required.");var from=await State(id,ct);await finalization.UnfinalizeAsync(id,Reason,AdminId,ct);await Transition(id,from,EventState.AwaitingFinalReview,Reason,ct);await Audit("event.unfinalized",id,Reason,ct);},ct);
    public async Task<IActionResult>OnPostArchiveAsync(Guid id,CancellationToken ct)=>await Run(id,async()=>{var from=await State(id,ct);await finalization.ArchiveAsync(id,AdminId,ct);await Transition(id,from,EventState.Archived,"Archived",ct);await Audit("event.archived",id,"Official event archived",ct);},ct);
    private async Task<IActionResult>Run(Guid id,Func<Task>action,CancellationToken ct){try{await action();}catch(InvalidOperationException ex){TempData["StatusMessage"]=ex.Message;}return RedirectToPage(new{id});}
    private Guid AdminId=>User.GetAccountId()!.Value;
    private async Task<EventState>State(Guid id,CancellationToken ct)=>await db.Events.AsNoTracking().Where(x=>x.Id==id).Select(x=>x.State).SingleAsync(ct);
    private async Task Transition(Guid id,EventState from,EventState to,string?reason,CancellationToken ct){db.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(),id,from,to,AdminId,time.GetUtcNow(),reason));await db.SaveChangesAsync(ct);}
    private Task Audit(string action,Guid id,string?details,CancellationToken ct)=>audit.WriteAsync(AdminId,User.Identity!.Name!,action,"event",id.ToString(),details,ct);
}
