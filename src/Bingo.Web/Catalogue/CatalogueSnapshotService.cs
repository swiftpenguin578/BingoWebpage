using System.Text.Json;
using System.Text.Json.Serialization;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Catalogue;

public sealed class CatalogueSnapshotService(ApplicationDbContext db, TimeProvider time)
{
    public const string DefaultRelativePath = "data/osrs-catalogue.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<SnapshotCounts> ExportAsync(string path, CancellationToken cancellationToken = default)
    {
        var bosses = await db.BossActivities.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var items = await db.CatalogueItems.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var drops = await db.SourceDrops.AsNoTracking().OrderBy(x => x.BossActivityId).ThenBy(x => x.ItemId).ToListAsync(cancellationToken);
        var snapshot = new CatalogueSnapshot(
            1,
            DateTimeOffset.UtcNow,
            bosses.Select(x => new BossRecord(x.Id, x.Name, x.Slug, x.Category, x.EfficientCompletionsPerHour, x.ExternalIdentifier, x.DataSource, x.ImageUrl, x.Active, x.Notes)).ToArray(),
            items.Select(x => new ItemRecord(x.Id, x.Name, x.NormalizedName, x.ExternalIdentifier, x.ImageUrl, x.Active, x.Notes)).ToArray(),
            drops.Select(x => new DropRecord(x.Id, x.BossActivityId, x.ItemId, x.DisplayRate, x.NumericProbability, x.RateConditionNote, x.DefaultEhbEstimate, x.ProbabilityScope, x.ConditionalOnParent, x.ParentProbability, x.AssumedParticipants, x.RollsPerCompletion, x.RollGroup, x.DataSource, x.Active)).ToArray());

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(snapshot, JsonOptions) + Environment.NewLine, cancellationToken);
        return snapshot.Counts;
    }

    public async Task<SnapshotCounts> ApplyAsync(string path, CancellationToken cancellationToken = default)
    {
        var snapshot = JsonSerializer.Deserialize<CatalogueSnapshot>(await File.ReadAllTextAsync(path, cancellationToken), JsonOptions)
            ?? throw new InvalidOperationException("The catalogue snapshot is empty or invalid.");
        if (snapshot.SchemaVersion != 1) throw new InvalidOperationException($"Unsupported catalogue snapshot schema version {snapshot.SchemaVersion}.");

        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var bosses = await db.BossActivities.ToListAsync(cancellationToken);
        var bossIds = new Dictionary<Guid, Guid>();
        var appliedBossIds = new HashSet<Guid>();
        foreach (var record in snapshot.Bosses)
        {
            var boss = bosses.SingleOrDefault(x => x.Slug == record.Slug)
                ?? bosses.SingleOrDefault(x => x.Id == record.Id);
            if (boss is null)
            {
                boss = new BossActivity(record.Id, record.Name, record.Slug, record.Category, record.EfficientCompletionsPerHour, now);
                db.BossActivities.Add(boss);
                bosses.Add(boss);
            }
            boss.Update(record.Name, record.Category, record.EfficientCompletionsPerHour, record.ExternalIdentifier, record.DataSource, record.Notes, now, record.ImageUrl);
            boss.SetActive(record.Active);
            bossIds[record.Id] = boss.Id;
            appliedBossIds.Add(boss.Id);
        }
        foreach (var boss in bosses.Where(x => !appliedBossIds.Contains(x.Id))) boss.SetActive(false);

        var items = await db.CatalogueItems.ToListAsync(cancellationToken);
        var itemIds = new Dictionary<Guid, Guid>();
        var appliedItemIds = new HashSet<Guid>();
        foreach (var record in snapshot.Items)
        {
            var item = items.SingleOrDefault(x => x.NormalizedName == record.NormalizedName)
                ?? items.SingleOrDefault(x => x.Id == record.Id);
            if (item is null)
            {
                item = new CatalogueItem(record.Id, record.Name, record.NormalizedName);
                db.CatalogueItems.Add(item);
                items.Add(item);
            }
            item.Update(record.Name, record.NormalizedName, record.ExternalIdentifier, record.Notes, record.ImageUrl);
            item.SetActive(record.Active);
            itemIds[record.Id] = item.Id;
            appliedItemIds.Add(item.Id);
        }
        foreach (var item in items.Where(x => !appliedItemIds.Contains(x.Id))) item.SetActive(false);

        await db.SaveChangesAsync(cancellationToken);
        var drops = await db.SourceDrops.ToListAsync(cancellationToken);
        var dropIds = new Dictionary<Guid, Guid>();
        var appliedDropIds = new HashSet<Guid>();
        foreach (var record in snapshot.Drops)
        {
            var bossId = bossIds[record.BossActivityId];
            var itemId = itemIds[record.ItemId];
            var drop = drops.SingleOrDefault(x => x.BossActivityId == bossId && x.ItemId == itemId)
                ?? drops.SingleOrDefault(x => x.Id == record.Id);
            if (drop is null)
            {
                drop = new SourceDrop(record.Id, bossId, itemId, record.DisplayRate, record.NumericProbability, record.DefaultEhbEstimate, now);
                db.SourceDrops.Add(drop);
                drops.Add(drop);
            }
            drop.Update(record.DisplayRate, record.NumericProbability, record.RateConditionNote, record.DefaultEhbEstimate, record.DataSource, now);
            drop.SetRateMechanics(record.ProbabilityScope, record.ConditionalOnParent, record.ParentProbability, record.AssumedParticipants, record.RollsPerCompletion, record.RollGroup);
            drop.SetActive(record.Active);
            dropIds[record.Id] = drop.Id;
            appliedDropIds.Add(drop.Id);
        }
        foreach (var drop in drops.Where(x => !appliedDropIds.Contains(x.Id))) drop.SetActive(false);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return snapshot.Counts;
    }

    public sealed record CatalogueSnapshot(int SchemaVersion, DateTimeOffset ExportedAt, BossRecord[] Bosses, ItemRecord[] Items, DropRecord[] Drops)
    {
        [JsonIgnore]
        public SnapshotCounts Counts => new(Bosses.Length, Items.Length, Drops.Length);
    }

    public sealed record BossRecord(Guid Id, string Name, string Slug, string Category, decimal? EfficientCompletionsPerHour, string? ExternalIdentifier, string? DataSource, string? ImageUrl, bool Active, string? Notes);
    public sealed record ItemRecord(Guid Id, string Name, string NormalizedName, string? ExternalIdentifier, string? ImageUrl, bool Active, string? Notes);
    public sealed record DropRecord(Guid Id, Guid BossActivityId, Guid ItemId, string DisplayRate, decimal? NumericProbability, string? RateConditionNote, decimal? DefaultEhbEstimate, DropProbabilityScope ProbabilityScope, bool ConditionalOnParent, decimal? ParentProbability, int AssumedParticipants, int RollsPerCompletion, string RollGroup, string? DataSource, bool Active);
    public sealed record SnapshotCounts(int Bosses, int Items, int Drops);
}
