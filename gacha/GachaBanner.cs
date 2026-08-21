namespace Gacha;

/// <summary>
/// Баннер v1 — пул 8 персонажей из /shared/content.md. Фичер — один 5★.
/// При выпадении 5★: 50% фичер, иначе следующий 5★ гарант фичером.
/// </summary>
public class GachaBanner
{
    public string Featured5StarDefId { get; } = "char_eclipse_sovereign";
    public string NonFeatured5StarDefId { get; } = "char_dawn_herald";

    // Пулы по редкости (def_id) — синхронизированы с /shared/content.md:10
    public readonly string[] Pool5Star = new[]
    {
        "char_eclipse_sovereign",
        "char_dawn_herald"
    };

    public readonly string[] Pool4Star = new[]
    {
        "char_day_mage",
        "char_night_assassin",
        "char_twilight_trickster",
        "char_dusk_scout"
    };

    public readonly string[] Pool3Star = new[]
    {
        "char_day_squire",
        "char_night_initiate"
    };

    /// <summary>Выбрать случайного персонажа заданной редкости.</summary>
    public string PickRandom(int rarity, Random rng) => rarity switch
    {
        5 => Pool5Star[rng.Next(Pool5Star.Length)],
        4 => Pool4Star[rng.Next(Pool4Star.Length)],
        3 => Pool3Star[rng.Next(Pool3Star.Length)],
        _ => throw new ArgumentOutOfRangeException(nameof(rarity))
    };

    /// <summary>Разрулить 50/50 для 5★ с учётом гаранта.</summary>
    public string Resolve5Star(PlayerCollection col, Random rng)
    {
        if (col.GuaranteedFeatured)
        {
            col.GuaranteedFeatured = false;
            return Featured5StarDefId;
        }

        bool win = rng.NextDouble() < GachaConfig.FeaturedWinRate;
        if (win) return Featured5StarDefId;
        // проиграл 50/50 — следующий 5★ гарантирован
        col.GuaranteedFeatured = true;
        return NonFeatured5StarDefId;
    }
}
