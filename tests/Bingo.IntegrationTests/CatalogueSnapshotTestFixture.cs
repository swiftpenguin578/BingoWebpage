using System.Text.Json;
using System.Text.Json.Serialization;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Catalogue;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

internal static class CatalogueSnapshotTestFixture
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task WriteAsync(
        ApplicationDbContext db,
        string path,
        CancellationToken cancellationToken = default)
    {
        var bosses = await db.BossActivities.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var items = await db.CatalogueItems.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var drops = await db.SourceDrops.AsNoTracking()
            .OrderBy(x => x.BossActivityId)
            .ThenBy(x => x.ItemId)
            .ToListAsync(cancellationToken);
        var snapshot = new CatalogueSnapshotService.CatalogueSnapshot(
            2,
            DateTimeOffset.UtcNow,
            bosses.Select(x => new CatalogueSnapshotService.BossRecord(
                x.Id,
                x.Name,
                x.Slug,
                x.Category,
                x.EfficientCompletionsPerHour,
                x.ExternalIdentifier,
                x.DataSource,
                x.ImageUrl,
                x.Active,
                x.Notes,
                x.MappingStatus,
                x.MappingCheckedAt,
                x.TeamSize)).ToArray(),
            items.Select(x => new CatalogueSnapshotService.ItemRecord(
                x.Id,
                x.Name,
                x.NormalizedName,
                x.ExternalIdentifier,
                x.ImageUrl,
                x.Active,
                x.Notes,
                x.CatalogueValueGp,
                x.PriceSource,
                x.PriceObservedAt,
                x.MappingStatus,
                x.MappingCheckedAt,
                x.MatchedApiName,
                x.MatchedApiIcon,
                x.RejectedPriceGp,
                x.RejectedPriceObservedAt)).ToArray(),
            drops.Select(x => new CatalogueSnapshotService.DropRecord(
                x.Id,
                x.BossActivityId,
                x.ItemId,
                x.DisplayRate,
                x.NumericProbability,
                x.RateConditionNote,
                x.DefaultEhbEstimate,
                x.ProbabilityScope,
                x.ConditionalOnParent,
                x.ParentProbability,
                x.AssumedParticipants,
                x.RollsPerCompletion,
                x.RollGroup,
                x.DataSource,
                x.Active)).ToArray());

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(snapshot, JsonOptions) + Environment.NewLine,
            cancellationToken);
    }
}
