using Bingo.Domain.Access;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Signups;

public sealed class EventParticipantCharacterService(ApplicationDbContext db, TimeProvider timeProvider)
{
    public Task AssignExternalRosterCharacterAsync(
        EventParticipant participant, string primaryName, decimal ehb, string? secondName,
        EhbSource source, Guid? actorAccountId, CancellationToken cancellationToken) =>
        AssignExternalRosterCharactersAsync(participant, primaryName, ehb,
            string.IsNullOrWhiteSpace(secondName) ? [] : [secondName], source, actorAccountId, cancellationToken);

    /// <summary>Retained for the separate pre-formed roster workflow; authenticated signup uses SignupForm assignments.</summary>
    public async Task AssignExternalRosterCharactersAsync(
        EventParticipant participant,
        string primaryName,
        decimal ehb,
        IReadOnlyCollection<string>? additionalNames,
        EhbSource source,
        Guid? actorAccountId,
        CancellationToken cancellationToken)
    {
        var normalizedPrimary = SignupService.NormalizeAccountName(primaryName);
        ArgumentOutOfRangeException.ThrowIfNegative(ehb);
        var informationals = (additionalNames ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => new { Display = name.Trim(), Normalized = SignupService.NormalizeAccountName(name) })
            .Where(name => name.Normalized != normalizedPrimary)
            .DistinctBy(name => name.Normalized)
            .ToList();
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

        foreach (var existing in current) existing.Release(actorAccountId, now);
        var desired = new[] { new Desired(primaryName.Trim(), normalizedPrimary, EventCharacterRole.Playing, (decimal?)ehb, (EhbSource?)source) }
            .Concat(informationals.Select(name => new Desired(name.Display, name.Normalized, EventCharacterRole.Informational, null, null)));
        foreach (var target in desired)
        {
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
                actorAccountId, null, target.Role, target.Ehb, target.Source, null));
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
