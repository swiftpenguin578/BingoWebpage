using Bingo.Application.Announcements;
using Bingo.Application.Boards;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Antiforgery;

namespace Bingo.Web.Announcements;

public static class DropAnnouncementEndpoints
{
    public static IEndpointRouteBuilder MapDropAnnouncementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/drop-announcements").RequireAuthorization();
        group.MapGet("/current", GetCurrentAsync);
        group.MapGet("/new", GetNewAsync);
        group.MapGet("/{eventId:guid}", GetAsync);
        endpoints.MapGet("/api/public/events/{slug}/recent-drops", GetRecentDropsAsync);
        group.MapPost("/claim", ClaimAsync);
        group.MapPost("/dismiss", DismissAsync);
        group.MapPost("/acknowledge-banner", AcknowledgeBannerAsync);
        group.MapPost("/acknowledge-drops", AcknowledgeDropsAsync);
        group.MapPost("/acknowledge-both", AcknowledgeBothAsync);
        group.MapPost("/clear-all-new", ClearAllNewAsync);
        return endpoints;
    }

    private static async Task<IResult> GetNewAsync(HttpContext context, IDropAnnouncementService announcements, Guid eventId, string? submissionIds, CancellationToken cancellationToken)
    {
        if (!TryAccount(context, out var accountId)) return Results.Unauthorized();
        var ids = (submissionIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty).Where(value => value != Guid.Empty).Take(100).ToArray();
        return Results.Ok(await announcements.GetNewSubmissionIdsAsync(accountId, eventId, ids, cancellationToken));
    }

    private static async Task<IResult> GetRecentDropsAsync(string slug, IPublicBoardService boards, int? limit, string? dropSearch, string? dropTeam, string? loadedSubmissionIds, CancellationToken cancellationToken)
    {
        var ids = loadedSubmissionIds?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(value => value != Guid.Empty).Distinct().Take(100).ToArray();
        var feed = await boards.GetRecentDropsAsync(slug, limit ?? 25, dropSearch, dropTeam, ids, cancellationToken);
        return feed is null ? Results.NotFound() : Results.Ok(feed);
    }

    private static async Task<IResult> GetCurrentAsync(HttpContext context, IDropAnnouncementService announcements, int? limit, int? offset, long? snapshotSequence, CancellationToken cancellationToken)
    {
        if (!TryAccount(context, out var accountId)) return Results.Unauthorized();
        var snapshot = await announcements.GetCurrentAsync(accountId, limit ?? 25, offset ?? 0, snapshotSequence, cancellationToken);
        return snapshot is null ? Results.NoContent() : Results.Ok(snapshot);
    }

    private static async Task<IResult> GetAsync(Guid eventId, HttpContext context, IDropAnnouncementService announcements, int? limit, int? offset, long? snapshotSequence, CancellationToken cancellationToken)
    {
        if (!TryAccount(context, out var accountId)) return Results.Unauthorized();
        var snapshot = await announcements.GetAsync(accountId, eventId, limit ?? 25, offset ?? 0, snapshotSequence, cancellationToken);
        return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
    }

    private static async Task<IResult> ClaimAsync(HttpContext context, IDropAnnouncementService announcements, IAntiforgery antiforgery, EventRequest request, CancellationToken cancellationToken)
    {
        if (!TryAccount(context, out var accountId)) return Results.Unauthorized();
        if (!await ValidMutationAsync(context, antiforgery)) return Results.BadRequest();
        return Results.Ok(new { claimed = await announcements.ClaimAutomaticExpansionAsync(accountId, request.EventId, request.SnapshotSequence, cancellationToken) });
    }

    private static async Task<IResult> DismissAsync(HttpContext context, IDropAnnouncementService announcements, IAntiforgery antiforgery, AnnouncementRequest request, CancellationToken cancellationToken)
    {
        if (!TryAccount(context, out var accountId)) return Results.Unauthorized();
        if (!await ValidMutationAsync(context, antiforgery)) return Results.BadRequest();
        if (request.Generation is { } generation && request.SnapshotSequence is { } sequence)
            await announcements.DismissSnapshotAsync(accountId, request.EventId, generation, sequence, cancellationToken);
        else
            await announcements.DismissAsync(accountId, request.EventId, request.SubmissionIds ?? [], cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> AcknowledgeBannerAsync(HttpContext context, IDropAnnouncementService announcements, IAntiforgery antiforgery, AnnouncementRequest request, CancellationToken cancellationToken)
    {
        if (!TryAccount(context, out var accountId)) return Results.Unauthorized();
        if (!await ValidMutationAsync(context, antiforgery)) return Results.BadRequest();
        await announcements.AcknowledgeBannerAsync(accountId, request.EventId, request.SubmissionIds ?? [], cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> AcknowledgeDropsAsync(HttpContext context, IDropAnnouncementService announcements, IAntiforgery antiforgery, AnnouncementRequest request, CancellationToken cancellationToken)
    {
        if (!TryAccount(context, out var accountId)) return Results.Unauthorized();
        if (!await ValidMutationAsync(context, antiforgery)) return Results.BadRequest();
        await announcements.AcknowledgeDropsAsync(accountId, request.EventId, request.SubmissionIds ?? [], cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> AcknowledgeBothAsync(HttpContext context, IDropAnnouncementService announcements, IAntiforgery antiforgery, SingleAnnouncementRequest request, CancellationToken cancellationToken)
    {
        if (!TryAccount(context, out var accountId)) return Results.Unauthorized();
        if (!await ValidMutationAsync(context, antiforgery)) return Results.BadRequest();
        await announcements.AcknowledgeBothAsync(accountId, request.EventId, request.SubmissionId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ClearAllNewAsync(HttpContext context, IDropAnnouncementService announcements, IAntiforgery antiforgery, EventRequest request, CancellationToken cancellationToken)
    {
        if (!TryAccount(context, out var accountId)) return Results.Unauthorized();
        if (!await ValidMutationAsync(context, antiforgery)) return Results.BadRequest();
        await announcements.ClearAllNewAsync(accountId, request.EventId, cancellationToken);
        return Results.NoContent();
    }

    private static bool TryAccount(HttpContext context, out Guid accountId)
    {
        var id = context.User.GetAccountId();
        accountId = id.GetValueOrDefault();
        return id is not null;
    }

    private static async Task<bool> ValidMutationAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    public sealed record EventRequest(Guid EventId, long? SnapshotSequence = null);
    public sealed record AnnouncementRequest(Guid EventId, int? Generation, long? SnapshotSequence, IReadOnlyCollection<Guid>? SubmissionIds);
    public sealed record SingleAnnouncementRequest(Guid EventId, Guid SubmissionId);
}
