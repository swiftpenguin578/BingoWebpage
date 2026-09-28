using Bingo.Domain.Teams;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bingo.Web.Pages.Admin.Events;

/// <summary>
/// Compile-time compatibility for retained direct tests/operators. These methods
/// are extension methods, not instance methods on <see cref="DraftModel"/>, so
/// Razor Pages cannot select them as HTTP handlers.
/// </summary>
public static class DraftLegacyHandlerCompatibilityExtensions
{
    public static Task<IActionResult> OnPostRemoveExternalTeamAsync(this DraftModel _, Guid id, Guid teamId, CancellationToken ct, bool confirmed = false) =>
        Task.FromResult<IActionResult>(new NotFoundResult());

    public static Task<IActionResult> OnPostAddExternalMemberAsync(this DraftModel _, Guid id, Guid teamId, string name, decimal ehb, string? additionalAccounts, CancellationToken ct, bool confirmed = false, Guid? rosterTeamId = null, TeamMembershipRole role = TeamMembershipRole.Participant, string? womValidationConfirmationToken = null) =>
        Task.FromResult<IActionResult>(new NotFoundResult());

    public static Task<IActionResult> OnGetRosterCsvTemplateAsync(this DraftModel _, Guid id, Guid teamId, CancellationToken ct) =>
        Task.FromResult<IActionResult>(new NotFoundResult());

    public static Task<IActionResult> OnPostPreviewRosterCsvAsync(this DraftModel _, Guid id, Guid teamId, IFormFile? csv, CancellationToken ct) =>
        Task.FromResult<IActionResult>(new NotFoundResult());

    public static Task<IActionResult> OnPostApplyRosterCsvAsync(this DraftModel _, Guid id, Guid teamId, string? previewToken, CancellationToken ct) =>
        Task.FromResult<IActionResult>(new NotFoundResult());
}
