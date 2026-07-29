using System.Collections.Concurrent;

namespace Bingo.Web.Security;

/// <summary>Independent generic failure windows for a normalized identifier and source network.</summary>
public sealed class LoginThrottleService(TimeProvider time)
{
    private readonly ConcurrentDictionary<string, Window> windows = new();
    public bool IsBlocked(string normalizedUsername, string network) => IsBlocked("id:" + normalizedUsername) || IsBlocked("net:" + network);
    public void RecordFailure(string normalizedUsername, string network) { Record("id:" + normalizedUsername); Record("net:" + network); }
    public void Clear(string normalizedUsername) => windows.TryRemove("id:" + normalizedUsername, out _);
    private bool IsBlocked(string key) => windows.TryGetValue(key, out var value) && value.ExpiresAt > time.GetUtcNow() && value.Count >= 5;
    private void Record(string key) => windows.AddOrUpdate(key, _ => new Window(1, time.GetUtcNow().AddMinutes(1)), (_, old) => old.ExpiresAt <= time.GetUtcNow() ? new Window(1, time.GetUtcNow().AddMinutes(1)) : old with { Count = old.Count + 1 });
    private sealed record Window(int Count, DateTimeOffset ExpiresAt);
}
