using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class SeedBarrowsAndLunarDrops : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.Sql("""
                WITH item_data(name, normalized_name) AS (
                    VALUES
                        ('Ahrim''s hood', 'AHRIM''S HOOD'),
                        ('Ahrim''s robetop', 'AHRIM''S ROBETOP'),
                        ('Ahrim''s robeskirt', 'AHRIM''S ROBESKIRT'),
                        ('Ahrim''s staff', 'AHRIM''S STAFF'),
                        ('Dharok''s helm', 'DHAROK''S HELM'),
                        ('Dharok''s platebody', 'DHAROK''S PLATEBODY'),
                        ('Dharok''s platelegs', 'DHAROK''S PLATELEGS'),
                        ('Dharok''s greataxe', 'DHAROK''S GREATAXE'),
                        ('Guthan''s helm', 'GUTHAN''S HELM'),
                        ('Guthan''s platebody', 'GUTHAN''S PLATEBODY'),
                        ('Guthan''s chainskirt', 'GUTHAN''S CHAINSKIRT'),
                        ('Guthan''s warspear', 'GUTHAN''S WARSPEAR'),
                        ('Karil''s coif', 'KARIL''S COIF'),
                        ('Karil''s leathertop', 'KARIL''S LEATHERTOP'),
                        ('Karil''s leatherskirt', 'KARIL''S LEATHERSKIRT'),
                        ('Karil''s crossbow', 'KARIL''S CROSSBOW'),
                        ('Torag''s helm', 'TORAG''S HELM'),
                        ('Torag''s platebody', 'TORAG''S PLATEBODY'),
                        ('Torag''s platelegs', 'TORAG''S PLATELEGS'),
                        ('Torag''s hammers', 'TORAG''S HAMMERS'),
                        ('Verac''s helm', 'VERAC''S HELM'),
                        ('Verac''s brassard', 'VERAC''S BRASSARD'),
                        ('Verac''s plateskirt', 'VERAC''S PLATESKIRT'),
                        ('Verac''s flail', 'VERAC''S FLAIL'),
                        ('Blood moon helm', 'BLOOD MOON HELM'),
                        ('Blood moon chestplate', 'BLOOD MOON CHESTPLATE'),
                        ('Blood moon tassets', 'BLOOD MOON TASSETS'),
                        ('Dual macuahuitl', 'DUAL MACUAHUITL'),
                        ('Blue moon helm', 'BLUE MOON HELM'),
                        ('Blue moon chestplate', 'BLUE MOON CHESTPLATE'),
                        ('Blue moon tassets', 'BLUE MOON TASSETS'),
                        ('Blue moon spear', 'BLUE MOON SPEAR'),
                        ('Eclipse moon helm', 'ECLIPSE MOON HELM'),
                        ('Eclipse moon chestplate', 'ECLIPSE MOON CHESTPLATE'),
                        ('Eclipse moon tassets', 'ECLIPSE MOON TASSETS'),
                        ('Eclipse atlatl', 'ECLIPSE ATLATL')
                )
                INSERT INTO catalogue_items (id, name, normalized_name, active)
                SELECT gen_random_uuid(), name, normalized_name, TRUE
                FROM item_data
                ON CONFLICT (normalized_name) DO UPDATE SET name = EXCLUDED.name, active = TRUE;

                WITH drop_data(source_name, item_name, display_rate, probability, source_url) AS (
                    VALUES
                        ('Barrows Chests', 'Ahrim''s hood', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Ahrim''s robetop', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Ahrim''s robeskirt', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Ahrim''s staff', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Dharok''s helm', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Dharok''s platebody', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Dharok''s platelegs', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Dharok''s greataxe', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Guthan''s helm', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Guthan''s platebody', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Guthan''s chainskirt', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Guthan''s warspear', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Karil''s coif', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Karil''s leathertop', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Karil''s leatherskirt', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Karil''s crossbow', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Torag''s helm', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Torag''s platebody', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Torag''s platelegs', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Torag''s hammers', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Verac''s helm', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Verac''s brassard', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Verac''s plateskirt', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Barrows Chests', 'Verac''s flail', '7 × 1/2,448', 7.0 / 2448.0, 'https://oldschool.runescape.wiki/w/Chest_(Barrows)'),
                        ('Lunar Chests', 'Blood moon helm', '1/224', 1.0 / 224.0, 'https://oldschool.runescape.wiki/w/Lunar_Chest'),
                        ('Lunar Chests', 'Blood moon chestplate', '1/224', 1.0 / 224.0, 'https://oldschool.runescape.wiki/w/Lunar_Chest'),
                        ('Lunar Chests', 'Blood moon tassets', '1/224', 1.0 / 224.0, 'https://oldschool.runescape.wiki/w/Lunar_Chest'),
                        ('Lunar Chests', 'Dual macuahuitl', '1/224', 1.0 / 224.0, 'https://oldschool.runescape.wiki/w/Lunar_Chest'),
                        ('Lunar Chests', 'Blue moon helm', '1/224', 1.0 / 224.0, 'https://oldschool.runescape.wiki/w/Lunar_Chest'),
                        ('Lunar Chests', 'Blue moon chestplate', '1/224', 1.0 / 224.0, 'https://oldschool.runescape.wiki/w/Lunar_Chest'),
                        ('Lunar Chests', 'Blue moon tassets', '1/224', 1.0 / 224.0, 'https://oldschool.runescape.wiki/w/Lunar_Chest'),
                        ('Lunar Chests', 'Blue moon spear', '1/224', 1.0 / 224.0, 'https://oldschool.runescape.wiki/w/Lunar_Chest'),
                        ('Lunar Chests', 'Eclipse moon helm', '1/224', 1.0 / 224.0, 'https://oldschool.runescape.wiki/w/Lunar_Chest'),
                        ('Lunar Chests', 'Eclipse moon chestplate', '1/224', 1.0 / 224.0, 'https://oldschool.runescape.wiki/w/Lunar_Chest'),
                        ('Lunar Chests', 'Eclipse moon tassets', '1/224', 1.0 / 224.0, 'https://oldschool.runescape.wiki/w/Lunar_Chest'),
                        ('Lunar Chests', 'Eclipse atlatl', '1/224', 1.0 / 224.0, 'https://oldschool.runescape.wiki/w/Lunar_Chest')
                )
                INSERT INTO source_drops
                    (id, boss_activity_id, item_id, display_rate, numeric_probability, default_ehb_estimate, data_source, data_updated_at, active)
                SELECT gen_random_uuid(), boss.id, item.id, data.display_rate, data.probability,
                       1.0 / (boss.efficient_completions_per_hour * data.probability), data.source_url, NOW(), TRUE
                FROM drop_data data
                JOIN boss_activities boss ON boss.name = data.source_name
                JOIN catalogue_items item ON item.normalized_name = UPPER(data.item_name)
                ON CONFLICT (boss_activity_id, item_id) DO UPDATE SET
                    display_rate = EXCLUDED.display_rate,
                    numeric_probability = EXCLUDED.numeric_probability,
                    default_ehb_estimate = EXCLUDED.default_ehb_estimate,
                    data_source = EXCLUDED.data_source,
                    data_updated_at = EXCLUDED.data_updated_at,
                    active = TRUE;
                """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.Sql("""
                DELETE FROM source_drops
                WHERE boss_activity_id IN (
                    SELECT id FROM boss_activities WHERE name IN ('Barrows Chests', 'Lunar Chests'))
                  AND data_source IN (
                    'https://oldschool.runescape.wiki/w/Chest_(Barrows)',
                    'https://oldschool.runescape.wiki/w/Lunar_Chest');
                """);
    }
}
