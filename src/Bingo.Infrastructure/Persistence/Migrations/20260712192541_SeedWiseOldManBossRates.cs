using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class SeedWiseOldManBossRates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO boss_activities
                (id, name, slug, category, efficient_completions_per_hour, external_identifier, data_source, data_updated_at, active, notes, image_url)
            VALUES
                (gen_random_uuid(), 'Abyssal Sire', 'abyssal-sire', 'Boss', 50, 'abyssal_sire', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Alchemical Hydra', 'alchemical-hydra', 'Boss', 30, 'alchemical_hydra', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Amoxliatl', 'amoxliatl', 'Boss', 85, 'amoxliatl', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Araxxor', 'araxxor', 'Boss', 40, 'araxxor', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Artio', 'artio', 'Boss', 60, 'artio', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Barrows Chests', 'barrows-chests', 'Boss', 22, 'barrows_chests', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Brutus', 'brutus', 'Boss', 250, 'brutus', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Bryophyta', 'bryophyta', 'Boss', 9, 'bryophyta', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Callisto', 'callisto', 'Boss', 142, 'callisto', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Calvar''ion', 'calvarion', 'Boss', 55, 'calvarion', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Cerberus', 'cerberus', 'Boss', 65, 'cerberus', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Chambers of Xeric (CM)', 'chambers-of-xeric-cm', 'Boss', 3, 'chambers_of_xeric_challenge_mode', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Chambers of Xeric', 'chambers-of-xeric', 'Boss', 3.5, 'chambers_of_xeric', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Chaos Elemental', 'chaos-elemental', 'Boss', 120, 'chaos_elemental', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Chaos Fanatic', 'chaos-fanatic', 'Boss', 100, 'chaos_fanatic', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Commander Zilyana', 'commander-zilyana', 'Boss', 60, 'commander_zilyana', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Corporeal Beast', 'corporeal-beast', 'Boss', 60, 'corporeal_beast', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Crazy Archaeologist', 'crazy-archaeologist', 'Boss', 75, 'crazy_archaeologist', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Dagannoth Prime', 'dagannoth-prime', 'Boss', 105, 'dagannoth_prime', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Dagannoth Rex', 'dagannoth-rex', 'Boss', 105, 'dagannoth_rex', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Dagannoth Supreme', 'dagannoth-supreme', 'Boss', 105, 'dagannoth_supreme', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Deranged Archaeologist', 'deranged-archaeologist', 'Boss', 80, 'deranged_archaeologist', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Doom of Mokhaiotl', 'doom-of-mokhaiotl', 'Boss', 20, 'doom_of_mokhaiotl', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Duke Sucellus', 'duke-sucellus', 'Boss', 39, 'duke_sucellus', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'General Graardor', 'general-graardor', 'Boss', 58, 'general_graardor', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Giant Mole', 'giant-mole', 'Boss', 125, 'giant_mole', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Grotesque Guardians', 'grotesque-guardians', 'Boss', 37, 'grotesque_guardians', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Hespori', 'hespori', 'Boss', 60, 'hespori', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Kalphite Queen', 'kalphite-queen', 'Boss', 55, 'kalphite_queen', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'King Black Dragon', 'king-black-dragon', 'Boss', 130, 'king_black_dragon', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Kraken', 'kraken', 'Boss', 100, 'kraken', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Kree''Arra', 'kreearra', 'Boss', 40, 'kreearra', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'K''ril Tsutsaroth', 'kril-tsutsaroth', 'Boss', 65, 'kril_tsutsaroth', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Lunar Chests', 'lunar-chests', 'Boss', 18, 'lunar_chests', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Mimic', 'mimic', 'Boss', 60, 'mimic', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Nex', 'nex', 'Boss', 23.5, 'nex', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Nightmare', 'nightmare', 'Boss', 14, 'nightmare', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Obor', 'obor', 'Boss', 12, 'obor', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Phantom Muspah', 'phantom-muspah', 'Boss', 30, 'phantom_muspah', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Phosani''s Nightmare', 'phosanis-nightmare', 'Boss', 9.6, 'phosanis_nightmare', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Sarachnis', 'sarachnis', 'Boss', 110, 'sarachnis', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Scorpia', 'scorpia', 'Boss', 130, 'scorpia', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Scurrius', 'scurrius', 'Boss', 70, 'scurrius', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Shellbane Gryphon', 'shellbane-gryphon', 'Boss', 95, 'shellbane_gryphon', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Skotizo', 'skotizo', 'Boss', 45, 'skotizo', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Sol Heredit', 'sol-heredit', 'Boss', 2.7, 'sol_heredit', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Spindel', 'spindel', 'Boss', 55, 'spindel', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'The Corrupted Gauntlet', 'the-corrupted-gauntlet', 'Boss', 7.2, 'the_corrupted_gauntlet', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'The Hueycoatl', 'the-hueycoatl', 'Boss', 20, 'the_hueycoatl', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'The Gauntlet', 'the-gauntlet', 'Boss', 10, 'the_gauntlet', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'The Leviathan', 'the-leviathan', 'Boss', 31, 'the_leviathan', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'The Royal Titans', 'the-royal-titans', 'Boss', 55, 'the_royal_titans', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'The Whisperer', 'the-whisperer', 'Boss', 22, 'the_whisperer', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Theatre of Blood (HM)', 'theatre-of-blood-hm', 'Boss', 3, 'theatre_of_blood_hard_mode', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Theatre of Blood', 'theatre-of-blood', 'Boss', 3.2, 'theatre_of_blood', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Thermonuclear Smoke Devil', 'thermonuclear-smoke-devil', 'Boss', 150, 'thermonuclear_smoke_devil', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Tombs of Amascut (Expert Mode)', 'tombs-of-amascut-expert', 'Boss', 3, 'tombs_of_amascut_expert', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Tombs of Amascut', 'tombs-of-amascut', 'Boss', 3.7, 'tombs_of_amascut', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'TzKal-Zuk', 'tzkal-zuk', 'Boss', 1, 'tzkal_zuk', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'TzTok-Jad', 'tztok-jad', 'Boss', 2.5, 'tztok_jad', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Vardorvis', 'vardorvis', 'Boss', 39, 'vardorvis', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Venenatis', 'venenatis', 'Boss', 80, 'venenatis', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Vet''ion', 'vetion', 'Boss', 50, 'vetion', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Vorkath', 'vorkath', 'Boss', 34, 'vorkath', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Yama', 'yama', 'Boss', 18, 'yama', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL),
                (gen_random_uuid(), 'Zulrah', 'zulrah', 'Boss', 46, 'zulrah', 'https://wiseoldman.net/ehb/main', TIMESTAMPTZ '2026-07-12 00:00:00+00', TRUE, NULL, NULL)
            ON CONFLICT (slug) DO UPDATE SET
                name = EXCLUDED.name,
                category = EXCLUDED.category,
                efficient_completions_per_hour = EXCLUDED.efficient_completions_per_hour,
                external_identifier = EXCLUDED.external_identifier,
                data_source = EXCLUDED.data_source,
                data_updated_at = EXCLUDED.data_updated_at;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM boss_activities WHERE data_source = 'https://wiseoldman.net/ehb/main';");
    }
}
