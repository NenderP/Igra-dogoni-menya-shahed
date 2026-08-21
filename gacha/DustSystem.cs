namespace Gacha;

/// <summary>
/// Пыль: дубли → пыль и крафт круток.
/// Конвертация: 3★=5, 4★=25, 5★=100 (GachaConfig).
/// Крафт: DustPerPull (дефолт 60, диапазон 50–75) пыли = 1 крутка.
/// </summary>
public static class DustSystem
{
    public static int DustForRarity(int rarity) => rarity switch
    {
        5 => GachaConfig.Dust5Star,
        4 => GachaConfig.Dust4Star,
        3 => GachaConfig.Dust3Star,
        _ => 0
    };

    /// <summary>Начислить пыль за дубль, вернуть сколько начислили.</summary>
    public static int ConvertDuplicate(PlayerCollection col, int rarity)
    {
        var dust = DustForRarity(rarity);
        col.Dust += dust;
        return dust;
    }

    /// <summary>Обменять пыль на крутки. Возвращает сколько круток реально выдали.</summary>
    public static int DustToPulls(PlayerCollection col, int pullsRequested, int? costPerPull = null)
    {
        int cost = costPerPull ?? GachaConfig.DustPerPull;
        if (cost < GachaConfig.DustPerPullMin) cost = GachaConfig.DustPerPullMin;
        if (cost > GachaConfig.DustPerPullMax) cost = GachaConfig.DustPerPullMax;

        int affordable = col.Dust / cost;
        int toGive = Math.Min(pullsRequested, affordable);
        col.Dust -= toGive * cost;
        return toGive;
    }

    public static bool CanAfford(PlayerCollection col, int pulls, int? costPerPull = null)
    {
        int cost = costPerPull ?? GachaConfig.DustPerPull;
        return col.Dust >= pulls * cost;
    }
}
