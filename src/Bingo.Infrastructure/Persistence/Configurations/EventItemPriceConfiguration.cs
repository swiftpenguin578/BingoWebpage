using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class EventItemPriceConfiguration : IEntityTypeConfiguration<EventItemPrice>
{
    public void Configure(EntityTypeBuilder<EventItemPrice> builder)
    {
        builder.ToTable("event_item_prices", table =>
        {
            table.HasCheckConstraint("ck_event_item_price_value", "value_gp >= 0 AND (api_item_id IS NULL OR api_item_id > 0)");
            table.HasCheckConstraint("ck_event_item_price_hour", "date_trunc('hour', selected_hour AT TIME ZONE 'UTC') = selected_hour AT TIME ZONE 'UTC' AND selected_hour + interval '1 hour' <= captured_at");
            table.HasCheckConstraint("ck_event_item_price_provenance", """
                (source = 'WikiHourly' AND api_item_id IS NOT NULL AND price_observed_at IS NOT NULL AND price_observed_at = selected_hour AND fallback_catalogue_source IS NULL AND fallback_reason IS NULL)
                OR (source IN ('CatalogueFallback', 'CatalogueIntroduction') AND fallback_catalogue_source IS NOT NULL
                    AND fallback_catalogue_source IN ('Api', 'Manual', 'Untradeable') AND (fallback_catalogue_source <> 'Untradeable' OR value_gp = 0)
                    AND ((source = 'CatalogueIntroduction' AND fallback_reason IS NULL)
                        OR (source = 'CatalogueFallback' AND fallback_reason IS NOT NULL AND fallback_reason IN ('NoMapping', 'NoHourlyPrice', 'ProviderUnavailable', 'PreparedHourChanged', 'PriceMoveRejected'))))
                """);
        });
        builder.HasKey(x => new { x.EventId, x.ItemId });
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.ItemId).HasColumnName("item_id");
        builder.Property(x => x.ValueGp).HasColumnName("value_gp");
        builder.Property(x => x.SelectedHour).HasColumnName("selected_hour");
        builder.Property(x => x.CapturedAt).HasColumnName("captured_at");
        builder.Property(x => x.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ApiItemId).HasColumnName("api_item_id");
        builder.Property(x => x.PriceObservedAt).HasColumnName("price_observed_at");
        builder.Property(x => x.FallbackCatalogueSource).HasColumnName("fallback_catalogue_source").HasConversion<string>().HasMaxLength(24);
        builder.Property(x => x.FallbackReason).HasColumnName("fallback_reason").HasConversion<string>().HasMaxLength(32);
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CatalogueItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}
