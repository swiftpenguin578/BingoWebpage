using Bingo.Domain.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class BossActivityConfiguration : IEntityTypeConfiguration<BossActivity>
{
    public void Configure(EntityTypeBuilder<BossActivity> builder)
    {
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
        builder.ToTable("catalogue_items"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200); builder.Property(x => x.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200); builder.HasIndex(x => x.NormalizedName).IsUnique(); builder.Property(x => x.ExternalIdentifier).HasColumnName("external_identifier").HasMaxLength(200); builder.Property(x => x.ImageUrl).HasColumnName("image_url").HasMaxLength(2000); builder.Property(x => x.Active).HasColumnName("active"); builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken(); builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(4000);
    }
}
public sealed class SourceDropConfiguration : IEntityTypeConfiguration<SourceDrop>
{
    public void Configure(EntityTypeBuilder<SourceDrop> builder)
    {
        builder.ToTable("source_drops"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.BossActivityId).HasColumnName("boss_activity_id"); builder.Property(x => x.ItemId).HasColumnName("item_id"); builder.HasIndex(x => new { x.BossActivityId, x.ItemId }).IsUnique(); builder.Property(x => x.DisplayRate).HasColumnName("display_rate").HasMaxLength(200); builder.Property(x => x.NumericProbability).HasColumnName("numeric_probability").HasPrecision(18, 12); builder.Property(x => x.ProbabilityScope).HasColumnName("probability_scope").HasConversion<string>().HasMaxLength(20).HasDefaultValue(DropProbabilityScope.Participant); builder.Property(x => x.ConditionalOnParent).HasColumnName("conditional_on_parent"); builder.Property(x => x.ParentProbability).HasColumnName("parent_probability").HasPrecision(18, 12); builder.Property(x => x.AssumedParticipants).HasColumnName("assumed_participants").HasDefaultValue(1); builder.Property(x => x.RollsPerCompletion).HasColumnName("rolls_per_completion").HasDefaultValue(1); builder.Property(x => x.RollGroup).HasColumnName("roll_group").HasMaxLength(120).HasDefaultValue("default"); builder.Property(x => x.RateConditionNote).HasColumnName("rate_condition_note").HasMaxLength(2000); builder.Property(x => x.DefaultEhbEstimate).HasColumnName("default_ehb_estimate").HasPrecision(12, 4); builder.Property(x => x.DataSource).HasColumnName("data_source").HasMaxLength(300); builder.Property(x => x.DataUpdatedAt).HasColumnName("data_updated_at"); builder.Property(x => x.Active).HasColumnName("active"); builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
    }
}
