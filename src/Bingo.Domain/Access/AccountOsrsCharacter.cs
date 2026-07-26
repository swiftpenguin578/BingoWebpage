namespace Bingo.Domain.Access;

public sealed class AccountOsrsCharacter
{
    private AccountOsrsCharacter() { }

    public AccountOsrsCharacter(
        Guid id,
        Guid accountId,
        Guid osrsCharacterId,
        bool preferred,
        int position,
        DateTimeOffset now)
        : this(id, accountId, osrsCharacterId, accountId, preferred, position, null, null, now)
    {
    }

    public AccountOsrsCharacter(
        Guid id,
        Guid accountId,
        Guid osrsCharacterId,
        Guid linkedByAccountId,
        bool preferred,
        int sortOrder,
        string? personalLabel,
        decimal? savedEhb,
        DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sortOrder);
        if (savedEhb < 0) throw new ArgumentOutOfRangeException(nameof(savedEhb));
        Id = id;
        AccountId = accountId;
        OsrsCharacterId = osrsCharacterId;
        LinkedByAccountId = linkedByAccountId;
        Active = true;
        Preferred = preferred;
        Position = sortOrder;
        PersonalLabel = CleanLabel(personalLabel);
        SavedEhb = savedEhb;
        LinkedAt = CreatedAt = UpdatedAt = now.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid OsrsCharacterId { get; private set; }
    public Guid LinkedByAccountId { get; private set; }
    public bool Active { get; private set; }
    public bool Preferred { get; private set; }
    public int Position { get; private set; }
    public int SortOrder => Position;
    public string? PersonalLabel { get; private set; }
    public decimal? SavedEhb { get; private set; }
    public DateTimeOffset LinkedAt { get; private set; }
    public DateTimeOffset? UnlinkedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public int Version { get; private set; }

    public void Unlink(DateTimeOffset now)
    {
        if (!Active) return;
        Active = false;
        Preferred = false;
        UnlinkedAt = now.ToUniversalTime();
        Touch(now);
    }

    public void Relink(Guid actorAccountId, DateTimeOffset now)
    {
        Active = true;
        LinkedByAccountId = actorAccountId;
        LinkedAt = now.ToUniversalTime();
        UnlinkedAt = null;
        Touch(now);
    }

    public void CorrectCharacter(Guid osrsCharacterId, DateTimeOffset now)
    {
        if (!Active) throw new InvalidOperationException("An unlinked character cannot be corrected.");
        OsrsCharacterId = osrsCharacterId;
        Touch(now);
    }

    public void UpdatePreferences(string? personalLabel, int sortOrder, bool preferred, decimal? savedEhb, DateTimeOffset now)
    {
        if (!Active) throw new InvalidOperationException("An unlinked character cannot be updated.");
        ArgumentOutOfRangeException.ThrowIfNegative(sortOrder);
        if (savedEhb < 0) throw new ArgumentOutOfRangeException(nameof(savedEhb));
        PersonalLabel = CleanLabel(personalLabel);
        Position = sortOrder;
        Preferred = preferred;
        SavedEhb = savedEhb;
        Touch(now);
    }

    public void AdvanceVersion() => Version++;

    private void Touch(DateTimeOffset now) => UpdatedAt = now.ToUniversalTime();

    private static string? CleanLabel(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
