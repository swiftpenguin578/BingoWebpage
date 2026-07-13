using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Signups;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Teams;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Bingo.Web.TestData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class ManageModel(ApplicationDbContext dbContext, ISignupService signupService, IAuditWriter auditWriter, TimeProvider timeProvider, LegacyTestSignupImporter testSignupImporter, IWebHostEnvironment environment) : PageModel
{
    public EventDetails? EventView { get; private set; }
    public IReadOnlyList<ParticipantRow> Participants { get; private set; } = [];
    public IReadOnlyList<EvidenceCodeRow> EvidenceCodes { get; private set; } = [];
    public bool IsDevelopment => environment.IsDevelopment();
    [BindProperty, Range(1, 10000), Display(Name = "New participant cap")] public int NewCap { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "New signup closing")] public DateTimeOffset NewSignupClosing { get; set; }
    [BindProperty, StringLength(1000), Display(Name = "Reason for reopening")] public string? StateReason { get; set; }
    [BindProperty, StringLength(100), Display(Name = "Evidence code")] public string? NewEvidenceCode { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "Activates at")] public DateTimeOffset? EvidenceCodeActivatesAt { get; set; }
    [BindProperty, StringLength(1000), Display(Name = "Code note")] public string? EvidenceCodeNote { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "Reopen until")] public DateTimeOffset? ReopenUntil { get; set; }
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) => await LoadAsync(id, ct) ? Page() : NotFound();
    public async Task<IActionResult> OnPostStateAsync(Guid id, EventState target, CancellationToken ct)
    {
        var item = await dbContext.Events.SingleOrDefaultAsync(e => e.Id == id, ct); if (item is null) return NotFound();
        if (item.DraftLocked && target == EventState.SignupOpen) { TempData["StatusMessage"] = "Signups cannot be reopened after the draft has started."; return RedirectToPage(new { id }); }
        var from = item.State;
        if (from == EventState.SignupClosed && target == EventState.SignupOpen && string.IsNullOrWhiteSpace(StateReason)) { TempData["StatusMessage"] = "A reason is required when reopening signups."; return RedirectToPage(new { id }); }
        if (target == EventState.SignupOpen) item.OpenSignups(timeProvider.GetUtcNow()); else if (target == EventState.SignupClosed) item.CloseSignups(); else return BadRequest();
        dbContext.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), id, from, item.State, User.GetAccountId()!.Value, timeProvider.GetUtcNow(), StateReason));
        await dbContext.SaveChangesAsync(ct); await AuditAsync("event.state_changed", item, $"{from} → {item.State}", ct); return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostCapacityAsync(Guid id, CancellationToken ct)
    {
        if (await dbContext.Events.AnyAsync(e => e.Id == id && e.DraftLocked, ct)) { TempData["StatusMessage"] = "The participant cap cannot change after the draft has started."; return RedirectToPage(new { id }); }
        var before = await dbContext.Events.AsNoTracking().Where(e => e.Id == id).Select(e => e.ParticipantCap).SingleOrDefaultAsync(ct);
        try { var promoted = await signupService.IncreaseCapacityAndPromoteAsync(id, NewCap, ct); await AuditAsync("event.capacity_increased", await dbContext.Events.FindAsync([id], ct) ?? throw new InvalidOperationException(), $"{before} → {NewCap}; promoted {promoted}", ct); TempData["StatusMessage"] = $"Capacity increased. {promoted} participant(s) promoted."; }
        catch (InvalidOperationException ex) { TempData["StatusMessage"] = ex.Message; }
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostExtendAsync(Guid id, CancellationToken ct)
    {
        var item = await dbContext.Events.SingleOrDefaultAsync(e => e.Id == id, ct); if (item is null) return NotFound();
        if (NewSignupClosing.Minute is not (0 or 30) || NewSignupClosing.Second != 0 || NewSignupClosing.Millisecond != 0) { TempData["StatusMessage"] = "Signup closing must use a full or half hour, such as 18:30 or 19:00."; return RedirectToPage(new { id }); }
        try { item.ExtendSignupClosing(NewSignupClosing); await dbContext.SaveChangesAsync(ct); await AuditAsync("event.signup_extended", item, NewSignupClosing.ToString("O"), ct); }
        catch (InvalidOperationException ex) { TempData["StatusMessage"] = ex.Message; }
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostRemoveAsync(Guid id, Guid participantId, string reason, CancellationToken ct)
    {
        if (await dbContext.Events.AnyAsync(e => e.Id == id && e.DraftLocked, ct)) { TempData["StatusMessage"] = "Participants are locked after the draft has started. Undo or correct the draft instead of changing the signup pool."; return RedirectToPage(new { id }); }
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(p => p.Id == participantId && p.EventId == id, ct); if (participant is null) return NotFound();
        if (await dbContext.TeamMemberships.AnyAsync(membership => membership.EventParticipantId == participantId && membership.LeftAt == null, ct)) { TempData["StatusMessage"] = "This player belongs to a team. Change or remove their roster membership from Teams and draft first."; return RedirectToPage(new { id }); }
        var wasConfirmed = participant.SignupStatus == SignupStatus.Confirmed; participant.Remove(timeProvider.GetUtcNow(), reason); await dbContext.SaveChangesAsync(ct);
        var promoted = wasConfirmed ? await signupService.PromoteAvailablePlacesAsync(id, ct) : 0;
        await auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "participant.removed", "participant", participant.Id.ToString(), $"Reason: {reason}; promoted {promoted}", ct);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostWithdrawAsync(Guid id, Guid participantId, string reason, CancellationToken ct)
    {
        if (await dbContext.Events.AnyAsync(e => e.Id == id && e.DraftLocked, ct)) { TempData["StatusMessage"] = "Participants are locked after the draft has started. Undo or correct the draft instead of changing the signup pool."; return RedirectToPage(new { id }); }
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(p => p.Id == participantId && p.EventId == id, ct); if (participant is null) return NotFound(); if (await dbContext.TeamMemberships.AnyAsync(membership => membership.EventParticipantId == participantId && membership.LeftAt == null, ct)) { TempData["StatusMessage"] = "This player belongs to a team. Change or remove their roster membership from Teams and draft first."; return RedirectToPage(new { id }); } var wasConfirmed = participant.SignupStatus == SignupStatus.Confirmed; participant.Withdraw(timeProvider.GetUtcNow(), reason); await dbContext.SaveChangesAsync(ct); var promoted = wasConfirmed ? await signupService.PromoteAvailablePlacesAsync(id, ct) : 0; await auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "participant.withdrawn", "participant", participant.Id.ToString(), $"Reason: {reason}; promoted {promoted}", ct); return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostAddTestSignupsAsync(Guid id, CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        try
        {
            var result = await testSignupImporter.ImportAsync(id, ct);
            await auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "participants.test_data_imported", "event", id.ToString(), $"Imported {result.Imported}; waiting {result.WaitingListed}; skipped {result.Skipped.Count}", ct);
            TempData["StatusMessage"] = $"Added {result.Imported} test signup(s); {result.WaitingListed} entered the waiting list." + (result.Skipped.Count > 0 ? $" {result.Skipped.Count} skipped: {string.Join("; ", result.Skipped)}" : string.Empty);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            TempData["StatusMessage"] = $"Test signup import failed: {exception.Message}";
        }
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostStartEventAsync(Guid id,CancellationToken ct)
    {
        var item=await dbContext.Events.SingleOrDefaultAsync(x=>x.Id==id,ct);if(item is null)return NotFound();if(!await dbContext.Boards.AnyAsync(x=>x.EventId==id&&x.State==Bingo.Domain.Boards.BoardState.Published,ct)){TempData["StatusMessage"]="Publish the board before starting the event.";return RedirectToPage(new{id});}if(!await dbContext.DraftSessions.AnyAsync(x=>x.EventId==id&&x.State==DraftState.Finalized,ct)){TempData["StatusMessage"]="Finalize the team rosters before starting the event.";return RedirectToPage(new{id});}try{item.StartEvent(timeProvider.GetUtcNow());await dbContext.SaveChangesAsync(ct);await AuditAsync("event.started",item,"Event entered Live state",ct);}catch(InvalidOperationException ex){TempData["StatusMessage"]=ex.Message;}return RedirectToPage(new{id});
    }
    public async Task<IActionResult> OnPostEndEventAsync(Guid id,CancellationToken ct)
    {var item=await dbContext.Events.SingleOrDefaultAsync(x=>x.Id==id,ct);if(item is null)return NotFound();try{item.EndEvent();await dbContext.SaveChangesAsync(ct);await AuditAsync("event.ended",item,"Awaiting final review",ct);}catch(InvalidOperationException ex){TempData["StatusMessage"]=ex.Message;}return RedirectToPage(new{id});}
    public async Task<IActionResult> OnPostReopenSubmissionsAsync(Guid id,CancellationToken ct)
    {var item=await dbContext.Events.SingleOrDefaultAsync(x=>x.Id==id,ct);if(item is null)return NotFound();if(ReopenUntil is null||string.IsNullOrWhiteSpace(StateReason)){TempData["StatusMessage"]="A future cutoff and reason are required.";return RedirectToPage(new{id});}try{item.ReopenSubmissions(ReopenUntil.Value,timeProvider.GetUtcNow());await dbContext.SaveChangesAsync(ct);await AuditAsync("event.submissions_reopened",item,$"Until {ReopenUntil:O}; {StateReason}",ct);TempData["StatusMessage"]=$"Submissions reopened until {ReopenUntil.Value.ToLocalTime():g}.";}catch(InvalidOperationException ex){TempData["StatusMessage"]=ex.Message;}return RedirectToPage(new{id});}
    public async Task<IActionResult> OnPostEvidenceCodeModeAsync(Guid id,bool enabled,CancellationToken ct)
    {var item=await dbContext.Events.SingleOrDefaultAsync(x=>x.Id==id,ct);if(item is null)return NotFound();item.SetEvidenceCodeEnabled(enabled);await dbContext.SaveChangesAsync(ct);await AuditAsync("event.evidence_code_mode",item,enabled?"Enabled":"Disabled",ct);return RedirectToPage(new{id});}
    public Task<IActionResult> OnPostGenerateEvidenceCodeAsync(Guid id,CancellationToken ct)=>CreateEvidenceCode(id,GenerateCode(),ct);
    public Task<IActionResult> OnPostCreateEvidenceCodeAsync(Guid id,CancellationToken ct)=>CreateEvidenceCode(id,NewEvidenceCode,ct);
    private async Task<IActionResult>CreateEvidenceCode(Guid id,string?code,CancellationToken ct)
    {
        var item=await dbContext.Events.SingleOrDefaultAsync(x=>x.Id==id,ct);if(item is null)return NotFound();if(!item.EvidenceCodeEnabled){TempData["StatusMessage"]="Enable evidence codes first.";return RedirectToPage(new{id});}if(string.IsNullOrWhiteSpace(code)){TempData["StatusMessage"]="Enter a code or use Generate.";return RedirectToPage(new{id});}var activates=(EvidenceCodeActivatesAt??timeProvider.GetUtcNow()).ToUniversalTime();if(await dbContext.EvidenceCodes.AnyAsync(x=>x.EventId==id&&x.ActivatesAt==activates,ct)){TempData["StatusMessage"]="Another code already activates at that exact time.";return RedirectToPage(new{id});}var created=new EvidenceCode(Guid.NewGuid(),id,code,activates,User.GetAccountId()!.Value,timeProvider.GetUtcNow(),EvidenceCodeNote);dbContext.EvidenceCodes.Add(created);var codes=await dbContext.EvidenceCodes.Where(x=>x.EventId==id).OrderBy(x=>x.ActivatesAt).ToListAsync(ct);codes.Add(created);codes=codes.OrderBy(x=>x.ActivatesAt).ToList();for(var index=0;index<codes.Count;index++)codes[index].SetRetiresAt(index+1<codes.Count?codes[index+1].ActivatesAt:null);await dbContext.SaveChangesAsync(ct);await AuditAsync("evidence_code.created",item,$"{created.Code}; activates {activates:O}",ct);TempData["StatusMessage"]=$"Evidence code {created.Code} saved.";return RedirectToPage(new{id});
    }
    private static string GenerateCode(){const string alphabet="ABCDEFGHJKLMNPQRSTUVWXYZ23456789";Span<char>value=stackalloc char[6];for(var i=0;i<value.Length;i++)value[i]=alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];return new string(value);}
    private async Task<bool> LoadAsync(Guid id, CancellationToken ct)
    {
        var item = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct); if (item is null) return false;
        var participants = await dbContext.EventParticipants.AsNoTracking().Where(p => p.EventId == id && p.Source != SignupSource.AdminCreated).OrderBy(p => p.SignedUpAt).ThenBy(p => p.SignupSequence).ToListAsync(ct);
        var waiting = participants.Where(p => p.SignupStatus == SignupStatus.WaitingList).Select((p, i) => (p.Id, Position: i + 1)).ToDictionary(x => x.Id, x => x.Position);
        Participants = participants.Select(p => new ParticipantRow(p.Id, p.SignupSequence, p.PrimaryAccountName, p.EhbSnapshot, p.SignupStatus, p.PaymentStatus, p.SignedUpAt, p.CaptainVolunteer, waiting.TryGetValue(p.Id, out var position) ? position : null)).ToList();
        EvidenceCodes=await dbContext.EvidenceCodes.AsNoTracking().Where(x=>x.EventId==id).OrderByDescending(x=>x.ActivatesAt).Select(x=>new EvidenceCodeRow(x.Id,x.Code,x.ActivatesAt,x.RetiresAt,x.Note)).ToListAsync(ct);EventView = new EventDetails(item.Id, item.Name, item.Slug, item.State, item.SignupClosesAt, item.EventStartsAt,item.EventEndsAt,item.SubmissionCutoffAt,item.ReopenedSubmissionCutoffAt,item.ParticipantCap, participants.Count(p => p.SignupStatus == SignupStatus.Confirmed), waiting.Count, item.DraftLocked,item.EvidenceCodeEnabled); NewCap = item.ParticipantCap; NewSignupClosing = item.SignupClosesAt;EvidenceCodeActivatesAt=timeProvider.GetUtcNow();ReopenUntil=timeProvider.GetUtcNow().AddHours(1);return true;
    }
    private Task AuditAsync(string action, BingoEvent item, string details, CancellationToken ct) => auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, action, "event", item.Id.ToString(), details, ct);
    public sealed record EventDetails(Guid Id, string Name, string Slug, EventState State, DateTimeOffset SignupClosesAt,DateTimeOffset StartsAt,DateTimeOffset EndsAt,DateTimeOffset SubmissionCutoff,DateTimeOffset?ReopenedCutoff,int ParticipantCap, int Confirmed, int Waiting, bool DraftLocked,bool EvidenceCodeEnabled);
    public sealed record ParticipantRow(Guid Id, long Sequence, string Name, decimal Ehb, SignupStatus Status, PaymentStatus Payment, DateTimeOffset SignedUpAt, bool CaptainVolunteer, int? WaitingPosition);
    public sealed record EvidenceCodeRow(Guid Id,string Code,DateTimeOffset ActivatesAt,DateTimeOffset?RetiresAt,string?Note);
}
