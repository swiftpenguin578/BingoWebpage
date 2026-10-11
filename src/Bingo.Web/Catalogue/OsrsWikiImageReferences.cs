using System.Collections.Frozen;
using System.Diagnostics;
using Bingo.Application.Catalogue;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Catalogue;

/// <summary>
/// The OSRS Wiki image sources the app's own data references: catalogue bosses and
/// items, tile templates and board tiles. Pages only render wiki art from these
/// columns, so the anonymous image endpoint downloads a missing file only for them.
/// </summary>
public sealed class OsrsWikiImageReferences(IServiceScopeFactory scopes) : IDisposable
{
    // However many unknown sources are requested, the stored sources are reloaded
    // at most once per interval.
    internal static readonly TimeSpan MinimumReloadInterval = TimeSpan.FromSeconds(2);

    private readonly SemaphoreSlim reload = new(1, 1);
    private Snapshot? current;
    private long lastLoadStartedAt;

    /// <summary>Every stored wiki image source, as written by the catalogue, templates and boards.</summary>
    public static async Task<List<string>> LoadStoredSourcesAsync(ApplicationDbContext db, CancellationToken cancellationToken = default) =>
        (await db.BossActivities.AsNoTracking().Where(value => value.ImageUrl != null).Select(value => value.ImageUrl!).Distinct().ToListAsync(cancellationToken))
        .Concat(await db.CatalogueItems.AsNoTracking().Where(value => value.ImageUrl != null).Select(value => value.ImageUrl!).Distinct().ToListAsync(cancellationToken))
        .Concat(await db.TileTemplates.AsNoTracking().Where(value => value.ImageUrl != null).Select(value => value.ImageUrl!).Distinct().ToListAsync(cancellationToken))
        .Concat(await db.BoardTiles.AsNoTracking().Where(value => value.ImageUrlSnapshot != null).Select(value => value.ImageUrlSnapshot!).Distinct().ToListAsync(cancellationToken))
        .ToList();

    /// <summary>
    /// The stored spellings (after <see cref="OsrsWikiImageUrl.Normalize"/>) of every
    /// source that canonicalises to <paramref name="canonicalSource"/>, or null when
    /// the app references no such file.
    /// </summary>
    public async ValueTask<IReadOnlyList<string>?> FindAsync(string canonicalSource, CancellationToken cancellationToken)
    {
        var requestedAt = Stopwatch.GetTimestamp();
        var snapshot = Volatile.Read(ref current);
        if (snapshot is not null && snapshot.Sources.TryGetValue(canonicalSource, out var known)) return known;

        // Not in the last load, which may predate a save: answer from a load that
        // started after this request arrived, paced so misses cannot flood the database.
        await reload.WaitAsync(cancellationToken);
        try
        {
            snapshot = Volatile.Read(ref current);
            if (snapshot is null || snapshot.StartedAt < requestedAt)
            {
                if (lastLoadStartedAt != 0)
                {
                    var wait = MinimumReloadInterval - Stopwatch.GetElapsedTime(lastLoadStartedAt);
                    if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);
                }

                lastLoadStartedAt = Stopwatch.GetTimestamp();
                snapshot = await LoadAsync(lastLoadStartedAt);
                Volatile.Write(ref current, snapshot);
            }
        }
        finally
        {
            reload.Release();
        }

        return snapshot.Sources.GetValueOrDefault(canonicalSource);
    }

    private async Task<Snapshot> LoadAsync(long startedAt)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sources = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var stored in await LoadStoredSourcesAsync(db, CancellationToken.None))
        {
            var normalized = OsrsWikiImageUrl.Normalize(stored);
            if (normalized is null || OsrsWikiImageCache.CanonicalSource(normalized) is not { } canonical) continue;
            if (!sources.TryGetValue(canonical, out var spellings)) sources[canonical] = spellings = [];
            if (!spellings.Contains(normalized, StringComparer.Ordinal)) spellings.Add(normalized);
        }

        return new Snapshot(startedAt, sources.ToFrozenDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.ToArray(),
            StringComparer.Ordinal));
    }

    public void Dispose() => reload.Dispose();

    private sealed record Snapshot(long StartedAt, FrozenDictionary<string, IReadOnlyList<string>> Sources);
}
