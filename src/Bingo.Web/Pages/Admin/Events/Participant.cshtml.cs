using System.ComponentModel.DataAnnotations;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Security;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class ParticipantModel(
    ApplicationDbContext dbContext,
    EventParticipantCharacterService characterService,
    IAuditWriter auditWriter,
    IPrivateEditTokenService tokenService) : PageModel
{
    private const string ReplacementLinkKey = "ParticipantReplacementEditLink";

    [BindProperty] public EditInput Input { get; set; } = new();
    public Guid EventId { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public SignupStatus Status { get; private set; }
    public string StatusLabel { get; private set; } = string.Empty;
    public string SourceLabel { get; private set; } = string.Empty;
    public DateTimeOffset SignedUpAt { get; private set; }
    public long SignupSequence { get; private set; }
    public string? TeamName { get; private set; }
    public string? StatusReason { get; private set; }
    public IReadOnlyList<QuestionView> Questions { get; private set; } = [];
    public bool CanReplaceEditLink { get; private set; }
    public string EditLinkMessage { get; private set; } = string.Empty;
    public string? ReplacementEditLink { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, Guid participantId, CancellationToken ct)
        => await LoadAsync(id, participantId, true, ct) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(Guid id, Guid participantId, CancellationToken ct)
    {
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(item => item.EventId == id && item.Id == participantId, ct);
        if (participant is null) return NotFound();

        var normalizedName = SignupService.NormalizeAccountName(Input.PrimaryAccountName ?? string.Empty);
        if (!Enum.IsDefined(Input.Payment))
        {
            ModelState.AddModelError("Input.Payment", "Choose a valid payment status.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedName))
        {
            var duplicate = await dbContext.PrimaryCharacters().AnyAsync(item =>
                item.EventId == id && item.ParticipantId != participantId &&
                item.NormalizedName == normalizedName, ct);
            if (duplicate) ModelState.AddModelError("Input.PrimaryAccountName", "That account is already signed up for this event.");
        }

        if (!ModelState.IsValid)
        {
            if (!await LoadAsync(id, participantId, false, ct)) return NotFound();
            return Page();
        }

        participant.UpdateSignupDetails(
            Clean(Input.DiscordIdentity), Clean(Input.Comments), Input.CaptainVolunteer);
        try
        {
            await characterService.ApplyFixedSignupAssignmentsAsync(
                participant, Input.PrimaryAccountName ?? string.Empty, Input.Ehb, Input.SecondAccountName,
                EhbSource.AdminCorrection, User.GetAccountId(), ct);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("Input.PrimaryAccountName", exception.Message);
            if (!await LoadAsync(id, participantId, false, ct)) return NotFound();
            return Page();
        }
        participant.SetPaymentStatus(Input.Payment);
        participant.SetAdminNotes(Clean(Input.AdminNotes));

        var activeQuestions = await dbContext.SignupQuestions.AsNoTracking()
            .Where(question => question.EventId == id && question.Active)
            .ToListAsync(ct);
        var existingAnswers = await dbContext.SignupAnswers
            .Where(answer => answer.EventParticipantId == participantId)
            .ToDictionaryAsync(answer => answer.SignupQuestionId, ct);
        foreach (var question in activeQuestions)
        {
            var value = Input.CustomAnswers.GetValueOrDefault(question.Id)?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                if (existingAnswers.TryGetValue(question.Id, out var answerToRemove)) dbContext.SignupAnswers.Remove(answerToRemove);
                continue;
            }

            if (existingAnswers.TryGetValue(question.Id, out var existing)) existing.Update(value);
            else dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participantId, question.Id, question.Label, value));
        }

        await dbContext.SaveChangesAsync(ct);
        await auditWriter.WriteAsync(
            User.GetAccountId(), User.Identity!.Name!, "participant.details_updated", "participant", participant.Id.ToString(),
            $"Account: {(Input.PrimaryAccountName ?? string.Empty).Trim()}; EHB: {Input.Ehb}; payment: {participant.PaymentStatus}", ct);
        SetStatus("Participant details saved.", UiMessageType.Success);
        return RedirectToPage(new { id, participantId });
    }

    public async Task<IActionResult> OnPostCreateEditLinkAsync(Guid id, Guid participantId, CancellationToken ct)
    {
        var bingoEvent = await dbContext.Events.SingleOrDefaultAsync(item => item.Id == id, ct);
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(item => item.EventId == id && item.Id == participantId, ct);
        if (bingoEvent is null || participant is null) return NotFound();
        if (!CanCreateEditLink(bingoEvent, participant))
        {
            SetStatus("A replacement edit link is not available for this participant.", UiMessageType.Error);
            return RedirectToPage(new { id, participantId });
        }

        var created = tokenService.Create();
        participant.ReplacePrivateEditToken(created.Hash);
        await dbContext.SaveChangesAsync(ct);
        var link = Url.Page("/Events/EditSignup", null, new { slug = bingoEvent.Slug, token = created.Token }, Request.Scheme)
                   ?? $"{Request.Scheme}://{Request.Host}/Events/{bingoEvent.Slug}/Signup/Edit/{created.Token}";
        TempData[ReplacementLinkKey] = link;
        await auditWriter.WriteAsync(
            User.GetAccountId(), User.Identity!.Name!, "participant.private_edit_link_replaced", "participant", participant.Id.ToString(),
            "Previous private edit link invalidated", ct);
        SetStatus("Replacement edit link created. The previous link no longer works.", UiMessageType.Success);
        return RedirectToPage(new { id, participantId });
    }

    private async Task<bool> LoadAsync(Guid id, Guid participantId, bool initializeInput, CancellationToken ct)
    {
        var bingoEvent = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
        var participant = await dbContext.EventParticipants.AsNoTracking().SingleOrDefaultAsync(item => item.EventId == id && item.Id == participantId, ct);
        if (bingoEvent is null || participant is null) return false;

        EventId = id;
        EventName = bingoEvent.Name;
        var authority = await dbContext.PrimaryCharacters().AsNoTracking().SingleAsync(x => x.ParticipantId == participantId, ct);
        var secondName = await (from assignment in dbContext.EventParticipantCharacters.AsNoTracking()
                                join character in dbContext.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                                where assignment.EventParticipantId == participantId && assignment.ReleasedAt == null &&
                                      assignment.EventRole == EventCharacterRole.Informational
                                orderby assignment.RegistrationOrder
                                select character.DisplayName).FirstOrDefaultAsync(ct);
        Name = authority.Name;
        Status = participant.SignupStatus;
        StatusLabel = participant.SignupStatus switch
        {
            SignupStatus.Confirmed => "Confirmed",
            SignupStatus.WaitingList => "Waiting list",
            SignupStatus.Withdrawn => "Withdrawn",
            _ => "Removed"
        };
        SourceLabel = participant.Source switch
        {
            SignupSource.Website => "Website signup",
            SignupSource.CsvImport => "CSV import",
            _ => "Added by an admin"
        };
        SignedUpAt = participant.SignedUpAt;
        SignupSequence = participant.SignupSequence;
        StatusReason = participant.StatusReason;
        TeamName = await (from membership in dbContext.TeamMemberships.AsNoTracking()
                          join team in dbContext.Teams.AsNoTracking() on membership.TeamId equals team.Id
                          where membership.EventParticipantId == participantId && membership.LeftAt == null
                          select team.Name).SingleOrDefaultAsync(ct);

        var questions = await dbContext.SignupQuestions.AsNoTracking()
            .Where(question => question.EventId == id)
            .OrderBy(question => question.Position)
            .ToListAsync(ct);
        var answers = await dbContext.SignupAnswers.AsNoTracking()
            .Where(answer => answer.EventParticipantId == participantId)
            .ToDictionaryAsync(answer => answer.SignupQuestionId, ct);
        Questions = questions
            .Where(question => question.Active || answers.ContainsKey(question.Id))
            .Select(question => new QuestionView(
                question.Id,
                answers.TryGetValue(question.Id, out var answer) ? answer.QuestionLabelSnapshot : question.Label,
                question.Type,
                question.Required,
                question.Active,
                question.Options?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
                answers.GetValueOrDefault(question.Id)?.Value))
            .ToList();

        if (initializeInput)
        {
            Input = new EditInput
            {
                PrimaryAccountName = authority.Name,
                Ehb = authority.Ehb,
                SecondAccountName = secondName,
                DiscordIdentity = participant.DiscordIdentity,
                Comments = participant.Comments,
                CaptainVolunteer = participant.CaptainVolunteer,
                Payment = participant.PaymentStatus,
                AdminNotes = participant.AdminNotes,
                CustomAnswers = Questions.Where(question => question.Active && !string.IsNullOrWhiteSpace(question.Value))
                    .ToDictionary(question => question.Id, question => question.Value!)
            };
        }

        CanReplaceEditLink = CanCreateEditLink(bingoEvent, participant);
        EditLinkMessage = EditLinkExplanation(bingoEvent, participant);
        ReplacementEditLink = TempData[ReplacementLinkKey]?.ToString();
        return true;
    }

    private static bool CanCreateEditLink(BingoEvent bingoEvent, EventParticipant participant)
        => bingoEvent.AllowPrivateSignupEditing && !bingoEvent.DraftLocked && participant.Source == SignupSource.Website &&
           participant.SignupStatus is SignupStatus.Confirmed or SignupStatus.WaitingList;

    private static string EditLinkExplanation(BingoEvent bingoEvent, EventParticipant participant)
    {
        if (participant.Source != SignupSource.Website) return "This participant did not sign up through the website.";
        if (!bingoEvent.AllowPrivateSignupEditing) return "Private signup editing is disabled for this event.";
        if (bingoEvent.DraftLocked) return "Private signup editing closed when the draft started.";
        if (participant.SignupStatus is not (SignupStatus.Confirmed or SignupStatus.WaitingList)) return "Inactive signups cannot be edited by the player.";
        return "Existing links cannot be displayed for security. Create a replacement only if the player lost their link.";
    }

    private void SetStatus(string message, UiMessageType type)
    {
        TempData["StatusMessage"] = message;
        TempData[UiMessage.TypeKey] = type.ToString();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed class EditInput
    {
        [Required, StringLength(100)] public string PrimaryAccountName { get; set; } = string.Empty;
        [Range(0, 100000)] public decimal Ehb { get; set; }
        [StringLength(100)] public string? SecondAccountName { get; set; }
        [StringLength(100)] public string? DiscordIdentity { get; set; }
        [StringLength(4000)] public string? Comments { get; set; }
        public bool CaptainVolunteer { get; set; }
        public PaymentStatus Payment { get; set; }
        [StringLength(4000)] public string? AdminNotes { get; set; }
        public Dictionary<Guid, string> CustomAnswers { get; set; } = [];
    }

    public sealed record QuestionView(
        Guid Id, string Label, SignupQuestionType Type, bool Required, bool Active, string[] Options, string? Value);
}
