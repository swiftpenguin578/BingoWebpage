using System.Data;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bingo.Web.Security;

/// <summary>Account-scoped My Accounts mutations. A global character is never an ownership claim.</summary>
public sealed class MyAccountsService(ApplicationDbContext db, TimeProvider time)
{
    public async Task<IReadOnlyList<MyAccountCharacter>> ListAsync(Guid accountId, CancellationToken ct)
    {
        await RequireWebsiteAccountAsync(accountId, ct);
        var links = await (
            from link in db.AccountOsrsCharacters.AsNoTracking()
            join character in db.OsrsCharacters.AsNoTracking() on link.OsrsCharacterId equals character.Id
            where link.AccountId == accountId && link.Active
            orderby link.Position, link.LinkedAt, link.Id
            select new MyAccountCharacter(link.Id, character.DisplayName, link.PersonalLabel, link.SavedEhb, link.Preferred, link.Position, false))
            .ToListAsync(ct);

        if (links.Count == 0) return links;
        var registrations = await (
            from link in db.AccountOsrsCharacters.AsNoTracking()
            join assignment in db.EventParticipantCharacters.AsNoTracking() on link.OsrsCharacterId equals assignment.OsrsCharacterId
            join participant in db.EventParticipants.AsNoTracking() on assignment.EventParticipantId equals participant.Id
            join bingoEvent in db.Events.AsNoTracking() on assignment.EventId equals bingoEvent.Id
            where link.AccountId == accountId && link.Active &&
                  assignment.ReleasedAt == null && participant.AccountId == accountId &&
                  (participant.SignupStatus == SignupStatus.Confirmed || participant.SignupStatus == SignupStatus.WaitingList) &&
                  (bingoEvent.State == EventState.Draft || bingoEvent.State == EventState.SignupOpen || bingoEvent.State == EventState.SignupClosed || bingoEvent.State == EventState.Live)
            select link.Id).Distinct().ToListAsync(ct);
        var warned = registrations.ToHashSet();
        return links.Select(link => link with { HasUpcomingOrLiveRegistration = warned.Contains(link.Id) }).ToList();
    }

    public async Task AddOrReactivateAsync(Guid accountId, string characterName, string? label, decimal? savedEhb, CancellationToken ct)
    {
        var cleanName = RequireCharacterName(characterName);
        savedEhb = NormalizeEhb(savedEhb);
        await using var transaction = await BeginAccountTransactionAsync(accountId, ct);
        var now = time.GetUtcNow();
        await LockCharacterAsync(cleanName, ct);
        var character = await FindOrCreateCharacterAsync(cleanName, now, ct);
        var link = await db.AccountOsrsCharacters.SingleOrDefaultAsync(
            item => item.AccountId == accountId && item.OsrsCharacterId == character.Id, ct);
        if (link is null)
        {
            var position = (await db.AccountOsrsCharacters.Where(item => item.AccountId == accountId && item.Active)
                .MaxAsync(item => (int?)item.Position, ct) ?? -1) + 1;
            db.AccountOsrsCharacters.Add(new AccountOsrsCharacter(Guid.NewGuid(), accountId, character.Id, accountId, false, position, label, savedEhb, now));
        }
        else if (link.Active)
        {
            throw new InvalidOperationException("That character is already in your My Accounts list.");
        }
        else
        {
            link.Relink(accountId, now);
            link.UpdatePreferences(label, link.Position, false, savedEhb, now);
        }

        await NormalizePreferredAsync(accountId, now, ct);
        await SaveAndCommitAsync(transaction, ct);
    }

    public async Task UpdateAsync(Guid accountId, Guid linkId, string characterName, string? label, decimal? savedEhb, CancellationToken ct)
    {
        var cleanName = RequireCharacterName(characterName);
        savedEhb = NormalizeEhb(savedEhb);
        await using var transaction = await BeginAccountTransactionAsync(accountId, ct);
        await LockCharacterAsync(cleanName, ct);
        var link = await ActiveLinkAsync(accountId, linkId, ct);
        var now = time.GetUtcNow();
        var corrected = await FindOrCreateCharacterAsync(cleanName, now, ct);
        await ApplyCharacterCorrectionAsync(accountId, link, corrected, now, ct);
        link.UpdatePreferences(label, link.Position, link.Preferred, savedEhb, now);
        await NormalizePreferredAsync(accountId, now, ct);
        await SaveAndCommitAsync(transaction, ct);
    }

