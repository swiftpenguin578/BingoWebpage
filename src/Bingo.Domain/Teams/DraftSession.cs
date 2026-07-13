namespace Bingo.Domain.Teams;

public sealed class DraftSession
{
    private DraftSession() { }
    public DraftSession(Guid id, Guid eventId, int targetTeamSize) { ArgumentOutOfRangeException.ThrowIfLessThan(targetTeamSize, 1); Id = id; EventId = eventId; TargetTeamSize = targetTeamSize; State = DraftState.Setup; ControlVersion = 1; }
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public int TargetTeamSize { get; private set; }
    public DraftState State { get; private set; }
    public DateTimeOffset? LockedAt { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }
    public Guid? ControllerAccountId { get; private set; }
    public DateTimeOffset? ControllerLeaseExpiresAt { get; private set; }
    public long ControlVersion { get; private set; }
    public void ConfigureTargetSize(int size) { if (State != DraftState.Setup) throw new InvalidOperationException("Team size cannot change after the draft starts."); ArgumentOutOfRangeException.ThrowIfLessThan(size, 1); TargetTeamSize = size; }
    public void Start(DateTimeOffset now) { if (State != DraftState.Setup) throw new InvalidOperationException("Only a draft in setup can start."); State = DraftState.Running; LockedAt = now.ToUniversalTime(); }
    public void Pause() { if (State != DraftState.Running) throw new InvalidOperationException("Only a running draft can be paused."); State = DraftState.Paused; }
    public void Resume() { if (State != DraftState.Paused) throw new InvalidOperationException("Only a paused draft can resume."); State = DraftState.Running; }
    public void Finalize(DateTimeOffset now) { if (State is not (DraftState.Running or DraftState.Paused)) throw new InvalidOperationException("Start the draft before finalizing it."); State = DraftState.Finalized; FinalizedAt = now.ToUniversalTime(); }
    public bool HasActiveController(DateTimeOffset now) => ControllerAccountId is not null && ControllerLeaseExpiresAt > now.ToUniversalTime();
    public Guid? AcquireControl(Guid accountId, DateTimeOffset now, TimeSpan leaseDuration, bool force = false)
    {
        if (State == DraftState.Finalized) throw new InvalidOperationException("A finalized draft cannot be controlled.");
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        var at = now.ToUniversalTime();
        var previous = HasActiveController(at) ? ControllerAccountId : null;
        if (previous is not null && previous != accountId && !force) throw new InvalidOperationException("Another administrator currently controls this draft.");
        ControllerAccountId = accountId;
        ControllerLeaseExpiresAt = at.Add(leaseDuration);
        ControlVersion++;
        return previous;
    }
    public void RenewControl(Guid accountId, DateTimeOffset now, TimeSpan leaseDuration)
    {
        RequireControl(accountId, now);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        ControllerLeaseExpiresAt = now.ToUniversalTime().Add(leaseDuration);
    }
    public void RequireControl(Guid accountId, DateTimeOffset now)
    {
        if (!HasActiveController(now) || ControllerAccountId != accountId) throw new InvalidOperationException("You no longer control this draft. Reload to see the current controller or explicitly take over.");
    }
    public void ReleaseControl(Guid accountId, DateTimeOffset now)
    {
        RequireControl(accountId, now);
        ControllerAccountId = null;
        ControllerLeaseExpiresAt = null;
        ControlVersion++;
    }
}
