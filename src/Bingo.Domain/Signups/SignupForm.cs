namespace Bingo.Domain.Signups;

public sealed class SignupForm
{
    private SignupForm() { }

    public SignupForm(Guid id, Guid eventId, DateTimeOffset createdAt)
    {
        Id = id;
        EventId = eventId;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset? FirstResponseAt { get; private set; }
    public bool RequireSignupCode { get; private set; }
    public string? SignupCodeHash { get; private set; }

    public void Publish(DateTimeOffset now) => PublishedAt ??= now.ToUniversalTime();
    public void Close(DateTimeOffset now) => ClosedAt = now.ToUniversalTime();
    public void RecordAcceptedResponse(DateTimeOffset now) => FirstResponseAt ??= now.ToUniversalTime();
    public void ConfigureSignupCode(bool required, string? hash)
    {
        if (required && string.IsNullOrWhiteSpace(hash)) throw new ArgumentException("A required signup code needs a hash.", nameof(hash));
        RequireSignupCode = required;
        SignupCodeHash = required ? hash : null;
    }
    public void AdvanceVersion() => Version++;
}