    public async Task MoveAsync(Guid accountId, Guid linkId, int direction, CancellationToken ct)
    {
        if (direction is not (-1 or 1)) throw new InvalidOperationException("That move is not available.");
        await using var transaction = await BeginAccountTransactionAsync(accountId, ct);
        var links = await db.AccountOsrsCharacters.Where(item => item.AccountId == accountId && item.Active)
            .OrderBy(item => item.Position).ThenBy(item => item.LinkedAt).ThenBy(item => item.Id).ToListAsync(ct);
        var index = links.FindIndex(item => item.Id == linkId);
        if (index < 0) throw new InvalidOperationException("That character is no longer available in your My Accounts list.");
        var next = index + direction;
        if (next < 0 || next >= links.Count) throw new InvalidOperationException("That move is not available.");
        var now = time.GetUtcNow();
        var first = links[index];
        var second = links[next];
        var firstPosition = first.Position;
        var secondPosition = second.Position;
        first.UpdatePreferences(first.PersonalLabel, secondPosition, first.Preferred, first.SavedEhb, now);
        second.UpdatePreferences(second.PersonalLabel, firstPosition, second.Preferred, second.SavedEhb, now);
        foreach (var link in links.Where(item => item.Preferred))
            link.UpdatePreferences(link.PersonalLabel, link.Position, false, link.SavedEhb, now);
        await SaveChangesSafelyAsync(ct);
        await NormalizePreferredAsync(accountId, now, ct);
        await SaveAndCommitAsync(transaction, ct);
    }

    public async Task UnlinkAsync(Guid accountId, Guid linkId, bool confirmedRegistrationWarning, CancellationToken ct)
    {
        await using var transaction = await BeginAccountTransactionAsync(accountId, ct);
        var link = await ActiveLinkAsync(accountId, linkId, ct);
        if (!confirmedRegistrationWarning && await HasUpcomingOrLiveRegistrationAsync(accountId, link.OsrsCharacterId, ct))
            throw new MyAccountsConfirmationRequiredException();
        var now = time.GetUtcNow();
        link.Unlink(now);
        await NormalizePreferredAsync(accountId, now, ct);
        await SaveAndCommitAsync(transaction, ct);
    }

    public async Task CorrectAsync(Guid accountId, Guid linkId, string correctedName, CancellationToken ct)
    {
        var cleanName = RequireCharacterName(correctedName);
        await using var transaction = await BeginAccountTransactionAsync(accountId, ct);
        await LockCharacterAsync(cleanName, ct);
        var link = await ActiveLinkAsync(accountId, linkId, ct);
        var now = time.GetUtcNow();
        var corrected = await FindOrCreateCharacterAsync(cleanName, now, ct);
        await ApplyCharacterCorrectionAsync(accountId, link, corrected, now, ct);
        await NormalizePreferredAsync(accountId, now, ct);
        await SaveAndCommitAsync(transaction, ct);
    }

    private async Task ApplyCharacterCorrectionAsync(Guid accountId, AccountOsrsCharacter link, OsrsCharacter corrected, DateTimeOffset now, CancellationToken ct)
    {
        if (corrected.Id == link.OsrsCharacterId) return;
        if (await db.AccountOsrsCharacters.AnyAsync(item => item.AccountId == accountId && item.OsrsCharacterId == corrected.Id, ct))
            throw new InvalidOperationException("That corrected character already has a separate My Accounts link.");

        var editable = await (
            from assignment in db.EventParticipantCharacters
            join participant in db.EventParticipants on assignment.EventParticipantId equals participant.Id
            join bingoEvent in db.Events on assignment.EventId equals bingoEvent.Id
            where assignment.OsrsCharacterId == link.OsrsCharacterId && assignment.ReleasedAt == null &&
                  participant.AccountId == accountId && (participant.SignupStatus == SignupStatus.Confirmed || participant.SignupStatus == SignupStatus.WaitingList) &&
                  bingoEvent.State == EventState.SignupOpen && !bingoEvent.DraftLocked && now < bingoEvent.SignupClosesAt
            select new EditableAssignment(assignment, participant.Id, bingoEvent.Id, bingoEvent.Name)).ToListAsync(ct);

        var eventIds = editable.Select(item => item.EventId).Distinct().ToList();
        if (eventIds.Count > 0)
        {
            var conflicts = await (
                from assignment in db.EventParticipantCharacters
                join bingoEvent in db.Events on assignment.EventId equals bingoEvent.Id
                where eventIds.Contains(assignment.EventId) && assignment.OsrsCharacterId == corrected.Id &&
                      assignment.ReleasedAt == null && !editable.Select(item => item.ParticipantId).Contains(assignment.EventParticipantId)
                select bingoEvent.Name).Distinct().ToListAsync(ct);
            if (conflicts.Count > 0) throw new MyAccountsCorrectionConflictException(conflicts[0]);
        }

        link.CorrectCharacter(corrected.Id, now);
        foreach (var assignment in editable) assignment.Assignment.ReplaceCharacter(corrected.Id);
    }

    public async Task<bool> IsWebsiteAccountAsync(Guid accountId, CancellationToken ct) =>
        await db.Accounts.AsNoTracking().AnyAsync(account => account.Id == accountId && account.AccountType == AccountType.WebsiteAccount, ct);

