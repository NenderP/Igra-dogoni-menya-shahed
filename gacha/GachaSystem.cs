namespace Gacha;

/// <summary>
/// Ядро гачи: ролл редкости с учётом пити, выбор персонажа, начисление пыли/коллекции.
/// Соответствует protocol-v0.md:91 (gacha_pull → gacha_result).
/// Все роллы server-authoritative, детерминированы от Random.
/// </summary>
public class GachaSystem
{
    private readonly Random _rng;
    private readonly GachaBanner _banner;

    public GachaSystem(Random? rng = null, GachaBanner? banner = null)
    {
        _rng = rng ?? new Random();
        _banner = banner ?? new GachaBanner();
    }

    public record GachaItem(string DefId, int Rarity, bool IsNew, int ConvertedToDust);
    public record GachaPullResult(
        List<GachaItem> Items,
        PityState PityAfter,
        int DustBalance,
        int CurrencySpent
    );

    /// <summary>Сделать 1 или 10 круток. Мутирует PlayerCollection (пыль, пити, owned).</summary>
    public GachaPullResult Pull(PlayerCollection col, int count)
    {
        if (count != 1 && count != 10) throw new ArgumentException("count must be 1 or 10");
        var items = new List<GachaItem>(count);

        for (int i = 0; i < count; i++)
        {
            int rarity = RollRarity(col);
            string defId;

            if (rarity == 5)
            {
                defId = _banner.Resolve5Star(col, _rng);
                col.PullsSince5Star = 0;
                col.PullsSince4Star++;
            }
            else if (rarity == 4)
            {
                defId = _banner.PickRandom(4, _rng);
                col.PullsSince5Star++;
                col.PullsSince4Star = 0;
            }
            else
            {
                defId = _banner.PickRandom(3, _rng);
                col.PullsSince5Star++;
                col.PullsSince4Star++;
            }

            col.TotalPulls++;

            bool isNew = !col.Owns(defId);
            int dust = 0;
            if (isNew) col.AddCopy(defId);
            else
            {
                dust = DustSystem.ConvertDuplicate(col, rarity);
                // копию всё равно считаем для статистики? — да, но is_new=false
                col.AddCopy(defId);
            }

            items.Add(new GachaItem(defId, rarity, isNew, dust));
        }

        var pityAfter = new PityState(col.PullsSince5Star, col.GuaranteedFeatured);
        return new GachaPullResult(items, pityAfter, col.Dust, CurrencySpent: 0);
    }

    /// <summary>Ролл редкости с учётом пити.</summary>
    public int RollRarity(PlayerCollection col)
    {
        // Хард-пити 5★ на 90
        if (col.PullsSince5Star + 1 >= GachaConfig.HardPity5Star) return 5;

        // Хард-пити 4★ на 10 (если давно не было 4★+): гарант срабатывает только если не выпал 5★
        bool need4Star = col.PullsSince4Star + 1 >= GachaConfig.HardPity4Star;

        double rate5 = Effective5StarRate(col);
        double r = _rng.NextDouble();
        if (r < rate5) return 5;

        // Условный шанс 4★ при условии "не 5★": Rate4 / (1 - rate5), нормировка на остаток
        double p4Conditional = GachaConfig.Rate4Star / (1 - rate5);
        if (need4Star || _rng.NextDouble() < p4Conditional) return 4;

        return 3;
    }

    private double Effective5StarRate(PlayerCollection col)
    {
        int nextPull = col.PullsSince5Star + 1;
        if (nextPull < GachaConfig.SoftPityStart) return GachaConfig.Rate5Star;
        if (nextPull >= GachaConfig.HardPity5Star) return 1.0;
        // линейный софт-пити
        double steps = nextPull - GachaConfig.SoftPityStart + 1;
        double rate = GachaConfig.Rate5Star + steps * GachaConfig.SoftPityStep;
        return Math.Min(rate, 1.0);
    }

    /// <summary>Симуляция для баланса — прогнать N пуллов с нуля и собрать статистику.</summary>
    public static Dictionary<int, int> Simulate(int pulls, int seed = 42)
    {
        var rng = new Random(seed);
        var sys = new GachaSystem(rng);
        var col = new PlayerCollection();
        var hist = new Dictionary<int, int> { [3] = 0, [4] = 0, [5] = 0 };
        for (int i = 0; i < pulls; i++)
        {
            var res = sys.Pull(col, 1);
            hist[res.Items[0].Rarity]++;
        }
        return hist;
    }
}
