namespace Bingo.Application.Catalogue;

public static class BossArtworkFamily
{
    public static string Key(string bossName) => bossName switch
    {
        "Artio" or "Callisto" => "wilderness-callisto",
        "Spindel" or "Venenatis" => "wilderness-venenatis",
        "Calvar'ion" or "Vet'ion" => "wilderness-vetion",
        "Chambers of Xeric" or "Chambers of Xeric (CM)" => "chambers-of-xeric",
        "Phosani's Nightmare" or "The Nightmare" => "nightmare",
        "Theatre of Blood" or "Theatre of Blood (HM)" => "theatre-of-blood",
        "Tombs of Amascut" or "Tombs of Amascut (Expert Mode)" => "tombs-of-amascut",
        "The Gauntlet" or "The Corrupted Gauntlet" => "gauntlet",
        _ => "boss:" + bossName
    };

    public static int Priority(string bossName) => bossName switch
    {
        "Callisto" or "Venenatis" or "Vet'ion" or
        "Chambers of Xeric" or "The Nightmare" or "Theatre of Blood" or
        "Tombs of Amascut" or "The Gauntlet" => 0,
        _ => 1
    };
}
