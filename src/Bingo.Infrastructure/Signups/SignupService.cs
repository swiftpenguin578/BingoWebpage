using System.Data;
using Bingo.Application.Security;
using Bingo.Application.Signups;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Infrastructure.Signups;

public sealed class SignupService(
    ApplicationDbContext dbContext,
    EventParticipantCharacterService characterService,
    IPrivateEditTokenService tokenService,
    ISecretHasher secretHasher,
    TimeProvider timeProvider) : ISignupService
{
    public async Task<SignupResult> SignUpAsync(SignupRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var bingoEvent = await dbContext.Events
            .FromSqlInterpolated($"SELECT * FROM events WHERE id = {request.EventId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (bingoEvent is null)
        {
            return new SignupResult(false, "Signups are not currently open.", null, null, null, null);
        }

        if (bingoEvent.DraftLocked)
        {
            return new SignupResult(false, "The participant pool is locked because the draft has started.", null, null, null, null);
        }

        if (!request.BypassAvailability && !bingoEvent.AcceptsSignups(now))
        {
            return new SignupResult(false, "Signups are not currently open.", null, null, null, null);
        }

        if (!request.BypassAvailability && bingoEvent.RequireSignupCode &&
            (string.IsNullOrWhiteSpace(request.SignupCode) || bingoEvent.SignupCodeHash is null ||
             !secretHasher.Verify(request.SignupCode, bingoEvent.SignupCodeHash)))
        {
            return new SignupResult(false, "The event code is incorrect.", null, null, null, null);
        }

        var normalizedName = NormalizeAccountName(request.PrimaryAccountName);
        var duplicateExists = await dbContext.PrimaryCharacters().AnyAsync(
            participant => participant.EventId == request.EventId &&
                participant.NormalizedName == normalizedName,
            cancellationToken);
        if (duplicateExists)
        {
            return new SignupResult(false, "That account is already signed up for this event.", null, null, null, null);
        }

        var questions = await dbContext.SignupQuestions.AsNoTracking()
            .Where(question => question.EventId == request.EventId && question.Active)
            .ToListAsync(cancellationToken);
        var missingRequired = questions.Any(question => question.Required &&
            (!request.CustomAnswers.TryGetValue(question.Id, out var answer) || string.IsNullOrWhiteSpace(answer)));
        if (missingRequired)
        {
            return new SignupResult(false, "A required signup question is missing.", null, null, null, null);
        }

        var confirmedCount = await dbContext.EventParticipants.CountAsync(
            participant => participant.EventId == request.EventId &&
                participant.Source != SignupSource.AdminCreated &&
                participant.SignupStatus == SignupStatus.Confirmed,
            cancellationToken);
        var status = confirmedCount < bingoEvent.ParticipantCap
            ? SignupStatus.Confirmed
            : SignupStatus.WaitingList;
        if (status == SignupStatus.WaitingList && !bingoEvent.WaitingListEnabled && !request.BypassAvailability)
        {
            return new SignupResult(false, "This event is full and does not have a waiting list.", null, null, null, null);
        }

        var nextSequence = (await dbContext.EventParticipants
            .Where(participant => participant.EventId == request.EventId)
            .MaxAsync(participant => (long?)participant.SignupSequence, cancellationToken) ?? 0) + 1;
        var token = bingoEvent.AllowPrivateSignupEditing && request.Source == SignupSource.Website ? tokenService.Create() : default;
        var participant = new EventParticipant(
            Guid.NewGuid(), request.EventId, status, nextSequence, now, request.Source, token.Hash);
        participant.UpdateSignupDetails(
            Clean(request.DiscordIdentity), Clean(request.Comments), request.CaptainVolunteer);
        dbContext.EventParticipants.Add(participant);
        try
        {
            await characterService.ApplyFixedSignupAssignmentsAsync(
                participant, request.PrimaryAccountName, request.Ehb, request.SecondAccountName,
                request.Source == SignupSource.CsvImport ? EhbSource.Import : EhbSource.Manual,
                null, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            dbContext.ChangeTracker.Clear();
            return new SignupResult(false, exception.Message, null, null, null, null);
        }

        foreach (var question in questions)
        {
            if (request.CustomAnswers.TryGetValue(question.Id, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                dbContext.SignupAnswers.Add(new SignupAnswer(
                    Guid.NewGuid(), participant.Id, question.Id, question.Label, value.Trim()));
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            dbContext.ChangeTracker.Clear();
            return new SignupResult(false, "That account is already signed up for this event.", null, null, null, null);
        }
        await transaction.CommitAsync(cancellationToken);
        int? waitingPosition = status == SignupStatus.WaitingList
            ? await GetWaitingPositionAsync(participant.Id, request.EventId, cancellationToken)
            : null;
        return new SignupResult(true, null, participant.Id, status, waitingPosition, token.Token);
    }

    public async Task<int> IncreaseCapacityAndPromoteAsync(Guid eventId, int newCap, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var bingoEvent = await dbContext.Events
            .FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        if (bingoEvent.DraftLocked)
        {
            throw new InvalidOperationException("The participant cap is locked because the draft has started.");
        }
        bingoEvent.IncreaseParticipantCap(newCap);
        var promoted = await PromoteWithinLockedEventAsync(bingoEvent, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return promoted;
    }

    public async Task<int> PromoteAvailablePlacesAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var bingoEvent = await dbContext.Events
            .FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        var promoted = await PromoteWithinLockedEventAsync(bingoEvent, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return promoted;
    }

    private async Task<int> PromoteWithinLockedEventAsync(Domain.Events.BingoEvent bingoEvent, CancellationToken cancellationToken)
    {
        if (bingoEvent.DraftLocked) return 0;
        var confirmed = await dbContext.EventParticipants.CountAsync(
            participant => participant.EventId == bingoEvent.Id &&
                participant.Source != SignupSource.AdminCreated &&
                participant.SignupStatus == SignupStatus.Confirmed,
            cancellationToken);
        var places = Math.Max(0, bingoEvent.ParticipantCap - confirmed);
        if (places == 0) return 0;
        var waiting = await dbContext.EventParticipants
            .Where(participant => participant.EventId == bingoEvent.Id && participant.SignupStatus == SignupStatus.WaitingList)
            .OrderBy(participant => participant.SignedUpAt)
            .ThenBy(participant => participant.SignupSequence)
            .Take(places)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var participant in waiting) participant.Promote(now);
        return waiting.Count;
    }

    private async Task<int> GetWaitingPositionAsync(Guid participantId, Guid eventId, CancellationToken cancellationToken)
    {
        var waitingIds = await dbContext.EventParticipants.AsNoTracking()
            .Where(participant => participant.EventId == eventId && participant.SignupStatus == SignupStatus.WaitingList)
            .OrderBy(participant => participant.SignedUpAt)
            .ThenBy(participant => participant.SignupSequence)
            .Select(participant => participant.Id)
            .ToListAsync(cancellationToken);
        return waitingIds.IndexOf(participantId) + 1;
    }

    public static string NormalizeAccountName(string value) => value.Trim().ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