    private async Task<IDbContextTransaction> BeginAccountTransactionAsync(Guid accountId, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({accountId.ToString()}, 0))", ct);
            await RequireWebsiteAccountAsync(accountId, ct);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private async Task<AccountOsrsCharacter> ActiveLinkAsync(Guid accountId, Guid linkId, CancellationToken ct) =>
        await db.AccountOsrsCharacters.SingleOrDefaultAsync(item => item.Id == linkId && item.AccountId == accountId && item.Active, ct)
            ?? throw new InvalidOperationException("That character is no longer available in your My Accounts list.");

    private async Task RequireWebsiteAccountAsync(Guid accountId, CancellationToken ct)
    {
        if (!await IsWebsiteAccountAsync(accountId, ct)) throw new InvalidOperationException("My Accounts is only available to website accounts.");
    }

    private async Task<OsrsCharacter> FindOrCreateCharacterAsync(string displayName, DateTimeOffset now, CancellationToken ct)
    {
        var normalized = AccountIdentityService.NormalizeOsrsCharacterName(displayName);
        var character = await db.OsrsCharacters.SingleOrDefaultAsync(item => item.NormalizedName == normalized, ct);
        if (character is not null) return character;
        character = new OsrsCharacter(Guid.NewGuid(), displayName, normalized, now);
        db.OsrsCharacters.Add(character);
        return character;
    }

    private Task<int> LockCharacterAsync(string displayName, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({AccountIdentityService.NormalizeOsrsCharacterName(displayName)}, 0))", ct);

    private async Task<bool> HasUpcomingOrLiveRegistrationAsync(Guid accountId, Guid characterId, CancellationToken ct) =>
        await (
            from assignment in db.EventParticipantCharacters
            join participant in db.EventParticipants on assignment.EventParticipantId equals participant.Id
            join bingoEvent in db.Events on assignment.EventId equals bingoEvent.Id
            where assignment.OsrsCharacterId == characterId && assignment.ReleasedAt == null && participant.AccountId == accountId &&
                  (participant.SignupStatus == SignupStatus.Confirmed || participant.SignupStatus == SignupStatus.WaitingList) &&
                  (bingoEvent.State == EventState.Draft || bingoEvent.State == EventState.SignupOpen || bingoEvent.State == EventState.SignupClosed || bingoEvent.State == EventState.Live)
            select assignment.Id).AnyAsync(ct);

    private async Task NormalizePreferredAsync(Guid accountId, DateTimeOffset now, CancellationToken ct)
    {
        await db.AccountOsrsCharacters.Where(item => item.AccountId == accountId).LoadAsync(ct);
        var links = db.AccountOsrsCharacters.Local
            .Where(item => item.AccountId == accountId && item.Active)
            .OrderBy(item => item.Position).ThenBy(item => item.LinkedAt).ThenBy(item => item.Id)
            .ToList();
        foreach (var link in links.Where(item => item.Preferred))
        {
            link.UpdatePreferences(link.PersonalLabel, link.Position, false, link.SavedEhb, now);
        }
        for (var position = 0; position < links.Count; position++)
        {
            var link = links[position];
            var preferred = position == 0;
            if (link.Position == position && link.Preferred == preferred) continue;
            link.UpdatePreferences(link.PersonalLabel, position, preferred, link.SavedEhb, now);
        }
    }

    private async Task SaveAndCommitAsync(IDbContextTransaction transaction, CancellationToken ct)
    {
        try
        {
            await SaveChangesSafelyAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception exception) when (exception is DbUpdateException or DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw new InvalidOperationException("Your My Accounts changes conflicted with another update. Please reload and try again.");
        }
    }

    private async Task SaveChangesSafelyAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (Exception exception) when (exception is DbUpdateException or DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw new InvalidOperationException("Your My Accounts changes conflicted with another update. Please reload and try again.");
        }
    }

    private static string RequireCharacterName(string value)
    {
        var result = value.Trim();
        if (string.IsNullOrWhiteSpace(result)) throw new InvalidOperationException("An OSRS character name is required.");
        if (result.Length > 100) throw new InvalidOperationException("An OSRS character name must be 100 characters or fewer.");
        return result;
    }

    public static decimal RoundEhb(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal? NormalizeEhb(decimal? savedEhb)
    {
        if (savedEhb < 0) throw new InvalidOperationException("EHB cannot be negative.");
        return savedEhb is { } value ? RoundEhb(value) : null;
    }

    private sealed record EditableAssignment(EventParticipantCharacter Assignment, Guid ParticipantId, Guid EventId, string EventName);
}

public sealed record MyAccountCharacter(Guid Id, string CharacterName, string? PersonalLabel, decimal? SavedEhb, bool Preferred, int Position, bool HasUpcomingOrLiveRegistration);
public sealed class MyAccountsConfirmationRequiredException : InvalidOperationException;
public sealed class MyAccountsCorrectionConflictException(string eventName) : InvalidOperationException
{
    public string EventName { get; } = eventName;
}
