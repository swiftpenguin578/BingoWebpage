namespace Bingo.Domain.Access;

public sealed class OsrsCharacter { private OsrsCharacter() { } public OsrsCharacter(Guid id, string displayName, string normalizedName, DateTimeOffset now) { Id = id; DisplayName = displayName.Trim(); NormalizedName = normalizedName; CreatedAt = UpdatedAt = now.ToUniversalTime(); } public Guid Id { get; private set; } public string DisplayName { get; private set; } = string.Empty; public string NormalizedName { get; private set; } = string.Empty; public DateTimeOffset CreatedAt { get; private set; } public DateTimeOffset UpdatedAt { get; private set; } }
