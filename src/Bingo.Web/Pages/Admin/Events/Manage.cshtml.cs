using System.ComponentModel.DataAnnotations;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Signups;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class ManageModel(ApplicationDbContext dbContext, ISignupService signupService, IAuditWriter auditWriter, TimeProvider timeProvider) : PageModel
{
    public EventDetails? EventView { get; private set; }
    public IReadOnlyList<ParticipantRow> Participants { get; private set; } = [];
    [BindProperty, Range(1, 10000), Display(Name = "New participant cap")] public int NewCap { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "New signup closing")] public DateTimeOffset NewSignupClosing { get; set; }
    [BindProperty, StringLength(1000), Display(Name = "Reason for reopening")] public string? StateReason { get; set; }
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) => await LoadAsync(id, ct) ? Page() : NotFound();
    public async Task<IActionResult> OnPostStateAsync(Guid id, EventState target, CancellationToken ct)
    {
        var item = await dbContext.Events.SingleOrDefaultAsync(e => e.Id == id, ct); if (item is null) return NotFound();
        var from = item.State;
        if (from == EventState.SignupClosed && target == EventState.SignupOpen && string.IsNullOrWhiteSpace(StateReason)) { TempData["StatusMessage"] = "A reason is required when reopening signups."; return RedirectToPage(new { id }); }
        if (target == EventState.SignupOpen) item.OpenSignups(timeProvider.GetUtcNow()); else if (target == EventState.SignupClosed) item.CloseSignups(); else return BadRequest();
        dbContext.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), id, from, item.State, User.GetAccountId()!.Value, timeProvider.GetUtcNow(), StateReason));
        await dbContext.SaveChangesAsync(ct); await AuditAsync("event.state_changed", item, $"{from} → {item.State}", ct); return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostCapacityAsync(Guid id, CancellationToken ct)
    {
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
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(p => p.Id == participantId && p.EventId == id, ct); if (participant is null) return NotFound();
        var wasConfirmed = participant.SignupStatus == SignupStatus.Confirmed; participant.Remove(timeProvider.GetUtcNow(), reason); await dbContext.SaveChangesAsync(ct);
        var promoted = wasConfirmed ? await signupService.PromoteAvailablePlacesAsync(id, ct) : 0;
        await auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "participant.removed", "participant", participant.Id.ToString(), $"Reason: {reason}; promoted {promoted}", ct);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostWithdrawAsync(Guid id, Guid participantId, string reason, CancellationToken ct)
    {
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(p => p.Id == participantId && p.EventId == id, ct); if (participant is null) return NotFound(); var wasConfirmed = participant.SignupStatus == SignupStatus.Confirmed; participant.Withdraw(timeProvider.GetUtcNow(), reason); await dbContext.SaveChangesAsync(ct); var promoted = wasConfirmed ? await signupService.PromoteAvailablePlacesAsync(id, ct) : 0; await auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "participant.withdrawn", "participant", participant.Id.ToString(), $"Reason: {reason}; promoted {promoted}", ct); return RedirectToPage(new { id });
    }
    private async Task<bool> LoadAsync(Guid id, CancellationToken ct)
    {
        var item = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct); if (item is null) return false;
        var participants = await dbContext.EventParticipants.AsNoTracking().Where(p => p.EventId == id).OrderBy(p => p.SignedUpAt).ThenBy(p => p.SignupSequence).ToListAsync(ct);
        var waiting = participants.Where(p => p.SignupStatus == SignupStatus.WaitingList).Select((p, i) => (p.Id, Position: i + 1)).ToDictionary(x => x.Id, x => x.Position);
        Participants = participants.Select(p => new ParticipantRow(p.Id, p.SignupSequence, p.PrimaryAccountName, p.EhbSnapshot, p.SignupStatus, p.PaymentStatus, p.SignedUpAt, p.CaptainVolunteer, waiting.TryGetValue(p.Id, out var position) ? position : null)).ToList();
        EventView = new EventDetails(item.Id, item.Name, item.Slug, item.State, item.SignupClosesAt, item.ParticipantCap, participants.Count(p => p.SignupStatus == SignupStatus.Confirmed), waiting.Count); NewCap = item.ParticipantCap; NewSignupClosing = item.SignupClosesAt; return true;
    }
    private Task AuditAsync(string action, BingoEvent item, string details, CancellationToken ct) => auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, action, "event", item.Id.ToString(), details, ct);
    public sealed record EventDetails(Guid Id, string Name, string Slug, EventState State, DateTimeOffset SignupClosesAt, int ParticipantCap, int Confirmed, int Waiting);
    public sealed record ParticipantRow(Guid Id, long Sequence, string Name, decimal Ehb, SignupStatus Status, PaymentStatus Payment, DateTimeOffset SignedUpAt, bool CaptainVolunteer, int? WaitingPosition);
}
