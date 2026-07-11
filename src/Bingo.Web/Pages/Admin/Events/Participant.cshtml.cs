using System.ComponentModel.DataAnnotations;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class ParticipantModel(ApplicationDbContext dbContext, IAuditWriter auditWriter) : PageModel
{
    public string Name { get; private set; } = string.Empty; public string? Comments { get; private set; }
    public SignupStatus Status { get; private set; }
    [BindProperty] public PaymentStatus Payment { get; set; }
    [BindProperty, StringLength(4000), Display(Name = "Private admin notes")] public string? AdminNotes { get; set; }
    public async Task<IActionResult> OnGetAsync(Guid id, Guid participantId, CancellationToken ct) { var p = await Find(id, participantId, ct); if (p is null) return NotFound(); Load(p); return Page(); }
    public async Task<IActionResult> OnPostAsync(Guid id, Guid participantId, CancellationToken ct) { var p = await Find(id, participantId, ct); if (p is null) return NotFound(); if (!ModelState.IsValid) { Load(p); return Page(); } p.SetPaymentStatus(Payment); p.SetAdminNotes(string.IsNullOrWhiteSpace(AdminNotes) ? null : AdminNotes.Trim()); await dbContext.SaveChangesAsync(ct); await auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "participant.admin_fields_updated", "participant", p.Id.ToString(), $"Payment: {Payment}", ct); return RedirectToPage("Manage", new { id }); }
    private Task<EventParticipant?> Find(Guid id, Guid participantId, CancellationToken ct) => dbContext.EventParticipants.SingleOrDefaultAsync(p => p.EventId == id && p.Id == participantId, ct);
    private void Load(EventParticipant p) { Name = p.PrimaryAccountName; Comments = p.Comments; Status = p.SignupStatus; Payment = p.PaymentStatus; AdminNotes = p.AdminNotes; }
}
