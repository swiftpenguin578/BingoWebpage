using Bingo.Domain.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class BossActivityConfiguration : IEntityTypeConfiguration<BossActivity>
{
    public void Configure(EntityTypeBuilder<BossActivity> builder)
    {
        builder.Property(x => x.MappingStatus).HasColumnName("mapping_status").HasDefaultValue(ApiMappingStatus.NotConfigured).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.MappingCheckedAt).HasColumnName("mapping_checked_at");
        builder.ToTable("boss_activities"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200); builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(150); builder.HasIndex(x => x.Slug).IsUnique();
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(100); builder.Property(x => x.EfficientCompletionsPerHour).HasColumnName("efficient_completions_per_hour").HasPrecision(12, 4);
        builder.Property(x => x.ExternalIdentifier).HasColumnName("external_identifier").HasMaxLength(200); builder.Property(x => x.DataSource).HasColumnName("data_source").HasMaxLength(300); builder.Property(x => x.ImageUrl).HasColumnName("image_url").HasMaxLength(2000); builder.Property(x => x.DataUpdatedAt).HasColumnName("data_updated_at"); builder.Property(x => x.Active).HasColumnName("active"); builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken(); builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(4000);
    }
}
public sealed class CatalogueItemConfiguration : IEntityTypeConfiguration<CatalogueItem>
{
    public void Configure(EntityTypeBuilder<CatalogueItem> builder)
    {
        builder.Property(x => x.ArtworkX).HasColumnName("artwork_x");
        builder.Property(x => x.ArtworkY).HasColumnName("artwork_y");
        builder.Property(x => x.ArtworkWidth).HasColumnName("artwork_width");
        builder.Property(x => x.ArtworkHeight).HasColumnName("artwork_height");
        builder.Property(x => x.ArtworkScale).HasColumnName("artwork_scale");
        builder.Property(x => x.ArtworkRotation).HasColumnName("artwork_rotation");
        builder.ToTable(table => table.HasCheckConstraint("ck_catalogue_item_artwork", "((artwork_x IS NULL AND artwork_y IS NULL AND artwork_width IS NULL AND artwork_height IS NULL AND artwork_scale IS NULL AND artwork_rotation IS NULL) OR (artwork_x IS NOT NULL AND artwork_y IS NOT NULL AND artwork_width IS NOT NULL AND artwork_height IS NOT NULL AND artwork_scale IS NOT NULL AND artwork_rotation IS NOT NULL AND artwork_x BETWEEN 0 AND 100 AND artwork_y BETWEEN 0 AND 100 AND artwork_width BETWEEN 5 AND 150 AND artwork_height BETWEEN 5 AND 200 AND artwork_scale BETWEEN 0.5 AND 2.5 AND artwork_rotation BETWEEN -180 AND 180))"));
        builder.Property(x => x.CatalogueValueGp).HasColumnName("catalogue_value_gp");
        builder.Property(x => x.PriceSource).HasColumnName("price_source").HasDefaultValue(CataloguePriceSource.Missing).HasConversion<string>().HasMaxLength(24);
        builder.Property(x => x.PriceObservedAt).HasColumnName("price_observed_at");
        builder.Property(x => x.RejectedPriceGp).HasColumnName("rejected_price_gp");
        builder.Property(x => x.RejectedPriceObservedAt).HasColumnName("rejected_price_observed_at");
        builder.ToTable(table => table.HasCheckConstraint("ck_catalogue_price_rejection", "(rejected_price_gp IS NULL AND rejected_price_observed_at IS NULL) OR (rejected_price_gp IS NOT NULL AND rejected_price_gp >= 0 AND rejected_price_observed_at IS NOT NULL)"));
        builder.Property(x => x.MappingStatus).HasColumnName("mapping_status").HasDefaultValue(ApiMappingStatus.NotConfigured).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.MappingCheckedAt).HasColumnName("mapping_checked_at");
        builder.Property(x => x.MatchedApiName).HasColumnName("matched_api_name").HasMaxLength(200);
        builder.Property(x => x.MatchedApiIcon).HasColumnName("matched_api_icon").HasMaxLength(500);
        builder.ToTable("catalogue_items", table => table.HasCheckConstraint("ck_catalogue_item_price", "(catalogue_value_gp IS NULL AND price_source = 'Missing') OR (catalogue_value_gp IS NOT NULL AND catalogue_value_gp >= 0 AND price_source IN ('Api', 'Manual', 'Untradeable') AND (price_source <> 'Untradeable' OR catalogue_value_gp = 0))")); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200); builder.Property(x => x.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200); builder.HasIndex(x => x.NormalizedName).IsUnique(); builder.Property(x => x.ExternalIdentifier).HasColumnName("external_identifier").HasMaxLength(200); builder.Property(x => x.ImageUrl).HasColumnName("image_url").HasMaxLength(2000); builder.Property(x => x.Active).HasColumnName("active"); builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken(); builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(4000);
    }
}
public sealed class SourceDropConfiguration : IEntityTypeConfiguration<SourceDrop>
{
    public void Configure(EntityTypeBuilder<SourceDrop> builder)
    {
        builder.ToTable("source_drops"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.BossActivityId).HasColumnName("boss_activity_id"); builder.Property(x => x.ItemId).HasColumnName("item_id"); builder.HasIndex(x => new { x.BossActivityId, x.ItemId }).IsUnique(); builder.Property(x => x.DisplayRate).HasColumnName("display_rate").HasMaxLength(200); builder.Property(x => x.NumericProbability).HasColumnName("numeric_probability").HasPrecision(18, 12); builder.Property(x => x.ProbabilityScope).HasColumnName("probability_scope").HasConversion<string>().HasMaxLength(20).HasDefaultValue(DropProbabilityScope.Participant); builder.Property(x => x.ConditionalOnParent).HasColumnName("conditional_on_parent"); builder.Property(x => x.ParentProbability).HasColumnName("parent_probability").HasPrecision(18, 12); builder.Property(x => x.AssumedParticipants).HasColumnName("assumed_participants").HasDefaultValue(1); builder.Property(x => x.RollsPerCompletion).HasColumnName("rolls_per_completion").HasDefaultValue(1); builder.Property(x => x.RollGroup).HasColumnName("roll_group").HasMaxLength(120).HasDefaultValue("default"); builder.Property(x => x.RateConditionNote).HasColumnName("rate_condition_note").HasMaxLength(2000); builder.Property(x => x.DefaultEhbEstimate).HasColumnName("default_ehb_estimate").HasPrecision(12, 4); builder.Property(x => x.DataSource).HasColumnName("data_source").HasMaxLength(300); builder.Property(x => x.DataUpdatedAt).HasColumnName("data_updated_at"); builder.Property(x => x.Active).HasColumnName("active"); builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
    }
}
