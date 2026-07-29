using Bingo.Domain.Access;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Signups;

public sealed class EventParticipantCharacterService(ApplicationDbContext db, TimeProvider timeProvider)
{
    /// <summary>Retained for the separate pre-formed roster workflow; authenticated signup uses SignupForm assignments.</summary>
    public async Task AssignExternalRosterCharacterAsync(
        EventParticipant participant,
        string primaryName,
        decimal ehb,
        string? secondName,
        EhbSource source,
        Guid? actorAccountId,
        CancellationToken cancellationToken)
    {
        var normalizedPrimary = SignupService.NormalizeAccountName(primaryName);
        var cleanSecond = string.IsNullOrWhiteSpace(secondName) ? null : secondName.Trim();
        var normalizedSecond = cleanSecond is null ? null : SignupService.NormalizeAccountName(cleanSecond);
        if (normalizedSecond == normalizedPrimary) cleanSecond = normalizedSecond = null;

        var desired = new[]
        {
            new Desired(primaryName.Trim(), normalizedPrimary, EventCharacterRole.Playing, (decimal?)ehb, (EhbSource?)source),
            cleanSecond is null ? null : new Desired(cleanSecond, normalizedSecond!, EventCharacterRole.Informational, null, null)
        };
        var current = await db.EventParticipantCharacters
            .Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null)
            .OrderBy(x => x.RegistrationOrder)
            .ToListAsync(cancellationToken);
        var characterIds = current.Select(x => x.OsrsCharacterId).ToList();
        var currentCharacters = await db.OsrsCharacters
            .Where(x => characterIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var nextOrder = (await db.EventParticipantCharacters
            .Where(x => x.EventParticipantId == participant.Id)
            .MaxAsync(x => (int?)x.RegistrationOrder, cancellationToken) ?? -1) + 1;
        var now = timeProvider.GetUtcNow();

        foreach (var role in new[] { EventCharacterRole.Playing, EventCharacterRole.Informational })
        {
            var target = desired.SingleOrDefault(x => x?.Role == role);
            var existing = current.SingleOrDefault(x => x.EventRole == role);
            if (target is null)
            {
                existing?.Release(actorAccountId, now);
                continue;
            }

            if (existing is not null &&
                currentCharacters[existing.OsrsCharacterId].NormalizedName == target.NormalizedName)
            {
                if (role == EventCharacterRole.Playing)
                    existing.UpdatePlayingEhb(target.Ehb!.Value, target.Source!.Value);
                continue;
            }

            existing?.Release(actorAccountId, now);
            var character = await db.OsrsCharacters.SingleOrDefaultAsync(
                x => x.NormalizedName == target.NormalizedName, cancellationToken);
            if (character is null)
            {
                await LockCharacterAsync(target.NormalizedName, cancellationToken);
                character = await db.OsrsCharacters.SingleOrDefaultAsync(
                    x => x.NormalizedName == target.NormalizedName, cancellationToken);
                if (character is null)
                {
                    character = new OsrsCharacter(Guid.NewGuid(), target.DisplayName, target.NormalizedName, now);
                    db.OsrsCharacters.Add(character);
                }
            }
            var reserved = await db.EventParticipantCharacters.AnyAsync(
                x => x.EventId == participant.EventId && x.OsrsCharacterId == character.Id &&
                     x.EventParticipantId != participant.Id && x.ReleasedAt == null,
                cancellationToken);
            if (reserved) throw new InvalidOperationException("That account is already signed up for this event.");

            db.EventParticipantCharacters.Add(new EventParticipantCharacter(
                Guid.NewGuid(), participant.EventId, participant.Id, character.Id, nextOrder++, now,
                actorAccountId, null, role, target.Ehb, target.Source, null));
        }
    }

    public async Task ReleaseAllAsync(
        Guid participantId, Guid? actorAccountId, CancellationToken cancellationToken)
    {
        var assignments = await db.EventParticipantCharacters
            .Where(x => x.EventParticipantId == participantId && x.ReleasedAt == null)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var assignment in assignments) assignment.Release(actorAccountId, now);
    }

    private sealed record Desired(
        string DisplayName, string NormalizedName, EventCharacterRole Role, decimal? Ehb, EhbSource? Source);

    private Task<int> LockCharacterAsync(string normalizedName, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({normalizedName}, 0))", cancellationToken);
}
